// LLM maintained.
using System.Text;
using System.Text.RegularExpressions;

namespace NFMWorld.ShaderCompiler;

/// <summary>
/// Recovers the semantic names a vertex entry point declared its inputs with.
///
/// glslang's HLSL front-end throws the semantics away: a semantic that was written
/// <c>POSITION0</c> becomes a bare <c>Location</c> decoration on an input variable, and the
/// string itself is nowhere in the SPIR-V (see
/// <c>scratch/SemProbe/Program.cs</c>, which checked every stage-input id for
/// <c>SpvDecorationUserSemantic</c> and found none). So the only place the semantics exist
/// is the HLSL text, and they have to be read back out of it before glslang runs.
///
/// This is what <see cref="SpirvCrossReflector"/> feeds to spirv-cross's
/// <c>add_vertex_attribute_remap</c>, which is what turns spirv-cross's default
/// <c>TEXCOORD&lt;location&gt;</c> naming back into the names the caller's vertex layouts
/// declare. D3D11 matches an input layout's semantic name against the compiled shader's own
/// signature, so without this a layout declaring <c>POSITION0</c> is rejected outright.
///
/// Two source shapes have to be handled, because the shaders in this repository use both:
/// <code>
/// VertexShaderOutput VertexShaderFunction(float4 Position : POSITION, float4 Color : COLOR0)
/// float4 main(VertexShaderInput input)          // struct declared separately
/// </code>
/// </summary>
public static class HlslSemantics
{
    /// <summary>
    /// One declared vertex input: the field name glslang will reflect, its semantic, and how many
    /// consecutive registers it covers.
    ///
    /// <see cref="Registers"/> is 1 for everything except a matrix, which the HLSL compiler
    /// expands into one register per column - that is why a <c>float4x4 world : TEXCOORD3</c>
    /// input is declared as the four attributes TEXCOORD3..6 on the pipeline's layout.
    /// </summary>
    public sealed record DeclaredInput(string Field, string Semantic, int Registers = 1);

    private static readonly Regex StructRegex = new(
        @"\bstruct\s+(\w+)\s*\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    private static readonly Regex StructFieldRegex = new(
        @"([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*:\s*([A-Za-z_]\w*)", RegexOptions.Compiled);

    // A parameter, with or without a semantic, with optional direction/modifier keywords.
    private static readonly Regex ParameterRegex = new(
        @"^(?:(?:in|out|inout|const|uniform|precise|linear|centroid|nointerpolation|noperspective)\s+)*" +
        @"([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*(?::\s*([A-Za-z_]\w*))?$",
        RegexOptions.Compiled);

    /// <summary>
    /// The vertex inputs declared by <paramref name="entryPoint"/>'s signature, in declaration
    /// order. System-value semantics (<c>SV_VertexID</c> and friends) are skipped: glslang maps
    /// those to builtins rather than to locations, so they are not layout attributes and
    /// spirv-cross cannot be asked to rename them.
    /// </summary>
    public static IReadOnlyList<DeclaredInput> ForEntryPoint(string source, string entryPoint)
    {
        var text = StripComments(source);
        var parameters = ParametersOf(text, entryPoint);
        if (parameters is null)
            return Array.Empty<DeclaredInput>();

        var structs = ParseStructs(text);
        var inputs = new List<DeclaredInput>();

        foreach (var parameter in SplitTopLevel(parameters))
        {
            var match = ParameterRegex.Match(parameter.Trim());
            if (!match.Success)
                continue;

            var type = match.Groups[1].Value;
            var field = match.Groups[2].Value;
            var semantic = match.Groups[3].Success ? match.Groups[3].Value : null;

            if (semantic is not null)
            {
                if (!IsSystemValue(semantic))
                    inputs.Add(new DeclaredInput(field, semantic, MatrixRows(type)));
                continue;
            }

            // A struct-typed parameter contributes its fields, in field order.
            if (structs.TryGetValue(type, out var fields))
                inputs.AddRange(fields);
        }

        return inputs;
    }

    private static bool IsSystemValue(string semantic) =>
        semantic.StartsWith("SV_", StringComparison.OrdinalIgnoreCase);

    // A matrix type is `<base><rows>x<cols>` (float4x4, float3x3). The qualifier is matched off
    // first so the digits sit at the end of the string, which distinguishes a matrix from the
    // scalar/vector types whose only digits are the component count (float4, half2).
    private static readonly Regex MatrixTypeRegex = new(
        @"^(?:(?:row_major|column_major)\s+)?[A-Za-z_]\w*?(\d+)x(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// How many registers a parameter type covers: the row count for a matrix type
    /// (<c>float4x4</c> -&gt; 4, <c>float3x3</c> -&gt; 3), one for everything else.
    /// </summary>
    private static int MatrixRows(string type)
    {
        var match = MatrixTypeRegex.Match(type);
        if (!match.Success)
            return 1;
        var rows = int.Parse(match.Groups[1].Value);
        return rows > 1 ? rows : 1;
    }

    /// <summary>
    /// Splits a struct type name to the semantics of its fields. Nested struct types are not
    /// expanded (no shader here uses one), which is why the body match excludes braces.
    /// </summary>
    private static Dictionary<string, List<DeclaredInput>> ParseStructs(string text)
    {
        var structs = new Dictionary<string, List<DeclaredInput>>(StringComparer.Ordinal);
        foreach (Match match in StructRegex.Matches(text))
        {
            var fields = new List<DeclaredInput>();
            foreach (Match field in StructFieldRegex.Matches(match.Groups["body"].Value))
            {
                var semantic = field.Groups[3].Value;
                if (!IsSystemValue(semantic))
                    fields.Add(new DeclaredInput(field.Groups[2].Value, semantic, MatrixRows(field.Groups[1].Value)));
            }
            structs[match.Groups[1].Value] = fields;
        }
        return structs;
    }

    /// <summary>
    /// Returns the text inside the parentheses of <paramref name="entryPoint"/>'s definition, or
    /// null if no definition is found. A definition is a parameter list followed by a body, which
    /// distinguishes it from a forward declaration sharing the name.
    /// </summary>
    private static string? ParametersOf(string text, string entryPoint)
    {
        foreach (Match match in Regex.Matches(text, $@"\b{Regex.Escape(entryPoint)}\s*\("))
        {
            var open = match.Index + match.Length - 1;
            var close = MatchingDelimiter(text, open, '(', ')');
            if (close < 0)
                continue;

            // A definition's body opens somewhere after the parameter list, before the next `;`.
            var rest = text[(close + 1)..];
            var terminator = rest.IndexOf(';');
            var brace = rest.IndexOf('{');
            if (brace >= 0 && (terminator < 0 || brace < terminator))
                return text[(open + 1)..close];
        }
        return null;
    }

    private static int MatchingDelimiter(string text, int open, char openChar, char closeChar)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == openChar) depth++;
            else if (text[i] == closeChar && --depth == 0) return i;
        }
        return -1;
    }

    /// <summary>Splits on commas that are not nested inside brackets or angle brackets.</summary>
    private static IEnumerable<string> SplitTopLevel(string text)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(' or '[' or '<': depth++; break;
                case ')' or ']' or '>': depth--; break;
                case ',' when depth == 0:
                    yield return text[start..i];
                    start = i + 1;
                    break;
            }
        }
        if (start < text.Length)
            yield return text[start..];
    }

    /// <summary>
    /// Removes <c>//</c> and <c>/* */</c> comments. The shaders in this repository carry
    /// commented-out parameter declarations and prose containing colons, either of which would
    /// otherwise be read as a semantic.
    /// </summary>
    private static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(i + 2, text.Length);
            }
            else
            {
                sb.Append(text[i++]);
            }
        }
        return sb.ToString();
    }
}
