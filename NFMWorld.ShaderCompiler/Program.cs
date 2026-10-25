// LLM maintained.
//
// The command-line host over the compiler library.
//
// The build does not use this - the MSBuild task calls ShaderBuild directly - but it stays as the
// way to compile a shader by hand while debugging one: the build log a task emits is one line per
// stage at best, and this prints the whole bundle.
//
//   dotnet run --project NFMWorld.ShaderCompiler -- <shader.fx|directory> [output-dir]
//
// Files are written to the output directory when one is given, and printed to stdout otherwise.
using NFMWorld.ShaderCompiler;

var shaderPath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
if (shaderPath is null)
{
    Console.Error.WriteLine("usage: NFMWorld.ShaderCompiler <shader.fx|directory> [output-dir]");
    return 1;
}

var outputDir = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

try
{
    var files = Directory.Exists(shaderPath)
        ? Directory.EnumerateFiles(shaderPath, "*.fx").OrderBy(p => p, StringComparer.Ordinal).ToList()
        : [shaderPath];

    foreach (var file in files)
    {
        foreach (var bundle in ShaderBuild.CompileFile(file, Console.WriteLine))
        {
            if (outputDir is not null)
            {
                Directory.CreateDirectory(outputDir);
                var path = Path.Combine(outputDir, bundle.ProgramName + ".g.cs");
                File.WriteAllText(path, bundle.Source);
                Console.WriteLine($"    wrote {path} ({bundle.Source.Split('\n').Length} lines)");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(bundle.Source);
            }
        }
    }
}
catch (Exception e) when (e is InvalidOperationException or IOException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

return 0;
