// LLM maintained.
//
// Reads the FX `technique` blocks that the compiler has to strip before glslang sees the
// source: they are effect-framework syntax, not HLSL, so they cannot be compiled - but they
// are also the only place the per-technique vertex and pixel entry points are written down.
// Stripping and reading are therefore two halves of the same step, which is why this sits
// beside the stripper rather than in the parser layer.
//
// The blocks reach this file already run through the preprocessor, so every one of them is
// live: the dialect guards in the `.fx` sources sit inside the shader bodies, never around a
// technique.
using System.Text.RegularExpressions;

namespace NFMWorld.ShaderCompiler;

/// <summary>One technique's GPU program: a name, and the two entry points it compiles.</summary>
public sealed record Technique(string Name, string VertexEntry, string PixelEntry);

/// <summary>
/// Extracts the techniques declared in an FX source.
///
/// An FX `technique` block is not HLSL, so glslang's front end has no parser for one. It is
/// where the effect framework records which functions each pass compiles, though, and that is
/// the metadata this compiler needs: a technique is exactly one program, and the GL backend
/// builds one program per vertex/pixel entry-point pair (it has no technique concept at all),
/// so the mapping has to be read out here and turned into one bundle per technique.
///
/// The other parsing machinery in this repository cannot be reused for it. `HLSLParser` does
/// build an `HLSLTree` with `HLSLTechnique` nodes, but its `ParseStateValue` deliberately skips
/// the value of an unrecognised state - which is what a `compile` statement is - so the entry
/// points are discarded before anything could read them.
/// </summary>
public static class TechniqueParser
{
    // `technique Name` - the brace may follow on the same line or the next, so it is matched
    // separately rather than as part of this.
    private static readonly Regex TechniqueHeader = new(
        @"^\s*technique\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    // `VertexShader = compile vs_3_0 MainVS();` - the shader model is matched but not captured,
    // because it is a D3D9 detail with no bearing on what the entry point compiles to here.
    private static readonly Regex StageAssignment = new(
        @"^\s*(VertexShader|PixelShader)\s*=\s*compile\s+[A-Za-z_]\w*\s+([A-Za-z_]\w*)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// The techniques declared in <paramref name="source"/>, in declaration order.
    ///
    /// Throws if a technique is missing either stage: a technique with no vertex or no pixel
    /// entry point cannot become a program, and silently emitting nothing for it would surface
    /// later as a missing shader rather than as the malformed source it is.
    /// </summary>
    public static IReadOnlyList<Technique> Parse(string source, string fileName)
    {
        var techniques = new List<Technique>();

        string? name = null;
        string? vertexEntry = null;
        string? pixelEntry = null;
        var depth = 0;

        foreach (var line in source.Split('\n'))
        {
            if (name is null)
            {
                var header = TechniqueHeader.Match(line);
                if (!header.Success)
                    continue;

                name = header.Groups[1].Value;
                vertexEntry = null;
                pixelEntry = null;
                depth = 0;
            }

            // The entry-point assignments sit inside the pass, which is inside the technique, so
            // both branches below see them before the closing brace ends the block.
            var assignment = StageAssignment.Match(line);
            if (assignment.Success)
            {
                if (assignment.Groups[1].Value == "VertexShader")
                    vertexEntry = assignment.Groups[2].Value;
                else
                    pixelEntry = assignment.Groups[2].Value;
            }

            // Brace counting rather than a lazy regex: the block is nested (technique > pass) and
            // a lazy match would stop at the first inner closing brace. A header line without its
            // brace contributes nothing and leaves the depth at zero, so the block stays open
            // until the real opening brace arrives - tracking only the depth would end it early.
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (depth > 0 || !line.Contains('}'))
                continue;

            if (vertexEntry is null)
                throw new InvalidOperationException(
                    $"{fileName}: technique '{name}' declares no VertexShader entry point.");
            if (pixelEntry is null)
                throw new InvalidOperationException(
                    $"{fileName}: technique '{name}' declares no PixelShader entry point.");

            techniques.Add(new Technique(name, vertexEntry, pixelEntry));
            name = null;
        }

        if (name is not null)
            throw new InvalidOperationException(
                $"{fileName}: technique '{name}' has no closing brace - the source is truncated.");

        return techniques;
    }

    /// <summary>
    /// Removes every <c>technique</c> block, leaving the source glslang can compile.
    ///
    /// Split from <see cref="Parse"/> rather than folded into it so a caller can read the
    /// techniques and strip them in whichever order reads best; they are independent passes over
    /// the same text.
    /// </summary>
    public static string Strip(string source)
    {
        var kept = new List<string>();
        var inTechnique = false;
        var depth = 0;

        foreach (var line in source.Split('\n'))
        {
            if (!inTechnique)
            {
                if (!TechniqueHeader.IsMatch(line))
                {
                    kept.Add(line);
                    continue;
                }

                // The opening brace may be on this line (`technique X {`) or the next.
                inTechnique = true;
            }

            // See Parse: the depth only starts counting once the brace that opens the block
            // appears, so a header on its own line does not end the block immediately.
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (depth <= 0 && line.Contains('}'))
            {
                inTechnique = false;
                depth = 0;
            }
        }

        return string.Join('\n', kept);
    }
}
