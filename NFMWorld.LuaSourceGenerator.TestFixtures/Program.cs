using Maxine.Extensions;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;

namespace NFMWorld.LuaSourceGenerator.Test.TestBindings;

public static class Program
{
    /// <summary>
    /// Regenerates the LuaBindings.Generated.cs file for the test project.
    /// This test is marked as Ignore so it doesn't run automatically.
    /// Run it manually when you need to update the generated bindings.
    /// </summary>
    public static void Main()
    {
        var outputPath = GetOutputPath();
        var code = GenerateBindingsCode();

        File.WriteAllText(outputPath, code);

        Console.WriteLine($"Generated bindings to: {outputPath}");
        Console.WriteLine($"Generated {code.Split('\n').Length} lines of code");

        // Test passes if we reach here without exception
    }

    /// <summary>
    /// Generates the bindings code as a string (useful for programmatic access).
    /// </summary>
    public static string GenerateBindingsCode()
    {
        var generator = new LuaBindingGenerator(typeof(SampleClass).Assembly, "NFMWorld.LuaSourceGenerator.Test.TestBindings");
        return generator.Generate();
    }

    /// <summary>
    /// Gets the output path for the generated bindings file.
    /// </summary>
    public static string GetOutputPath()
    {
        // Get the path relative to the test project
        var testDir = ProjectUtils.TryGetProjectDirectory() ?? "";

        return Path.Combine(testDir, "LuaBindings.Generated.cs");
    }
}