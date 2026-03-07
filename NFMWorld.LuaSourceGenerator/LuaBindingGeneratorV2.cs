using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator;

public class LuaBindingGeneratorV2
{
    public string Namespace { get; }
    public Dictionary<LuaVisibleType, DiscoveredKind> Types { get; set; }

    public LuaBindingGeneratorV2(Assembly assembly, string @namespace)
    {
        Namespace = @namespace;
        var mainAssemblyPath = assembly.Location;
        // Try to load and parse .deps.json file
        var depsJsonPath = Path.ChangeExtension(mainAssemblyPath, ".deps.json");
        if (File.Exists(depsJsonPath))
        {
            try
            {
                LoadDependenciesFromDepsJson(depsJsonPath, Path.GetDirectoryName(mainAssemblyPath)!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse {depsJsonPath}: {ex.Message}");
            }
        }

        var types = assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<LuaVisibleAttribute>() is not null
                           || assembly.GetCustomAttributes<AssemblyLuaVisibleAttribute>().Any(e => e.Type == type))
            .Where(LuaVisibleType.IsCandidateType)
            .Select(type => new LuaVisibleType(assembly, type))
            .ToDictionary(t => t.Type, t => t);
        var discoveredKinds = types.ToDictionary(t => t.Key, t => DiscoveredKind.LuaVisible);
        var queue = new Queue<LuaVisibleType>(types.Values);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            
            // Analyze referenced types
            foreach (var method in type.InstanceMethods)
            {
                if (method.IsExtensionMethod && method.DeclaringType != null)
                {
                    Add(new LuaVisibleType(assembly, method.DeclaringType), DiscoveredKind.ExtensionMethodDeclaringType);
                }
                foreach (var param in method.Method.GetParameters())
                {
                    Add(new LuaVisibleType(assembly, param.ParameterType), DiscoveredKind.MethodParameter);
                }
                Add(new LuaVisibleType(assembly, method.ReturnType), DiscoveredKind.MethodReturnType);
            }
            
            foreach (var property in type.InstanceProperties)
            {
                Add(new LuaVisibleType(assembly, property.PropertyType), DiscoveredKind.Property);
            }
            
            foreach (var ev in type.InstanceEvents)
            {
                foreach (var paramType in ev.ParameterTypes)
                {
                    Add(new LuaVisibleType(assembly, paramType), DiscoveredKind.EventParameterType);
                }
                Add(new LuaVisibleType(assembly, ev.ReturnType), DiscoveredKind.EventReturnType);
            }
            
            foreach (var method in type.StaticMethods)
            {
                foreach (var param in method.Method.GetParameters())
                {
                    Add(new LuaVisibleType(assembly, param.ParameterType), DiscoveredKind.MethodParameter);
                }
                Add(new LuaVisibleType(assembly, method.ReturnType), DiscoveredKind.MethodReturnType);
            }
            
            foreach (var property in type.StaticProperties)
            {
                Add(new LuaVisibleType(assembly, property.PropertyType), DiscoveredKind.Property);
            }
            
            foreach (var ev in type.StaticEvents)
            {
                foreach (var paramType in ev.ParameterTypes)
                {
                    Add(new LuaVisibleType(assembly, paramType), DiscoveredKind.EventParameterType);
                }
                Add(new LuaVisibleType(assembly, ev.ReturnType), DiscoveredKind.EventReturnType);
            }
            
            foreach (var constructor in type.Constructors)
            {
                foreach (var param in constructor.Parameters)
                {
                    Add(new LuaVisibleType(assembly, param.ParameterType), DiscoveredKind.ConstructorParameter);
                }
            }
            
            foreach (var op in type.Operators)
            {
                foreach (var param in op.Parameters)
                {
                    Add(new LuaVisibleType(assembly, param.ParameterType), DiscoveredKind.MethodParameter);
                }
                Add(new LuaVisibleType(assembly, op.ReturnType), DiscoveredKind.MethodReturnType);
            }
            
            if (type.BaseType != null)
                Add(new LuaVisibleType(assembly, type.BaseType), DiscoveredKind.BaseType);
            
            foreach (var interfaceType in type.InterfaceTypes)
                Add(new LuaVisibleType(assembly, interfaceType), DiscoveredKind.Interface);

            continue;

            void Add(LuaVisibleType member, DiscoveredKind kind)
            {
                Console.WriteLine($"Discovered type: {member.Type.GetFullTypeName()} (kind: {kind})");

                if (!LuaVisibleType.IsCandidateType(member.Type))
                {
                    return;
                }
                
                if (!discoveredKinds.TryAdd(member.Type, kind))
                {
                    discoveredKinds[member.Type] |= kind;
                }
                
                if (types.TryAdd(member.Type, member))
                {
                    queue.Enqueue(member);
                }
            }
        }

        Types = types.Values.ToDictionary(e => e, e => discoveredKinds[e.Type]);
    }
    
    public void GenerateBindings(string outputPath)
    {
        // Remove .g.cs files from previous runs
        if (Directory.Exists(outputPath))
        {
            var existingFiles = Directory.GetFiles(outputPath, "*.g.cs", SearchOption.TopDirectoryOnly);
            foreach (var file in existingFiles)
            {
                File.Delete(file);
            }
        }
        else
        {
            Directory.CreateDirectory(outputPath);
        }
        
        foreach (var (type, kind) in Types)
        {
            GenerateTypeBindings(type, kind, outputPath);
        }

        GenerateInitializeFile(outputPath);

        GenerateBaseBindings(outputPath);
        
        GenerateLuaStubsFile(outputPath);

        GenerateTypeScriptStubsFile(outputPath);
    }

    private void GenerateLuaStubsFile(string outputPath)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("---@meta");
        sb.AppendLine();
        sb.AppendLine("-- Auto-generated Lua stubs with LuaCATS annotations");
        sb.AppendLine("-- Generated by NFMWorld.LuaSourceGenerator");
        sb.AppendLine();

        foreach (var (typeInfo, kind) in Types)
        {
            var generator = new LuaTypeStubsGenerator(typeInfo, kind);
            var code = generator.GenerateCode();
            sb.AppendLine(code);
            sb.AppendLine();
        }

        const string fileName = $"LuaBindings.Stubs.lua";
        File.WriteAllText(Path.Combine(outputPath, fileName), sb.ToString());
    }
    
    private void GenerateTypeScriptStubsFile(string outputPath)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("// @ts-nocheck");
        sb.AppendLine("// noinspection All");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// TypeScript declarations for TypeScriptToLua");
        sb.AppendLine("// This file is auto-generated by NFMWorld.LuaSourceGenerator.");
        sb.AppendLine();

        foreach (var (typeInfo, kind) in Types)
        {
            var generator = new TypeScriptTypeStubsGenerator(typeInfo, kind);
            var code = generator.GenerateCode();
            sb.AppendLine(code);
            sb.AppendLine();
        }

        const string fileName = $"LuaBindings.Stubs.d.ts";
        File.WriteAllText(Path.Combine(outputPath, fileName), sb.ToString());
    }

    private void GenerateInitializeFile(string outputPath)
    {
        var generator = new LuaBindingInitializeGenerator(Types.Select(
            type => new LuaBindingTypeGenerator(type.Key, type.Value, Namespace)
        ).Where(generator => generator.HasInitializeCode())
        .Select(generator => generator.GenerateInitializeCode()), Namespace);
        var code = generator.GenerateCode();
        var fileName = $"LuaBindings.Initialize.g.cs";
        File.WriteAllText(Path.Combine(outputPath, fileName), code);
    }

    private void GenerateTypeBindings(LuaVisibleType type, DiscoveredKind kind, string outputPath)
    {
        var generator = new LuaBindingTypeGenerator(type, kind, Namespace);
        if (!generator.HasAnythingToGenerate())
        {
            Console.WriteLine($"Does not require generation: {type.Type.GetFullTypeName()}");
            return;
        }

        var code = generator.GenerateCode();
        var fileName = $"{type.Type.GetFullTypeName()
            .Replace('<', '[')
            .Replace('>', ']')
            .Replace("?", "_Nullable")
            .Replace('+', '.')
        }.g.cs";
        File.WriteAllText(Path.Combine(outputPath, fileName), code);
    }
    
    private void GenerateBaseBindings(string outputPath)
    {
        var generator = new LuaBindingBaseGenerator(Types, Namespace);
        var code = generator.GenerateCode();
        var fileName = $"LuaBindings.Base.g.cs";
        File.WriteAllText(Path.Combine(outputPath, fileName), code);
    }

    private static void LoadDependenciesFromDepsJson(string depsJsonPath, string assemblyDirectory)
    {
        var json = File.ReadAllText(depsJsonPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("targets", out var targets))
            return;

        // Get the first target (usually the runtime target)
        foreach (var target in targets.EnumerateObject())
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                if (!library.Value.TryGetProperty("runtime", out var runtime))
                    continue;

                foreach (var runtimeAssembly in runtime.EnumerateObject())
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(runtimeAssembly.Name);
                    var assemblyPath = Path.Combine(assemblyDirectory, runtimeAssembly.Name.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(assemblyPath))
                    {
                        Assembly.LoadFrom(assemblyPath);
                    }
                    else
                    {
                        assemblyPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".nuget", "packages",
                            library.Name.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar),
                            runtimeAssembly.Name.Replace('/', Path.DirectorySeparatorChar)
                        );
                        if (File.Exists(assemblyPath))
                        {
                            Assembly.LoadFrom(assemblyPath);
                        }
                    }
                }
            }
            break; // Only process first target
        }
    }
}