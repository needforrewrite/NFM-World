// Lua Source Generator
// This program uses reflection to find all types marked with [LuaVisible]
// and generates Lua binding code for them.

using System.Reflection;
using NFMWorld.LuaSourceGenerator;

if (args.Length < 2)
{
    Console.WriteLine("Usage: NFMWorld.LuaSourceGenerator <input-assembly-path> <output-directory-path> [namespace]");
    return;
}

var inputAssemblyPath = args[0];
var outputDirectory = args[1];
var @namespace = args.Length >= 3 ? args[2] : "nfm_world_library.Lua";

// Convert to absolute paths
inputAssemblyPath = Path.GetFullPath(inputAssemblyPath);
outputDirectory = Path.GetFullPath(outputDirectory);

var generator = new LuaBindingGeneratorV2(Assembly.LoadFrom(inputAssemblyPath), @namespace);
generator.GenerateBindings(outputDirectory);
Console.WriteLine($"Generated Lua bindings written to {outputDirectory}");