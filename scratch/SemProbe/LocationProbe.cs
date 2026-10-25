// LLM maintained.
// How does glslang assign Location to HLSL vertex inputs - declaration order, or in
// semantic-index order?
//
// This matters because the remap table that restores semantic names is keyed by Location
// (spvc_hlsl_vertex_attribute_remap { unsigned location; const char *semantic; }), so the
// emitter has to know which location each declared semantic ends up at. The fixture is the
// real Line.fx input struct, chosen because its semantic indices are deliberately NOT in
// declaration order (POSITION0, POSITION1, TEXCOORD0, NORMAL0, COLOR0, POSITION2, TEXCOORD1) -
// a declaration-order assignment and a semantic-index assignment give different answers, so
// this fixture can tell them apart where a tidy one could not.
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Shaderc;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Cross;
using CrossCompiler = Silk.NET.SPIRV.Cross.Compiler;
using CrossCompilerOptions = Silk.NET.SPIRV.Cross.CompilerOptions;
using ExecutionModel = Silk.NET.SPIRV.ExecutionModel;

namespace SemProbe;

internal static unsafe class LocationProbe
{
    /// <summary>The real nfm-world/data/shaders/Line.fx VertexShaderInput, verbatim.</summary>
    private const string Source = """
        struct VertexShaderInput
        {
            float3 PositionA : POSITION0;
            float3 PositionB : POSITION1;
            float Side : TEXCOORD0;
            float3 Normal : NORMAL0;
            float3 Color : COLOR0;
            float3 Centroid : POSITION2;
            float DecalOffset : TEXCOORD1;
        };
        float4 main(VertexShaderInput input) : SV_POSITION
        {
            return float4(input.PositionA + input.PositionB + input.Side
                        + input.Normal + input.Color + input.Centroid + input.DecalOffset, 1.0);
        }
        """;

    /// <summary>
    /// The fixture's declarations in source order: field name to declared semantic. The
    /// comparison has to go through the *field name*, because the reflected name glslang
    /// reports is the HLSL field (PositionA), never the semantic (POSITION0) - comparing the
    /// two textually is meaningless and was this probe's original bug.
    /// </summary>
    private static readonly (string Field, string Semantic)[] Declared =
    [
        ("PositionA", "POSITION0"),
        ("PositionB", "POSITION1"),
        ("Side", "TEXCOORD0"),
        ("Normal", "NORMAL0"),
        ("Color", "COLOR0"),
        ("Centroid", "POSITION2"),
        ("DecalOffset", "TEXCOORD1"),
    ];

    public static int Run()
    {
        var shaderc = Shaderc.GetApi();
        var cross = Silk.NET.SPIRV.Cross.Cross.GetApi();

        var compiler = shaderc.CompilerInitialize();
        var options = shaderc.CompileOptionsInitialize();
        shaderc.CompileOptionsSetSourceLanguage(options, Silk.NET.Shaderc.SourceLanguage.Hlsl);
        shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan11);
        shaderc.CompileOptionsSetTargetSpirv(options, SpirvVersion.Shaderc13);
        shaderc.CompileOptionsSetGenerateDebugInfo(options);
        shaderc.CompileOptionsSetHlslOffsets(options, 1);

        var bytes = Encoding.UTF8.GetBytes(Source);
        var result = shaderc.CompileIntoSpv(compiler, Source, (UIntPtr)bytes.Length, ShaderKind.VertexShader,
            "line.hlsl", "main", options);
        if (shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
        {
            Console.WriteLine($"compile failed: {shaderc.ResultGetErrorMessageS(result)}");
            return 1;
        }

        var spirv = new byte[(int)shaderc.ResultGetLength(result)];
        new ReadOnlySpan<byte>(shaderc.ResultGetBytes(result), spirv.Length).CopyTo(spirv);
        shaderc.ResultRelease(result);

        Context* context = null;
        cross.ContextCreate(&context);
        ParsedIr* ir = null;
        fixed (byte* p = spirv)
            cross.ContextParseSpirv(context, (uint*)p, (UIntPtr)(spirv.Length / 4), &ir);

        CrossCompiler* cc = null;
        cross.ContextCreateCompiler(context, Backend.Hlsl, ir, CaptureMode.Copy, &cc);
        cross.CompilerSetEntryPoint(cc, "main", ExecutionModel.Vertex);
        CrossCompilerOptions* o = null;
        cross.CompilerCreateCompilerOptions(cc, &o);
        cross.CompilerOptionsSetUint(o, CompilerOption.HlslShaderModel, 50);
        cross.CompilerInstallCompilerOptions(cc, o);

        Console.WriteLine("SPIR-V stage inputs, as glslang assigned them:");
        var byLocation = new Dictionary<uint, string>();
        for (uint id = 1; id < 256; id++)
        {
            var name = cross.CompilerGetNameS(cc, id);
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.StartsWith("input.")) continue;
            var loc = cross.CompilerGetDecoration(cc, id, Decoration.Location);
            byLocation[loc] = name["input.".Length..];
            Console.WriteLine($"  location {loc} = {name["input.".Length..]}");
        }

        Console.WriteLine();
        Console.WriteLine("Correlating each emitted Location back to the declared semantic:");
        var ordered = byLocation.OrderBy(kv => kv.Key).ToList();
        var declarationOrderMatches = ordered.Count == Declared.Length;
        for (var i = 0; i < ordered.Count; i++)
        {
            // The reflected name is the HLSL *field*, so the check is field name -> declared
            // semantic by position, which is exactly what the emitter would have to zip.
            var expectedField = i < Declared.Length ? Declared[i].Field : "(none)";
            var expectedSemantic = i < Declared.Length ? Declared[i].Semantic : "(none)";
            var matches = ordered[i].Value == expectedField;
            if (!matches) declarationOrderMatches = false;
            Console.WriteLine($"  location {ordered[i].Key}: field '{ordered[i].Value}' " +
                              $"= declared '{expectedSemantic}'" + (matches ? "  ok" : "  <-- NOT in declaration order"));
        }

        Console.WriteLine();
        Console.WriteLine(declarationOrderMatches
            ? "RESULT: glslang assigns Location in DECLARATION order, so the declared semantics zip 1:1 onto locations."
            : "RESULT: Location is NOT declaration order - the emitter cannot zip the two lists directly.");

        cross.ContextDestroy(context);
        return 0;
    }
}
