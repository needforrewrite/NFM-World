using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NFMWorld.LuaSourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class LuaVisibleGenerator : IIncrementalGenerator
{
    private const string LuaVisibleAttrName = "nfm_world_library.Lua.LuaVisibleAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            LuaVisibleAttrName,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax
                or StructDeclarationSyntax or InterfaceDeclarationSyntax,
            static (ctx, ct) => ctx
        );

        // Read optional stubs output directory from MSBuild property
        var stubsOutputDir = context.AnalyzerConfigOptionsProvider
            .Select((configOptions, token) =>
            {
                if (configOptions.GlobalOptions.TryGetValue(
                        "build_property.LuaVisibleGenerator_StubsOutputDirectory",
                        out var path))
                    return path;
                return (string?)null;
            });

        var combined = typeProvider.Collect().Combine(stubsOutputDir);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(combined),
            (spc, pair) =>
        {
            var (compilation, (typeContexts, stubsOutDir)) = pair;
            if (typeContexts.IsEmpty) return;

            var references = SymbolReferences.Create(compilation);
            if (references == null) return;

            var generatedTypes = new List<LuaTypeMetadata>();
            var externalTypeSymbols = new Dictionary<string, ITypeSymbol>(); // displayName → symbol
            var seenHints = new HashSet<string>();

            foreach (var attrCtx in typeContexts)
            {
                var symbol = (INamedTypeSymbol)attrCtx.TargetSymbol;
                var typeMeta = new LuaTypeMetadata(symbol, references, compilation);
                if (!typeMeta.IsCandidate) continue;

                generatedTypes.Add(typeMeta);
            }

            // Build set of known LuaVisible types (full names) for StructUserData wrapping decisions
            var luaVisibleFullNames = new HashSet<string>();
            foreach (var typeMeta in generatedTypes)
                luaVisibleFullNames.Add(typeMeta.FullTypeName);

            // Collect external type symbols, excluding types that are themselves [LuaVisible]
            foreach (var typeMeta in generatedTypes)
                CollectExternalTypeSymbols(typeMeta, externalTypeSymbols, luaVisibleFullNames);

            // Collect types from [assembly: AssemblyLuaVisible<T>] attributes
            CollectAssemblyLevelTypes(compilation, externalTypeSymbols, luaVisibleFullNames);

            // Generate ILuaUserData partials for [LuaVisible] types
            foreach (var typeMeta in generatedTypes)
            {
                var typeGen = new LuaBindingTypeGenerator(typeMeta, luaVisibleFullNames);
                var code = typeGen.GenerateCode();
                var hintName = SanitizeHint(typeMeta.FullTypeName);
                var fullHint = $"{hintName}.LuaVisible.g.cs";
                if (seenHints.Add(fullHint))
                    spc.AddSource(fullHint, code);
            }

            // Write LuaLS + TypeScript stubs to disk (if output directory configured)
            if (stubsOutDir != null)
            {
                try
                {
                    System.IO.Directory.CreateDirectory(stubsOutDir);

                    // Stubs for [LuaVisible] types
                    foreach (var typeMeta in generatedTypes)
                    {
                        WriteStubFiles(stubsOutDir, typeMeta.LuaName,
                            new LuaStubsGenerator(typeMeta, compilation).GenerateCode(),
                            new TypeScriptStubsGenerator(typeMeta, compilation).GenerateCode());
                    }

                    // Stubs for external types (StructUserData-wrapped)
                    foreach (var kvp in externalTypeSymbols)
                    {
                        var displayName = kvp.Key;
                        var typeSymbol = kvp.Value;
                        var stubName = SanitizeHint(displayName).Replace("_Array", "Array");
                        // Simple external stub: just the type name annotation
                        var luaStub = GenerateExternalTypeLuaStub(stubName);
                        var tsStub = GenerateExternalTypeTSStub(stubName, typeSymbol);
                        WriteStubFiles(stubsOutDir, stubName, luaStub, tsStub);
                    }
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("LUA001", "Stub generation",
                            $"Failed to write stubs to '{stubsOutDir}': {ex.Message}",
                            "LuaVisibleGenerator", DiagnosticSeverity.Warning, true),
                        Location.None));
                }
            }

            // Generate StructUserData metatables for external types (using resolved ITypeSymbol)
            foreach (var kvp in externalTypeSymbols)
            {
                var displayName = kvp.Key;
                var typeSymbol = kvp.Value;
                var code = GenerateStructUserDataMetatable(displayName, typeSymbol, compilation);
                if (code != null)
                {
                    var hintName = SanitizeHint(displayName);
                    var fullHint = $"StructUserData_Metatable_{hintName}.g.cs";
                    if (seenHints.Add(fullHint))
                        spc.AddSource(fullHint, code);
                }
            }

            // Generate per-assembly registration helper to register all type tables
            if (generatedTypes.Count > 0)
            {
                var registryCode = GenerateTypeRegistry(generatedTypes);
                spc.AddSource("LuaVisibleTypeRegistry.g.cs", registryCode);
            }
        });
    }

    private static void CollectAssemblyLevelTypes(Compilation compilation, Dictionary<string, ITypeSymbol> externalTypes, HashSet<string> luaVisibleFullNames)
    {
        var asmAttr = compilation.Assembly.GetAttributes();
        foreach (var attr in asmAttr)
        {
            if (attr.AttributeClass == null) continue;
            var attrName = attr.AttributeClass.ToDisplayString();
            // Match AssemblyLuaVisibleAttribute<T> (generic) or AssemblyLuaVisibleAttribute (non-generic)
            if (!attrName.StartsWith("nfm_world_library.Lua.AssemblyLuaVisibleAttribute")) continue;

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

            if (typeSymbol == null) continue;

            var displayName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
            if (externalTypes.ContainsKey(displayName)) continue;
            if (luaVisibleFullNames.Contains(displayName)) continue;
            if (!NeedsStructWrap(displayName)) continue;
            externalTypes[displayName] = typeSymbol;
        }
    }

    private static void CollectExternalTypeSymbols(LuaTypeMetadata typeMeta, Dictionary<string, ITypeSymbol> externalTypes, HashSet<string> luaVisibleFullNames)
    {
        var symbol = typeMeta.Symbol;

        // Instance methods — return types and parameters
        foreach (var m in symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared))
        {
            TryAddExternalType(externalTypes, m.ReturnType, luaVisibleFullNames);
            foreach (var p in m.Parameters)
                TryAddExternalType(externalTypes, p.Type, luaVisibleFullNames);
        }

        // Instance properties (no indexers)
        foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared))
        {
            TryAddExternalType(externalTypes, p.Type, luaVisibleFullNames);
        }

        // Instance fields
        foreach (var f in symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared))
        {
            TryAddExternalType(externalTypes, f.Type, luaVisibleFullNames);
        }
    }

    private static void TryAddExternalType(Dictionary<string, ITypeSymbol> dict, ITypeSymbol type, HashSet<string> luaVisibleFullNames)
    {
        var displayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        if (dict.ContainsKey(displayName)) return;
        // Skip types that are themselves [LuaVisible] — they have their own metatable
        if (luaVisibleFullNames.Contains(displayName)) return;
        if (!NeedsStructWrap(displayName)) return;
        dict[displayName] = type;
    }

    private static bool NeedsStructWrap(string t)
    {
        if (t is "int" or "long" or "float" or "double" or "bool" or "string" or "object" or "void"
            or "byte" or "sbyte" or "short" or "ushort" or "uint" or "ulong" or "decimal" or "char")
            return false;
        if (t.EndsWith("?") || t.StartsWith("System.Nullable<")) return false; // nullable types
        if (IsFixedMathBaseType(t)) return false;
        // Only skip true tuple types — check outermost type name (before any '<')
        var baseName = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
        if (baseName.StartsWith("(") || baseName == "System.ValueTuple" || baseName == "System.Tuple")
            return false;
        // Ref structs (Span<T>, ReadOnlySpan<T>) can't be wrapped in StructUserData
        if (baseName == "System.Span" || baseName == "Span"
            || baseName == "System.ReadOnlySpan" || baseName == "ReadOnlySpan")
            return false;
        return true;
    }

    private static string? GenerateStructUserDataMetatable(string typeName, ITypeSymbol? typeSymbol, Compilation compilation)
    {
        // Skip true tuple types at the top level
        var baseName = typeName.Contains('<') ? typeName.Substring(0, typeName.IndexOf('<')) : typeName;
        if (baseName.StartsWith("(") || baseName == "System.ValueTuple" || baseName == "System.Tuple")
            return null;

        // Prefer the already-resolved symbol (handles constructed generics like List<int>)
        // Fall back to metadata name lookup for simple named types
        // Otherwise fall back to reflection-based metatable
        INamedTypeSymbol? symbol = typeSymbol as INamedTypeSymbol
            ?? compilation.GetTypeByMetadataName(typeName);
        if (symbol == null)
        {
            return GenerateFallbackMetatable(typeName);
        }

        var sb = new CodeBuilder();
        var safeName = SanitizeHint(typeName);
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Lua;");
        sb.AppendLine("using Lua.Runtime;");
        sb.AppendLine("using nfm_world_library.Lua;");
        sb.AppendLine();
        sb.AppendLine($"internal static class StructUserData_Metatable_{safeName}");
        sb.AppendLine("{");

        // Collect PUBLIC instance fields and properties only (no private/protected)
        var props = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public).ToArray();
        var fields = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public).ToArray();
        var isArray = symbol.TypeKind == TypeKind.Array;
        var isEnumerable = symbol.AllInterfaces.Any(i => i.ToDisplayString() == "System.Collections.IEnumerable"
            || i.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

        sb.AppendLine("    internal static readonly LuaTable Metatable;");
        sb.AppendLine($"    static StructUserData_Metatable_{safeName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        Metatable = new LuaTable(0, {(isArray ? 3 : 2)});");
        sb.AppendLine();

        // __index
        sb.AppendLine("        Metatable[Metamethods.Index] = new LuaFunction(\"__index\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine("            var key = context.GetArgument(1);");
        sb.AppendLine("            if (key.TryRead<string>(out var sk))");
        sb.AppendLine("            {");
        foreach (var p in props)
        {
            if (p.GetMethod != null)
                sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") return new(context.Return({WrapField($"wrapper.Value.{p.Name}", p.Type)}));");
        }
        foreach (var f in fields)
            sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") return new(context.Return({WrapField($"wrapper.Value.{f.Name}", f.Type)}));");
        sb.AppendLine("                return new(context.Return(LuaValue.Nil));");
        sb.AppendLine("            }");
        if (isArray)
        {
            sb.AppendLine("            if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)");
            sb.AppendLine("            {");
            sb.AppendLine("                var i = (int)n - 1;");
            sb.AppendLine("                if ((uint)i < (uint)((System.Array)(object)wrapper.Value).Length)");
            sb.AppendLine("                    return new(context.Return(LuaValue.FromObject(((System.Array)(object)wrapper.Value).GetValue(i))));");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            return new(context.Return(LuaValue.Nil));");
        sb.AppendLine("        });");
        sb.AppendLine();

        // __newindex
        sb.AppendLine("        Metatable[Metamethods.NewIndex] = new LuaFunction(\"__newindex\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine("            var key = context.GetArgument(1);");
        sb.AppendLine("            var val = context.GetArgument(2);");
        sb.AppendLine("            if (key.TryRead<string>(out var sk))");
        sb.AppendLine("            {");
        var hasWritable = false;
        var isValueType = symbol.IsValueType;
        foreach (var p in props.Where(x => x.SetMethod != null && !x.SetMethod.IsInitOnly))
        {
            hasWritable = true;
            var typeStr = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (isValueType)
            {
                sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") {{ var __tmp = wrapper.Value; __tmp.{p.Name} = val.Read<{typeStr}>(); wrapper.Value = __tmp; return new(context.Return()); }}");
            }
            else
            {
                sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") {{ wrapper.Value.{p.Name} = val.Read<{typeStr}>(); return new(context.Return()); }}");
            }
        }
        foreach (var f in fields.Where(x => !x.IsReadOnly))
        {
            hasWritable = true;
            var typeStr = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (isValueType)
            {
                sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") {{ var __tmp = wrapper.Value; __tmp.{f.Name} = val.Read<{typeStr}>(); wrapper.Value = __tmp; return new(context.Return()); }}");
            }
            else
            {
                sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") {{ wrapper.Value.{f.Name} = val.Read<{typeStr}>(); return new(context.Return()); }}");
            }
        }
        if (hasWritable)
            sb.AppendLine("                throw new LuaRuntimeException(context.State, $\"'{sk}' is read-only or not found.\");");
        else
            sb.AppendLine("                throw new LuaRuntimeException(context.State, $\"'{sk}' not found.\");");
        sb.AppendLine("            }");
        sb.AppendLine("            throw new LuaRuntimeException(context.State, $\"'{key}' not found.\");");
        sb.AppendLine("        });");

        // __len for arrays
        if (isArray)
        {
            sb.AppendLine();
            sb.AppendLine("        Metatable[Metamethods.Len] = new LuaFunction(\"__len\", (context, ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
            sb.AppendLine("            return new(context.Return((double)((System.Array)(object)wrapper.Value).Length));");
            sb.AppendLine("        });");
        }

        // __tostring
        sb.AppendLine();
        sb.AppendLine("        Metatable[Metamethods.ToString] = new LuaFunction(\"__tostring\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine($"            return new(context.Return($\"StructUserData<{typeName}>: {{wrapper.Value}}\"));");
        sb.AppendLine("        });");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string WrapField(string expr, ITypeSymbol type)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (typeName is "int" or "long" or "float" or "double" or "bool" or "string"
            or "byte" or "sbyte" or "short" or "ushort" or "uint" or "ulong" or "decimal")
            return $"({expr})";
        if (IsFixedMathBaseType(typeName))
            return $"({expr})";
        return $"Lua.LuaValue.FromObject({expr})";
    }

    private static string GenerateTypeRegistry(List<LuaTypeMetadata> generatedTypes)
    {
        // Only include types that have a TypeTable (exclude interfaces and types with no constructors/statics)
        var registerable = generatedTypes
            .Where(t => !t.IsInterface)
            .OrderBy(t => t.FullTypeName)
            .ToList();
        if (registerable.Count == 0) return "// No registerable types";

        var sb = new CodeBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Lua;");
        sb.AppendLine();
        sb.AppendLine("public static class LuaVisibleTypeRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(LuaState state)");
        sb.AppendLine("    {");
        foreach (var t in registerable)
        {
            var luaName = t.LuaName;
            sb.AppendLine($"        state.Environment[\"{luaName}\"] = {t.FullTypeName}.TypeTable;");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Check if t is a FixedMath type by base name, not Contains (avoids false positives on generics).</summary>
    private static bool IsFixedMathBaseType(string t)
    {
        var clean = t.EndsWith("?") ? t.Substring(0, t.Length - 1) : t;
        var baseT = clean.Contains('<') ? clean.Substring(0, clean.IndexOf('<')) : clean;
        return baseT is "Fixed64" or "Vector3d" or "f64AngleSingle" or "f64Euler" or "Fixed4x4"
            || baseT.EndsWith(".Fixed64") || baseT.EndsWith(".Vector3d")
            || baseT.EndsWith(".f64AngleSingle") || baseT.EndsWith(".f64Euler")
            || baseT.EndsWith(".Fixed4x4");
    }

    private static void WriteStubFiles(string dir, string name, string luaStub, string tsStub)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"{name}.d.lua"), luaStub);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"{name}.d.ts"), tsStub);
    }

    private static string GenerateExternalTypeLuaStub(string stubName)
    {
        return $"---@class {stubName}Instance\n{stubName}Instance = {{}}\n\n---@class (exact) {stubName}\n";
    }

    private static string GenerateExternalTypeTSStub(string stubName, ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol arr)
        {
            var elemTs = arr.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
            var elemName = elemTs is "System.Int32" or "int" ? "number" :
                           elemTs is "System.Single" or "float" ? "number" :
                           elemTs is "System.Double" or "double" ? "number" :
                           elemTs is "System.String" or "string" ? "string" :
                           elemTs is "System.Boolean" or "bool" ? "boolean" : "any";
            return $"declare class {stubName} {{\n    [index: number]: {elemName};\n    readonly length: number;\n}}\n";
        }
        return $"declare class {stubName} {{\n}}\n";
    }

    private static string SanitizeHint(string name) =>
        name.Replace("global::", "").Replace("[]", "Array")
            .Replace("<", "_").Replace(">", "_")
            .Replace("(", "_").Replace(")", "_")
            .Replace("[", "_").Replace("]", "_").Replace("*", "Ptr_")
            .Replace("?", "_Nullable").Replace(",", "_")
            .Replace(" ", "").Replace(".", "_");

    private static string? GenerateFallbackMetatable(string typeName)
    {
        var safeName = SanitizeHint(typeName);
        var sb = new CodeBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Lua;");
        sb.AppendLine("using Lua.Runtime;");
        sb.AppendLine("using nfm_world_library.Lua;");
        sb.AppendLine();
        sb.AppendLine($"internal static class StructUserData_Metatable_{safeName}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly LuaTable Metatable;");
        sb.AppendLine($"    static StructUserData_Metatable_{safeName}()");
        sb.AppendLine("    {");
        sb.AppendLine("        Metatable = new LuaTable(0, 3);");
        sb.AppendLine();
        // __index
        sb.AppendLine("        Metatable[Metamethods.Index] = new LuaFunction(\"__index\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine("            var key = context.GetArgument(1);");
        sb.AppendLine("            if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                var i = (int)n - 1;");
        sb.AppendLine($"                var obj = (object)wrapper.Value;");
        sb.AppendLine("                if (obj is System.Array arr && (uint)i < (uint)arr.Length)");
        sb.AppendLine("                    return new(context.Return(Lua.LuaValue.FromObject(arr.GetValue(i))));");
        sb.AppendLine("                if (obj is System.Collections.IList list && (uint)i < (uint)list.Count)");
        sb.AppendLine("                    return new(context.Return(Lua.LuaValue.FromObject(list[i])));");
        sb.AppendLine("            }");
        sb.AppendLine("            if (key.TryRead<string>(out var sk))");
        sb.AppendLine("            {");
        sb.AppendLine("                var obj = (object)wrapper.Value;");
        sb.AppendLine("                var fi = obj.GetType().GetField(sk);");
        sb.AppendLine("                if (fi != null) return new(context.Return(Lua.LuaValue.FromObject(fi.GetValue(obj))));");
        sb.AppendLine("                var pi = obj.GetType().GetProperty(sk);");
        sb.AppendLine("                if (pi != null) return new(context.Return(Lua.LuaValue.FromObject(pi.GetValue(obj))));");
        sb.AppendLine("            }");
        sb.AppendLine("            return new(context.Return(LuaValue.Nil));");
        sb.AppendLine("        });");
        sb.AppendLine();
        // __tostring
        sb.AppendLine("        Metatable[Metamethods.ToString] = new LuaFunction(\"__tostring\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine($"            return new(context.Return($\"StructUserData<{typeName}>: {{wrapper.Value}}\"));");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Camel(string n) => n.Length > 0 ? char.ToLowerInvariant(n[0]) + n[1..] : n;
}

internal sealed class SymbolReferences
{
    public INamedTypeSymbol? LuaVisibleAttribute { get; }
    public INamedTypeSymbol? LuaNameAttribute { get; }
    public INamedTypeSymbol? LuaHiddenAttribute { get; }

    private SymbolReferences(Compilation compilation)
    {
        LuaVisibleAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaVisibleAttribute");
        LuaNameAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaNameAttribute");
        LuaHiddenAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaHiddenAttribute");
    }

    public static SymbolReferences? Create(Compilation compilation)
    {
        var r = new SymbolReferences(compilation);
        return r.LuaVisibleAttribute != null ? r : null;
    }
}

internal sealed class CodeBuilder
{
    private readonly System.Text.StringBuilder _sb = new();

    public void AppendLine(string line = "") => _sb.AppendLine(line);

    public void AppendLineIndented(string line, int indent)
    {
        _sb.Append(' ', indent * 4);
        _sb.AppendLine(line);
    }

    public override string ToString() => _sb.ToString();
}
