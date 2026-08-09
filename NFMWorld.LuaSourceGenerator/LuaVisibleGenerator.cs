using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NFMWorld.LuaSourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class LuaVisibleGenerator : IIncrementalGenerator
{
    public const string LuaVisibleAttrName = "nfm_world_library.Lua.LuaVisibleAttribute";
    public const string LuaNameAttrName = "nfm_world_library.Lua.LuaNameAttribute";
    public const string LuaHiddenAttrName = "nfm_world_library.Lua.LuaHiddenAttribute";
    public const string MemberLuaVisibleAttrName = "nfm_world_library.Lua.MemberLuaVisibleAttribute";
    public const string InlineArrayAttrName = "System.Runtime.CompilerServices.InlineArrayAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var symbolReferences = context.CompilationProvider
            .Select((compilation, token) => SymbolReferences.Create(compilation))
            .WithTrackingName("SymbolReferences");

        var typeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            LuaVisibleAttrName,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax or InterfaceDeclarationSyntax or EnumDeclarationSyntax,
            static (ctx, ct) => (INamedTypeSymbol)ctx.TargetSymbol)
            .WithTrackingName("LuaVisibleTypes");

        var luaTypeMetadatas = typeProvider.Combine(symbolReferences)
            .Select((pair, ct) =>
            {
                var (symbol, references) = pair;
                if (references == null) return null;
                return new LuaTypeMetadata(symbol, references);
            })
            .Where(tm => tm?.IsCandidate == true)
            .WithTrackingName("LuaTypeMetadatas");
        
        // Read optional stubs output directory from MSBuild property
        var stubsOutputDir = context.AnalyzerConfigOptionsProvider
            .Select((configOptions, token) =>
            {
                if (configOptions.GlobalOptions.TryGetValue(
                        "build_property.LuaVisibleGenerator_StubsOutputDirectory",
                        out var path))
                    return path;
                return null;
            })
            .WithTrackingName("StubsOutputDir");
        
        var typeProvider2 = context.SyntaxProvider.ForAttributeWithMetadataName(
            MemberLuaVisibleAttrName,
            static (node, _) => node is PropertyDeclarationSyntax or FieldDeclarationSyntax or MethodDeclarationSyntax or ConstructorDeclarationSyntax,
            static (ctx, ct) => ctx.TargetSymbol.ContainingType)
            .WithTrackingName("MemberLuaVisibleTypes");

        var luaTypeMetadatas2 = typeProvider2.Combine(symbolReferences)
            .Select((pair, ct) =>
            {
                var (symbol, references) = pair;
                if (references == null) return null;
                return new LuaTypeMetadata(symbol, references);
            })
            .Where(tm => tm?.IsCandidate == true)
            .WithTrackingName("MemberLuaVisibleTypeMetadatas");

        var assemblyLuaVisibleTypes = context.CompilationProvider
            .SelectMany((compilation, ct) => compilation.Assembly.GetAttributes())
            .Select((attr, ct) =>
            {
                if (attr.AttributeClass == null) return null;
                var attrName = attr.AttributeClass.ToDisplayString();
                // Match AssemblyLuaVisibleAttribute<T> (generic) or AssemblyLuaVisibleAttribute (non-generic)
                if (!attrName.StartsWith("nfm_world_library.Lua.AssemblyLuaVisibleAttribute")) return null;

                ITypeSymbol? typeSymbol = null;

                // Generic version: AssemblyLuaVisibleAttribute<T> — type is in TypeArguments
                if (attr.AttributeClass.TypeArguments.Length == 1)
                {
                    typeSymbol = attr.AttributeClass.TypeArguments[0];
                }
                // Non-generic version: AssemblyLuaVisibleAttribute(Type) — type is in constructor args
                else if (attr.ConstructorArguments.Length == 1)
                {
                    typeSymbol = attr.ConstructorArguments[0].Value as ITypeSymbol;
                }

                return typeSymbol as INamedTypeSymbol;
            })
            .Where(ts => ts != null)
            .WithTrackingName("AssemblyLuaVisibleTypes");
        
        var assemblyLuaTypeMetadatas = assemblyLuaVisibleTypes.Combine(symbolReferences)
            .Select((pair, ct) =>
            {
                var (symbol, references) = pair;
                if (references == null) return null;
                return new LuaTypeMetadata(symbol!, references);
            })
            .Where(tm => tm?.IsCandidate == true)
            .WithTrackingName("AssemblyLuaTypeMetadatas");

        var combined = luaTypeMetadatas.Collect().Combine(luaTypeMetadatas2.Collect()).Combine(assemblyLuaTypeMetadatas.Collect()).Combine(stubsOutputDir);
        
        context.RegisterSourceOutput(
            combined,
            (spc, pairs) =>
            {
                var (((visible, memberVisible), assemblyVisible), stubsOutputDir) = pairs;

                var list = new Dictionary<string, LuaTypeMetadata>();
                foreach (var meta in visible)
                {
                    if (!list.ContainsKey(meta!.FullTypeName))
                        list[meta.FullTypeName] = meta;
                }
                foreach (var meta in assemblyVisible)
                {
                    if (!list.ContainsKey(meta!.FullTypeName))
                        list[meta.FullTypeName] = meta;
                }
                foreach (var meta in memberVisible)
                {
                    if (!list.ContainsKey(meta!.FullTypeName))
                        list[meta.FullTypeName] = meta;
                }

                var stubTypes = new Dictionary<string, BaseLuaTypeMetadata>();
                foreach (var type in list.Values)
                {
                    foreach (var field in type.InstanceFields)
                    {
                        if (field.FieldType.IsCandidate && !list.ContainsKey(field.FieldType.FullTypeName) && !stubTypes.ContainsKey(field.FieldType.FullTypeName))
                        {
                            stubTypes[field.FieldType.FullTypeName] = field.FieldType;
                        }
                    }

                    foreach (var field in type.StaticFields)
                    {
                        if (field.FieldType.IsCandidate && !list.ContainsKey(field.FieldType.FullTypeName) && !stubTypes.ContainsKey(field.FieldType.FullTypeName))
                        {
                            stubTypes[field.FieldType.FullTypeName] = field.FieldType;
                        }
                    }
                    
                    foreach (var prop in type.InstanceProperties)
                    {
                        if (prop.PropertyType.IsCandidate && !list.ContainsKey(prop.PropertyType.FullTypeName) && !stubTypes.ContainsKey(prop.PropertyType.FullTypeName))
                        {
                            stubTypes[prop.PropertyType.FullTypeName] = prop.PropertyType;
                        }
                    }
                    
                    foreach (var prop in type.StaticProperties)
                    {
                        if (prop.PropertyType.IsCandidate && !list.ContainsKey(prop.PropertyType.FullTypeName) && !stubTypes.ContainsKey(prop.PropertyType.FullTypeName))
                        {
                            stubTypes[prop.PropertyType.FullTypeName] = prop.PropertyType;
                        }
                    }
                    
                    foreach (var method in type.InstanceMethods)
                    {
                        foreach (var param in method.Parameters)
                        {
                            if (param.Type.IsCandidate && !list.ContainsKey(param.Type.FullTypeName) && !stubTypes.ContainsKey(param.Type.FullTypeName))
                            {
                                stubTypes[param.Type.FullTypeName] = param.Type;
                            }
                        }
                        if (method.ReturnType.IsCandidate && !list.ContainsKey(method.ReturnType!.FullTypeName) && !stubTypes.ContainsKey(method.ReturnType.FullTypeName))
                        {
                            stubTypes[method.ReturnType.FullTypeName] = method.ReturnType;
                        }
                    }
                    
                    foreach (var method in type.StaticMethods)
                    {
                        foreach (var param in method.Parameters)
                        {
                            if (param.Type.IsCandidate && !list.ContainsKey(param.Type.FullTypeName) && !stubTypes.ContainsKey(param.Type.FullTypeName))
                            {
                                stubTypes[param.Type.FullTypeName] = param.Type;
                            }
                        }
                        if (method.ReturnType.IsCandidate && !list.ContainsKey(method.ReturnType!.FullTypeName) && !stubTypes.ContainsKey(method.ReturnType.FullTypeName))
                        {
                            stubTypes[method.ReturnType.FullTypeName] = method.ReturnType;
                        }
                    }
                }
                
                foreach (var type in list.Values)
                {
                    if (type.IsEnum)
                    {
                        var generator = new LuaBindingEnumTypeGenerator(type);
                        var code = generator.GenerateCode();
                        spc.AddSource($"{type.SanitizedTypeName}.cs", code);
                    }
                    else
                    {
                        var generator = new LuaBindingTypeGenerator(type);
                        var code = generator.GenerateCode();
                        spc.AddSource($"{type.SanitizedTypeName}.cs", code);
                    }
                }
                
                foreach (var type in stubTypes.Values)
                {
                    var generator = new LuaBindingStubTypeGenerator(type);
                    var code = generator.GenerateCode();
                    spc.AddSource($"{type.SanitizedTypeName}.cs", code);
                }
            });
    }
}

internal sealed class SymbolReferences
{
    public INamedTypeSymbol? LuaVisibleAttribute { get; }
    public INamedTypeSymbol? LuaNameAttribute { get; }
    public INamedTypeSymbol? LuaHiddenAttribute { get; }
    public INamedTypeSymbol? MemberLuaVisibleAttribute { get; }
    public INamedTypeSymbol? InlineArrayAttribute { get; }
    public INamedTypeSymbol? ILuaUserData { get; }
    public INamedTypeSymbol? Fixed64Vector3 { get; }
    public INamedTypeSymbol? Fixed64 { get; }
    public INamedTypeSymbol? Fixed64AngleSingle { get; }
    public INamedTypeSymbol? Fixed64Euler { get; }

    private SymbolReferences(Compilation compilation)
    {
        LuaVisibleAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaVisibleAttribute");
        LuaNameAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaNameAttribute");
        LuaHiddenAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaHiddenAttribute");
        MemberLuaVisibleAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.MemberLuaVisibleAttribute");
        InlineArrayAttribute = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.InlineArrayAttribute");
        ILuaUserData = compilation.GetTypeByMetadataName("Lua.ILuaUserData");
        Fixed64 = compilation.GetTypeByMetadataName("FixedMathSharp.Fixed64");
        Fixed64Vector3 = compilation.GetTypeByMetadataName("FixedMathSharp.Vector3d");
        Fixed64AngleSingle = compilation.GetTypeByMetadataName("NFMWorldLibrary.FixedMath.f64AngleSingle");
        Fixed64Euler = compilation.GetTypeByMetadataName("NFMWorldLibrary.FixedMath.f64Euler");
    }

    public static SymbolReferences? Create(Compilation compilation)
    {
        var r = new SymbolReferences(compilation);
        return r.LuaVisibleAttribute != null ? r : null;
    }
}
