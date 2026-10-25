using System.Text.RegularExpressions;
using ShaderPoc;

// FX `technique { pass { ... } }` blocks are not HLSL: glslang's HLSL front-end has no
// parser for them. In a real port this is where the build item's VS/FS entry-point
// metadata comes from, so the block is consumed rather than compiled. Brace counting
// rather than a regex, because the block is nested and a lazy match stops at the first
// inner closing brace.
static string StripEffectTechniques(string source)
{
    var kept = new List<string>();
    var inTechnique = false;
    var depth = 0;
    foreach (var line in source.Split('\n'))
    {
        if (!inTechnique)
        {
            if (!Regex.IsMatch(line, @"^\s*technique\b")) { kept.Add(line); continue; }
            // The opening brace may be on this line (`technique X {`) or the next.
            inTechnique = true;
        }

        // Drop the line, and close the block when its braces balance out. `technique X`
        // on a line by itself contributes no braces, so the block stays open until the
        // real `{` arrives -- tracking only the depth would end it immediately.
        depth += line.Count(c => c == '{') - line.Count(c => c == '}');
        if (depth <= 0 && line.Contains('}'))
        {
            inTechnique = false;
            depth = 0;
        }
    }
    return string.Join('\n', kept);
}

var shaderPath = args.Length > 0 ? args[0] : "nfm-world/data/shaders/Sky.fx";
var vsEntry = args.Length > 1 ? args[1] : "VertexShaderFunction";
var fsEntry = args.Length > 2 ? args[2] : "PixelShaderFunction";
var programName = Path.GetFileNameWithoutExtension(shaderPath).Replace("-", "_");
// Writes the per-backend sources next to the bundle, for eyeballing what the emitter got.
var dumpSources = args.Contains("--dump-sources");

var source = StripEffectTechniques(File.ReadAllText(shaderPath).Replace("\r\n", "\n"));
Console.WriteLine($"==> {shaderPath}");

// glslang asks the host to resolve #include; resolve relative to the shader's own
// directory, which is how fxc and the FNA pipeline find `./Mad.fxh`.
var shaderDir = Path.GetDirectoryName(Path.GetFullPath(shaderPath))!;
var includeCache = new Dictionary<string, string?>();
using var spirvCompiler = new SpirvCompiler
{
    IncludeResolver = target =>
    {
        var key = Path.GetFullPath(Path.Combine(shaderDir, target));
        if (!includeCache.TryGetValue(key, out var text))
            includeCache[key] = text = File.Exists(key) ? File.ReadAllText(key).Replace("\r\n", "\n") : null;
        return text;
    },
};
using var collector = new SpirvCrossReflector();

// The vertex semantics live only in the source text - glslang drops them - so they are read
// back out here, in declaration order, to be restored into the emitted HLSL.
var declaredInputs = HlslSemantics.ForEntryPoint(source, vsEntry);
Console.WriteLine($"    vertex semantics: {string.Join(", ", declaredInputs.Select(i => i.Semantic))}");

var defines = new Dictionary<string, string> { ["SM6"] = "1" };
var sourcesByStage = new Dictionary<ShaderStage, StageSources>();
var stages = new List<StageReflection>();

foreach (var (stage, entry) in new[] { (ShaderStage.Vertex, vsEntry), (ShaderStage.Fragment, fsEntry) })
{
    var (spirv, error) = spirvCompiler.Compile(source, shaderPath, stage, entry, defines);
    if (spirv is null)
    {
        Console.Error.WriteLine($"{stage}: HLSL->SPIR-V FAILED\n{error}");
        return 1;
    }

    var result = collector.ReflectAndCross(spirv, stage, entry, declaredInputs);
    sourcesByStage[stage] = new StageSources(spirv, result.Hlsl, result.Msl, result.GlslGl, result.GlslEs);
    stages.Add(result.Reflection);

    if (dumpSources)
    {
        File.WriteAllText($"{programName}.{stage}.hlsl", result.Hlsl);
        File.WriteAllText($"{programName}.{stage}.glsl", result.GlslGl);
        File.WriteAllText($"{programName}.{stage}.es.glsl", result.GlslEs);
        File.WriteAllText($"{programName}.{stage}.metal", result.Msl);
    }

    Console.WriteLine($"    {stage,-9} SPIR-V {spirv.Length,7} B   GLSL {result.GlslGl.Split('\n').Length,5} ln   GLSL(ES) {result.GlslEs.Split('\n').Length,5} ln   MSL {result.Msl.Split('\n').Length,5} ln   HLSL {result.Hlsl.Split('\n').Length,5} ln");
}

// The VS and FS reflect their own texture/sampler lists independently, but a pipeline
// binds them from one namespace, so the union is what gets emitted. First-seen order
// wins, matching the C++ implementation.
// A binding slot is a pipeline-wide index, not a per-stage one, so the merged lists
// get renumbered here rather than forwarding the per-stage declared registers.
var (textures, samplers) = BindingSlots.Assign(
    stages.SelectMany(s => s.Textures).DistinctBy(t => t.Name).ToList(),
    stages.SelectMany(s => s.Samplers).DistinctBy(s => s.Name).ToList());

var uniformBlocks = stages.SelectMany(s => s.UniformBlocks)
                          .GroupBy(b => b.Name)
                          .Select(g => g.First())
                          .ToList();

var program = new ProgramReflection(programName, stages, uniformBlocks, textures, samplers);

var bundle = CSharpBundleEmitter.Emit(program, sourcesByStage);
File.WriteAllText($"{programName}.g.cs", bundle);

Console.WriteLine();
Console.WriteLine($"    uniform blocks: {uniformBlocks.Count}, textures: {textures.Count}, samplers: {samplers.Count}");
Console.WriteLine($"    vertex attributes: {stages.First(s => s.Stage == ShaderStage.Vertex).Inputs.Count}");
Console.WriteLine($"    wrote {programName}.g.cs ({bundle.Split('\n').Length} lines)");
Console.WriteLine();
Console.WriteLine(bundle);
return 0;
