using System;
using System.Collections.Generic;
using Mono.Cecil;
using XamlX.TypeSystem;

namespace XamlX.CSharp;

/// <summary>
/// A type system that uses Cecil to read assemblies (including from byte arrays)
/// and emits C# source code instead of IL.
/// </summary>
public sealed class CSharpCodeTypeSystem : IDisposable
{
    private readonly IXamlTypeSystem _typeSystem;
    private readonly IAssemblyResolver _assemblyResolver;
    private readonly List<CSharpGeneratedType> _generatedTypes = new();

    /// <summary>
    /// Gets all generated C# types.
    /// </summary>
    public IReadOnlyList<CSharpGeneratedType> GeneratedTypes => _generatedTypes;

    /// <summary>
    /// Creates a CSharpCodeTypeSystem from file paths, same as CecilTypeSystem.
    /// </summary>
    public CSharpCodeTypeSystem(IEnumerable<string> paths, string? targetPath = null)
    {
        var cecilTypeSystem = new CecilTypeSystem(paths, targetPath);
        _typeSystem = cecilTypeSystem;
        _assemblyResolver = cecilTypeSystem;
    }

    /// <summary>
    /// Creates a CSharpCodeTypeSystem from an existing CecilTypeSystem.
    /// </summary>
    public CSharpCodeTypeSystem(IXamlTypeSystem typeSystem, IAssemblyResolver assemblyResolver)
    {
        _typeSystem = typeSystem;
        _assemblyResolver = assemblyResolver;
    }

    // /// <summary>
    // /// Loads an assembly from a byte array and registers it with the type system.
    // /// </summary>
    // public IXamlAssembly LoadAssemblyFromBytes(byte[] assemblyBytes)
    // {
    //     using var stream = new MemoryStream(assemblyBytes);
    //     var asm = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters(ReadingMode.Immediate)
    //     {
    //         InMemory = true,
    //         AssemblyResolver = (IAssemblyResolver)_typeSystem,
    //     });
    //     return _typeSystem.RegisterAssembly(asm);
    // }

    /// <summary>
    /// Creates a C#-emitting type builder for generating source code.
    /// </summary>
    public CSharpTypeBuilder CreateTypeBuilder(string namespaceName, string typeName,
        IXamlType? baseType = null, XamlVisibility visibility = XamlVisibility.Public)
    {
        var builder = new CSharpTypeBuilder(_typeSystem, namespaceName, typeName, baseType, visibility);
        _generatedTypes.Add(new CSharpGeneratedType(builder));
        return builder;
    }

    /// <summary>
    /// Gets all generated C# source code as a dictionary of filename → content.
    /// </summary>
    public Dictionary<string, string> GetGeneratedSources()
    {
        var result = new Dictionary<string, string>();
        foreach (var gen in _generatedTypes)
        {
            var fileName = gen.Builder.FullName.Replace('.', '_') + ".g.cs";
            result[fileName] = gen.Builder.GenerateSource();
        }
        return result;
    }

    public void Dispose()
    {
        // Don't dispose the cecil type system if it was passed in externally
    }
}

public class CSharpGeneratedType(CSharpTypeBuilder builder)
{
    public CSharpTypeBuilder Builder { get; } = builder;
}