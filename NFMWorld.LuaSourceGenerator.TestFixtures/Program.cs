using Maxine.Extensions;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;

namespace NFMWorld.LuaSourceGenerator.Test.TestBindings;

public static class Program
{
    /// <summary>
    /// Regenerates the Lua bindings files for the test project.
    /// Generates multiple files (one per type) in the current directory.
    /// Run it manually when you need to update the generated bindings.
    /// </summary>
    public static void Main()
    {
        var outputDir = GetOutputDirectory();
        GenerateBindingsFiles(outputDir);

        Console.WriteLine($"Generated bindings to: {outputDir}");

        // Count generated files
        var files = Directory.GetFiles(outputDir, "*.g.cs");
        Console.WriteLine($"Generated {files.Length} files");

        // Test passes if we reach here without exception
    }

    /// <summary>
    /// Generates the bindings files to the specified directory.
    /// </summary>
    public static void GenerateBindingsFiles(string outputDirectory)
    {
        var generator = new LuaBindingGenerator(typeof(SampleClass).Assembly, "NFMWorld.LuaSourceGenerator.Test.TestBindings");
        generator.GenerateToFiles(outputDirectory);
    }

    /// <summary>
    /// Gets the output directory for the generated bindings files.
    /// </summary>
    public static string GetOutputDirectory()
    {
        // Generate directly in the test project directory
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var binDir = Path.GetDirectoryName(assemblyLocation)!;
        
        // Navigate from bin\Debug\net10.0 to project root
        var projectRoot = Path.GetFullPath(Path.Combine(binDir, "..", "..", ".."));
        
        // Go to the Test project which is sibling to TestFixtures
        var testProjectDir = Path.Combine(projectRoot, "..", "NFMWorld.LuaSourceGenerator.Test");
        
        return Path.GetFullPath(testProjectDir);
    }
}