// LLM maintained.
//
// The orchestration: one `.fx` source in, one C# bundle out per technique.
//
// This used to be the body of the POC's Program.cs, which could only compile one shader per run
// with the entry points passed on the command line. The build needs something a host can call, so
// the sequence lives here and both the MSBuild task and the command-line host are thin wrappers
// over it. The pipeline itself is unchanged: strip the effect-framework syntax, compile each
// stage's entry point to SPIR-V, cross-compile that to every backend's source form, merge the two
// stages' binding lists, and emit.
using System.Text;

namespace NFMWorld.ShaderCompiler;

/// <summary>One technique's finished bundle: the class name to emit, and its C# source.</summary>
public sealed record CompiledBundle(string ProgramName, string Technique, string Source);

/// <summary>
/// Compiles shader sources into bundles. Stateless between calls; the per-file state (the include
/// cache, the collector) is created and disposed inside <see cref="CompileFile"/>.
/// </summary>
public static class ShaderBuild
{
    /// <summary>
    /// The define that selects the D3D11 dialect in the `.fx` sources. Every source that samples a
    /// texture carries `#if SM6` guards around the `Texture2D`/`SamplerState` form, with the D3D9
    /// `texture`/`sampler_state` form under `#else`, so that one source compiles under both fxc and
    /// glslang. This compiler is glslang, so the define is always on here.
    /// </summary>
    public const string DialectDefine = "SM6";

    /// <summary>
    /// Compiles every technique declared in <paramref name="path"/>.
    ///
    /// One bundle per technique rather than per file, because a technique is exactly one program
    /// and the GL backend has no technique concept at all - it links one program per vertex/pixel
    /// entry-point pair - so a file declaring four techniques (Nvg.fx) yields four bundles.
    /// </summary>
    /// <param name="log">Receives one line per stage for the build log; may be null.</param>
    /// <exception cref="InvalidOperationException">
    /// A technique is malformed, or a stage fails to compile. Both are build errors: a silently
    /// skipped technique would surface later as a missing shader rather than as the bad source it is.
    /// </exception>
    public static IReadOnlyList<CompiledBundle> CompileFile(string path, Action<string>? log = null)
    {
        // Normalising to \n keeps the emitted raw string literals free of stray carriage returns and
        // makes the technique parser's line splitting agree with the compiler's input.
        var source = File.ReadAllText(path).Replace("\r\n", "\n");

        // Read the entry points before stripping: the `technique` blocks are where they are written
        // down, and Strip removes exactly those blocks.
        var techniques = TechniqueParser.Parse(source, path);
        if (techniques.Count == 0)
            throw new InvalidOperationException($"{path}: no technique is declared - nothing to compile.");

        var stripped = TechniqueParser.Strip(source);
        var shaderDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var shaderName = Path.GetFileNameWithoutExtension(path).Replace("-", "_");

        log?.Invoke($"==> {path} ({techniques.Count} technique(s): {string.Join(", ", techniques.Select(t => t.Name))})");

        // glslang asks the host to resolve #include; resolve relative to the shader's own directory,
        // which is how fxc and the FNA pipeline find `./Mad.fxh`. Cached because Mad.fxh is included
        // by four of the eight sources and re-reading it per include is pure waste.
        var includeCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        string? ResolveInclude(string target)
        {
            string key;
            try
            {
                key = Path.GetFullPath(Path.Combine(shaderDir, target));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A malformed include target is not an exception the caller can act on; returning
                // null makes glslang report it as the unresolved include it is, in its own message.
                return null;
            }

            if (!includeCache.TryGetValue(key, out var text))
                includeCache[key] = text = File.Exists(key) ? File.ReadAllText(key).Replace("\r\n", "\n") : null;
            return text;
        }

        // One collector for the whole file: it holds the spirv-cross context, and reusing it across
        // the stages of one source is what the POC did.
        using var spirvCompiler = new SpirvCompiler { IncludeResolver = ResolveInclude };
        using var collector = new SpirvCrossReflector();

        // Which uniforms the source declares `bool`. SPIR-V cannot say - HLSL promotes a bool to a
        // 4-byte int before it is lowered - so this is read from the text, and the reflection is
        // re-typed from it. Read once per file, not per stage: it is a property of the source.
        var boolUniforms = HlslBoolUniforms.Find(stripped, ResolveInclude);

        var defines = new Dictionary<string, string> { [DialectDefine] = "1" };
        var bundles = new List<CompiledBundle>(techniques.Count);

        foreach (var technique in techniques)
        {
            var sourcesByStage = new Dictionary<ShaderStage, StageSources>();
            var stages = new List<StageReflection>();

            // The vertex semantics live only in the source text - glslang drops them - so they are
            // read back out here, in declaration order, to be restored into the emitted HLSL.
            var declaredInputs = HlslSemantics.ForEntryPoint(stripped, technique.VertexEntry);

            foreach (var (stage, entry) in new[]
                     {
                         (ShaderStage.Vertex, technique.VertexEntry),
                         (ShaderStage.Fragment, technique.PixelEntry),
                     })
            {
                var (spirv, error) = spirvCompiler.Compile(stripped, path, stage, entry, defines);
                if (spirv is null)
                    throw new InvalidOperationException(
                        $"{path} [technique {technique.Name}]: {stage} stage HLSL->SPIR-V failed.\n{error}");

                var result = collector.ReflectAndCross(spirv, stage, entry, declaredInputs);
                sourcesByStage[stage] = new StageSources(spirv, result.Hlsl, result.Msl, result.GlslGl, result.GlslEs);
                stages.Add(HlslBoolUniforms.Mark(result.Reflection, boolUniforms));

                log?.Invoke($"    {technique.Name,-16} {stage,-8} SPIR-V {spirv.Length,7} B   " +
                            $"HLSL {LineCount(result.Hlsl),5} ln   MSL {LineCount(result.Msl),5} ln   " +
                            $"GLSL {LineCount(result.GlslGl),5} ln   GLSL(ES) {LineCount(result.GlslEs),5} ln");
            }

            // The VS and FS reflect their own texture/sampler lists independently, but a pipeline
            // binds them from one namespace, so the union is what gets emitted. First-seen order
            // wins, matching the C++ implementation. A binding slot is a pipeline-wide index, not a
            // per-stage one, so the merged lists get renumbered rather than forwarding the declared
            // registers.
            var (textures, samplers) = BindingSlots.Assign(
                stages.SelectMany(s => s.Textures).DistinctBy(t => t.Name).ToList(),
                stages.SelectMany(s => s.Samplers).DistinctBy(s => s.Name).ToList());

            var uniformBlocks = stages.SelectMany(s => s.UniformBlocks)
                                      .GroupBy(b => b.Name)
                                      .Select(g => g.First())
                                      .ToList();

            // Uniform naming - `{Shader}{Technique}` - rather than a `{Shader}` special case for the
            // single-technique files, so a later technique added to one of them cannot collide with
            // the bundle already named after the file.
            var programName = shaderName + technique.Name;
            var program = new ProgramReflection(programName, stages, uniformBlocks, textures, samplers);

            bundles.Add(new CompiledBundle(programName, technique.Name, CSharpBundleEmitter.Emit(program, sourcesByStage)));
        }

        return bundles;
    }

    private static int LineCount(string text) =>
        text.AsSpan().Count('\n') + (text.EndsWith('\n') ? 0 : 1);

    /// <summary>
    /// Compiles every `.fx` in <paramref name="directory"/> (not recursive - the shaders are flat)
    /// and writes each bundle into <paramref name="outputDirectory"/>, returning the paths written.
    ///
    /// The whole batch aborts on the first failure rather than writing the bundles that succeeded:
    /// a partial generation would leave the C# compile resolving some bundles and not others, which
    /// reports as missing types rather than as the shader that failed to compile.
    /// </summary>
    public static IReadOnlyList<string> CompileDirectory(
        string directory,
        string outputDirectory,
        Action<string>? log = null)
    {
        var written = new List<string>();
        var errors = new StringBuilder();

        foreach (var path in Directory.EnumerateFiles(directory, "*.fx").OrderBy(p => p, StringComparer.Ordinal))
        {
            IReadOnlyList<CompiledBundle> bundles;
            try
            {
                bundles = CompileFile(path, log);
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                errors.AppendLine(e.Message);
                continue;
            }

            foreach (var bundle in bundles)
            {
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, bundle.ProgramName + ".g.cs");
                // Only rewrite when the content changed: the C# compile downstream is incremental on
                // file timestamps, so touching an unchanged bundle on every build would recompile the
                // world for nothing.
                if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != bundle.Source)
                    File.WriteAllText(outputPath, bundle.Source);
                written.Add(outputPath);
            }
        }

        if (errors.Length > 0)
            throw new InvalidOperationException(errors.ToString().TrimEnd());

        return written;
    }
}
