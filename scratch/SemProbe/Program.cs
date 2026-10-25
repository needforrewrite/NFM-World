// LLM maintained.
// Does the original HLSL semantic survive glslang -> SPIR-V, and can spirv-cross
// recover it on the way out?
//
// This decides how the sokol backend gets correct D3D11 input-layout semantics for a
// generated bundle. D3D11 matches the layout's declared semantic NAME against the
// compiled vertex shader's own signature, and spirv-cross by default renames every
// vertex input to TEXCOORD<location> - so a layout declaring POSITION0/COLOR0 is
// rejected (0x80070057). Two spirv-cross mechanisms could fix it:
//
//   A. spvc_compiler_hlsl_add_vertex_attribute_remap - an explicit location -> semantic
//      table, applied per compiler.
//   B. SPVC_COMPILER_OPTION_HLSL_USER_SEMANTIC - makes spirv-cross read
//      SpvDecorationUserSemantic strings as literal HLSL semantics.
//
// B only works if glslang actually emits a UserSemantic decoration carrying "POSITION0".
// This probe checks that, and checks A works regardless.
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Shaderc;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Cross;
// Compiler/CompilerOptions exist in both namespaces; the Cross ones are the ones the
// spirv-cross API takes, so pin them here as SpirvCrossReflector does.
using CrossCompiler = Silk.NET.SPIRV.Cross.Compiler;
using CrossCompilerOptions = Silk.NET.SPIRV.Cross.CompilerOptions;
using ExecutionModel = Silk.NET.SPIRV.ExecutionModel;

namespace SemProbe;

internal static unsafe class Program
{
    // Deliberately the semantics the app's real layouts use, including two of the same
    // base name at different indices, so the probe can tell an index-preserving
    // mechanism from one that collapses everything to index 0.
    private const string Source = """
        struct VSInput
        {
            float3 Position  : POSITION0;
            float4 Color     : COLOR0;
            float3 Normal    : NORMAL0;
            float3 Centroid  : POSITION2;
        };
        float4 main(VSInput input) : SV_POSITION
        {
            return float4(input.Position + input.Color.xyz + input.Normal + input.Centroid, 1.0);
        }
        """;

    private static int Main()
    {
        var mode = Environment.GetCommandLineArgs().ElementAtOrDefault(1);
        if (mode == "location") return LocationProbe.Run();

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
            "semprobe.hlsl", "main", options);

        if (shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
        {
            Console.WriteLine($"compile failed: {shaderc.ResultGetErrorMessageS(result)}");
            return 1;
        }

        var spirv = new byte[(int)shaderc.ResultGetLength(result)];
        new ReadOnlySpan<byte>(shaderc.ResultGetBytes(result), spirv.Length).CopyTo(spirv);
        Console.WriteLine($"compiled: {spirv.Length} bytes of SPIR-V");
        shaderc.ResultRelease(result);

        Context* context = null;
        cross.ContextCreate(&context);
        ParsedIr* ir = null;
        fixed (byte* p = spirv)
            cross.ContextParseSpirv(context, (uint*)p, (UIntPtr)(spirv.Length / 4), &ir);

        // --- 1. does the SPIR-V carry UserSemantic decorations? ---
        Console.WriteLine();
        Console.WriteLine("--- decorations on ids that have a name (looking for UserSemantic) ---");
        DumpDecorations(cross, ir);

        // --- 2. default HLSL output, for comparison ---
        Console.WriteLine();
        DumpInputs("DEFAULT", cross, ir, (o) => cross.CompilerOptionsSetUint(o, CompilerOption.HlslShaderModel, 50));

        // --- 3. mechanism B: HLSL_USER_SEMANTIC ---
        Console.WriteLine();
        DumpInputs("HLSL_USER_SEMANTIC=1", cross, ir, (o) =>
        {
            cross.CompilerOptionsSetUint(o, CompilerOption.HlslShaderModel, 50);
            cross.CompilerOptionsSetUint(o, CompilerOption.HlslUserSemantic, 1);
        });

        // --- 4. mechanism A: explicit remap table ---
        Console.WriteLine();
        DumpInputs("REMAP", cross, ir,
            (o) => cross.CompilerOptionsSetUint(o, CompilerOption.HlslShaderModel, 50),
            remap: ApplyRemap);

        cross.ContextDestroy(context);
        return 0;
    }

    /// <summary>
    /// The remap table for the fixture's four inputs, mirroring what a bundle emitter would
    /// build from the reflected stage inputs.
    /// </summary>
    private static void ApplyRemap(Silk.NET.SPIRV.Cross.Cross cross, CrossCompiler* cc)
    {
        var pairs = new (uint Loc, string Sem)[] { (0, "POSITION0"), (1, "COLOR0"), (2, "NORMAL0"), (3, "POSITION2") };
        var pinned = new List<GCHandle>();
        try
        {
            var table = new HlslVertexAttributeRemap[pairs.Length];
            for (var i = 0; i < pairs.Length; i++)
            {
                var handle = GCHandle.Alloc(Encoding.ASCII.GetBytes(pairs[i].Sem + "\0"), GCHandleType.Pinned);
                pinned.Add(handle);
                table[i] = new HlslVertexAttributeRemap
                {
                    Location = pairs[i].Loc,
                    Semantic = (byte*)handle.AddrOfPinnedObject(),
                };
            }
            fixed (HlslVertexAttributeRemap* t = table)
                cross.CompilerHlslAddVertexAttributeRemap(cc, t, (UIntPtr)table.Length);
        }
        finally
        {
            foreach (var h in pinned) h.Free();
        }
    }

    private static void DumpDecorations(Silk.NET.SPIRV.Cross.Cross cross, ParsedIr* ir)
    {
        Context* ctx = null;
        cross.ContextCreate(&ctx);
        CrossCompiler* cc = null;
        cross.ContextCreateCompiler(ctx, Backend.Hlsl, ir, CaptureMode.Copy, &cc);
        cross.CompilerSetEntryPoint(cc, "main", ExecutionModel.Vertex);

        var found = false;
        // Ids are not enumerable through the C API, so this sweeps a plausible range and
        // asks about the decorations that matter.
        for (uint id = 1; id < 256; id++)
        {
            var name = cross.CompilerGetNameS(cc, id);
            if (string.IsNullOrEmpty(name)) continue;
            // HasDecoration returns the C API's byte-based bool, not a C# bool.
            var hasLoc = cross.CompilerHasDecoration(cc, id, Decoration.Location) != 0;
            var hasSem = cross.CompilerHasDecoration(cc, id, Decoration.UserSemantic) != 0;
            if (!hasLoc && !hasSem) continue;
            var semText = hasSem ? cross.CompilerGetDecorationStringS(cc, id, Decoration.UserSemantic) : null;
            Console.WriteLine($"  id={id,-4} name={name,-12} location={hasLoc} userSemantic={hasSem}" +
                              (semText is null ? "" : $"  \"{semText}\""));
            found = true;
        }
        if (!found) Console.WriteLine("  (no named id carried a Location or UserSemantic decoration)");
        cross.ContextDestroy(ctx);
    }

    // Pointer types are not valid generic type arguments, so Action<T*> cannot be used
    // (the same constraint SpirvCrossReflector documents for its options callback).
    private delegate void OptionsConfigurator(CrossCompilerOptions* options);
    private delegate void Remapper(Silk.NET.SPIRV.Cross.Cross cross, CrossCompiler* compiler);

    private static void DumpInputs(
        string label,
        Silk.NET.SPIRV.Cross.Cross cross,
        ParsedIr* ir,
        OptionsConfigurator configure,
        Remapper? remap = null)
    {
        Context* ctx = null;
        cross.ContextCreate(&ctx);
        CrossCompiler* cc = null;
        cross.ContextCreateCompiler(ctx, Backend.Hlsl, ir, CaptureMode.Copy, &cc);
        cross.CompilerSetEntryPoint(cc, "main", ExecutionModel.Vertex);

        CrossCompilerOptions* o = null;
        cross.CompilerCreateCompilerOptions(cc, &o);
        configure(o);
        cross.CompilerInstallCompilerOptions(cc, o);
        remap?.Invoke(cross, cc);

        Console.WriteLine($"--- {label}: SPIRV_Cross_Input ---");
        byte* outSrc = null;
        if (cross.CompilerCompile(cc, &outSrc) != Result.Success)
        {
            Console.WriteLine($"  compile failed: {cross.ContextGetLastErrorStringS(ctx)}");
            cross.ContextDestroy(ctx);
            return;
        }

        var text = Marshal.PtrToStringUTF8((nint)outSrc) ?? "";
        var inStruct = false;
        foreach (var line in text.Split('\n'))
        {
            if (line.Contains("SPIRV_Cross_Input")) { inStruct = true; continue; }
            if (!inStruct) continue;
            if (line.Trim() == "};") break;
            if (line.Trim().Length > 0) Console.WriteLine("  " + line.Trim());
        }
        cross.ContextDestroy(ctx);
    }
}
