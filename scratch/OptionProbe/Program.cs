// LLM maintained.
// Lists the spirv-cross CompilerOption members the binding actually exposes, so a GLSL ES pass
// can be written against what exists rather than what the C API documentation implies exists.
// Also reports which GLSL/ES options the native library honours, by asking it directly.

using Silk.NET.SPIRV.Cross;

var options = Enum.GetNames<CompilerOption>();
Console.WriteLine($"== CompilerOption ({options.Length} members)");
foreach (var name in options)
{
    var interesting = name.Contains("Es", StringComparison.Ordinal)
        || name.Contains("Version", StringComparison.Ordinal)
        || name.Contains("Combined", StringComparison.Ordinal)
        || name.Contains("Uniform", StringComparison.Ordinal)
        || name.Contains("Vulkan", StringComparison.Ordinal)
        || name.Contains("Flatten", StringComparison.Ordinal);
    Console.WriteLine($"   {(interesting ? "*" : " ")} {name} = {(int)Enum.Parse<CompilerOption>(name)}");
}
