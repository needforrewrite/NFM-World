// LLM maintained.
//
// Reconciles the two WGSL stages' varying interfaces.
//
// HLSL lets a vertex shader return more than the pixel shader reads (Nvg.fx's simple technique is
// one: its VS writes TEXCOORD1, its PS never declares it), and every D3D and GL backend here
// tolerates that. WebGPU does not: wgpu refuses a render pipeline whose vertex stage provides a
// location the fragment stage does not consume, and it reports that by handing back a pipeline
// object in an error state rather than by failing the call - so the draw silently renders nothing.
//
// The fix is made on the fragment side rather than by pruning the vertex output: removing a struct
// member means rewriting the entry point's return expression too. An unused fragment parameter is
// legal WGSL and changes nothing about what the stage computes - it just declares the interface the
// pipeline is validated against.
//
// The parser is written against tint's output, which is regular: a vertex stage returns one struct
// whose interpolated members each carry `@location(Nu)` (and possibly `@interpolate(...)`), and a
// fragment entry point lists its inputs in the parameter list.

using System.Text.RegularExpressions;

namespace NFMWorld.ShaderCompiler;

internal static class WgslStageInterface
{
    // Lazy rather than [^)]*: a parameter list carries its own parentheses in its attributes
    // (@location(0u)), so the first ')' is not the end of the list - the one before '->' is.
    private static readonly Regex VertexReturnType = new(
        @"@vertex\s*\n\s*fn\s+\w+\s*\([\s\S]*?\)\s*->\s*(\w+)"
    );

    private static readonly Regex StructMember = new(
        @"@location\((\d+)u\)((?:\s*@\w+\([^)]*\))*)\s*([A-Za-z_]\w*)\s*:\s*([^,\r\n]+)"
    );

    private static readonly Regex FragmentEntry = new(
        @"@fragment\s*\n\s*fn\s+(\w+)\s*\(([\s\S]*?)\)\s*->"
    );

    private static readonly Regex LocationAttribute = new(@"@location\((\d+)u\)");

    /// <summary>
    /// Appends a parameter to the fragment entry point for every vertex output location it does not
    /// consume. Returns the source unchanged when the stages already agree.
    /// </summary>
    public static string Reconcile(string vertexWgsl, string fragmentWgsl)
    {
        var missing = MissingInputs(vertexWgsl, fragmentWgsl);

        if (missing.Count == 0)

            return fragmentWgsl;

        var entry = FragmentEntry.Match(fragmentWgsl);

        if (!entry.Success)
        {
            throw new InvalidOperationException(
                "The fragment stage has no recognisable entry point, so its input interface cannot be "
                + "reconciled with the vertex stage's outputs."
            );
        }

        // An unused parameter is legal WGSL; the interpolation attribute has to be copied, because a
        // flat vertex output may only be read through a flat fragment input.
        var added = string.Join(
            ", ",
            missing.Select(v => $"@location({v.Location}u) {v.Attributes}nfmw_unused_{v.Location} : {v.Type}")
        );

        var parameters = entry.Groups[2];
        var separator = parameters.Value.Trim().Length == 0 ? "" : ", ";
        var insertAt = parameters.Index + parameters.Length;

        return string.Concat(fragmentWgsl.AsSpan(0, insertAt), separator, added, fragmentWgsl.AsSpan(insertAt));
    }

    private static List<Varying> MissingInputs(string vertexWgsl, string fragmentWgsl)
    {
        var missing = new List<Varying>();

        var returnType = VertexReturnType.Match(vertexWgsl);

        if (!returnType.Success)

            return missing;

        var body = Regex.Match(
            vertexWgsl,
            @"\bstruct\s+" + Regex.Escape(returnType.Groups[1].Value) + @"\s*\{(.*?)\n\}",
            RegexOptions.Singleline
        );

        if (!body.Success)
        {
            // The entry returns a type declared elsewhere, which this parser cannot follow. The
            // mismatch then surfaces as a pipeline validation error rather than silently.

            return missing;
        }

        var consumed = new HashSet<string>();
        var fragmentEntry = FragmentEntry.Match(fragmentWgsl);

        if (fragmentEntry.Success)
        {
            foreach (Match location in LocationAttribute.Matches(fragmentEntry.Groups[2].Value))
                consumed.Add(location.Groups[1].Value);
        }

        foreach (Match member in StructMember.Matches(body.Groups[1].Value))
        {
            var location = member.Groups[1].Value;

            if (consumed.Contains(location))

                continue;

            var attributes = member.Groups[2].Value.Trim();
            missing.Add(
                new Varying(
                    location,
                    attributes.Length == 0 ? "" : attributes + " ",
                    member.Groups[4].Value.Trim()
                )
            );
        }

        return missing;
    }

    private readonly record struct Varying(string Location, string Attributes, string Type);
}
