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
            var externalTypeSymbols = new Dictionary<string, ITypeSymbol>(); // Metatable (MemberLuaVisible)
            var stubTypeSymbols = new Dictionary<string, ITypeSymbol>();    // Stubs only (all referenced)
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
            var luaVisibleEnumFullNames = new HashSet<string>();
            foreach (var typeMeta in generatedTypes)
            {
                luaVisibleFullNames.Add(typeMeta.FullTypeName);
                if (typeMeta.Symbol.TypeKind == TypeKind.Enum)
                    luaVisibleEnumFullNames.Add(typeMeta.FullTypeName);
            }

            // Collect external type symbols from [MemberLuaVisible] members + stubs from all members
            foreach (var typeMeta in generatedTypes)
                CollectExternalTypeSymbols(typeMeta, externalTypeSymbols, stubTypeSymbols, luaVisibleFullNames, references);

            // Collect types from [assembly: AssemblyLuaVisible<T>] attributes
            CollectAssemblyLevelTypes(compilation, externalTypeSymbols, luaVisibleFullNames);

            // Generate ILuaUserData partials for [LuaVisible] types
            foreach (var typeMeta in generatedTypes)
            {
                if (typeMeta.Symbol.TypeKind == TypeKind.Enum)
                {
                    // Enums can't implement interfaces — generate StructUserData metatable + TypeTable instead
                    var enumCode = GenerateEnumMetatable(typeMeta);
                    if (enumCode != null)
                    {
                        var enumHint = $"StructUserData_Metatable_{SanitizeHint(typeMeta.FullTypeName)}.g.cs";
                        if (seenHints.Add(enumHint))
                            spc.AddSource(enumHint, enumCode);
                    }
                    continue;
                }

                var typeGen = new LuaBindingTypeGenerator(typeMeta, luaVisibleFullNames, luaVisibleEnumFullNames);
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

                    // Build a mapping from full type name → LuaName for resolving references
                    var luaVisibleNameMap = new Dictionary<string, string>();
                    foreach (var typeMeta in generatedTypes)
                        luaVisibleNameMap[typeMeta.FullTypeName] = typeMeta.LuaName;

                    // Stubs for [LuaVisible] types
                    foreach (var typeMeta in generatedTypes)
                    {
                        WriteStubFiles(stubsOutDir, typeMeta.LuaName,
                            new LuaStubsGenerator(typeMeta, compilation, luaVisibleNameMap).GenerateCode(),
                            new TypeScriptStubsGenerator(typeMeta, compilation, luaVisibleNameMap).GenerateCode());
                    }

                    // Stubs for external types (StructUserData-wrapped)
                    foreach (var kvp in externalTypeSymbols)
                    {
                        var displayName = kvp.Key;
                        var typeSymbol = kvp.Value;
                        var stubName = SanitizeHint(displayName).Replace("_Array", "Array");
                        var luaStub = GenerateExternalTypeLuaStub(stubName, typeSymbol, compilation, luaVisibleNameMap);
                        var tsStub = GenerateExternalTypeTSStub(stubName, typeSymbol);
                        WriteStubFiles(stubsOutDir, stubName, luaStub, tsStub);
                    }

                    // Opaque stubs for all other referenced types (no metatable, just type identity)
                    foreach (var kvp in stubTypeSymbols)
                    {
                        if (externalTypeSymbols.ContainsKey(kvp.Key)) continue; // already emitted above
                        var stubName = SanitizeHint(kvp.Key).Replace("_Array", "Array");
                        var luaStub = $"---@class {stubName}Instance\n{stubName}Instance = {{}}\n\n---@class (exact) {stubName}\n";
                        var tsStub = $"declare class {stubName} {{\n}}\n";
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
            var structUserDataMetatableClassNames = new List<(string ClassName, string TypeName)>();
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
                    {
                        spc.AddSource(fullHint, code);
                        structUserDataMetatableClassNames.Add(($"StructUserData_Metatable_{hintName}", displayName));
                    }
                }
            }

            // Register enum StructUserData metatables too
            foreach (var typeMeta in generatedTypes.Where(t => t.Symbol.TypeKind == TypeKind.Enum))
            {
                structUserDataMetatableClassNames.Add(($"StructUserData_Metatable_{SanitizeHint(typeMeta.FullTypeName)}", typeMeta.FullTypeName));
            }

            // Generate per-assembly registration helper to register all type tables
            if (generatedTypes.Count > 0)
            {
                var registryCode = GenerateTypeRegistry(generatedTypes);
                spc.AddSource("LuaVisibleTypeRegistry.g.cs", registryCode);
            }

            // Generate module initializer to register StructUserData metatables into the global registry
            if (structUserDataMetatableClassNames.Count > 0)
            {
                var initCode = GenerateStructUserDataMetatableInitializer(structUserDataMetatableClassNames);
                spc.AddSource("StructUserDataMetatableInitializer.g.cs", initCode);
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

    private static void CollectExternalTypeSymbols(LuaTypeMetadata typeMeta, Dictionary<string, ITypeSymbol> externalTypes, Dictionary<string, ITypeSymbol> stubTypes, HashSet<string> luaVisibleFullNames, SymbolReferences references)
    {
        var symbol = typeMeta.Symbol;
        var memberLuaVisibleAttr = references.MemberLuaVisibleAttribute;

        // Collect MemberLuaVisible types for StructUserData metatables
        CollectMemberLuaVisibleTypes(symbol, externalTypes, luaVisibleFullNames, memberLuaVisibleAttr);

        // Collect ALL referenced types for opaque stubs
        CollectStubTypes(symbol, stubTypes, luaVisibleFullNames);

        // For interfaces, also add base interfaces themselves for stubs
        if (symbol.TypeKind == TypeKind.Interface)
        {
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            WalkInterfaces(symbol, visited);
        }

        void WalkInterfaces(INamedTypeSymbol iface, HashSet<INamedTypeSymbol> visited)
        {
            foreach (var baseIface in iface.Interfaces)
            {
                if (!visited.Add(baseIface)) continue;
                TryAddExternalType(externalTypes, baseIface, luaVisibleFullNames);
                TryAddStubType(stubTypes, baseIface, luaVisibleFullNames);
                CollectMemberLuaVisibleTypes(baseIface, externalTypes, luaVisibleFullNames, memberLuaVisibleAttr);
                CollectStubTypes(baseIface, stubTypes, luaVisibleFullNames);
                WalkInterfaces(baseIface, visited);
            }
        }
    }

    /// <summary>Collect types for opaque stubs from all public members, base types, and interfaces.</summary>
    private static void CollectStubTypes(INamedTypeSymbol symbol, Dictionary<string, ITypeSymbol> stubTypes, HashSet<string> luaVisibleFullNames)
    {
        // Base type (if not Object/ValueType)
        var bt = symbol.BaseType;
        if (bt != null && bt.SpecialType != SpecialType.System_Object && bt.SpecialType != SpecialType.System_ValueType)
            TryAddStubType(stubTypes, bt, luaVisibleFullNames);

        // Implemented interfaces
        foreach (var iface in symbol.AllInterfaces)
            TryAddStubType(stubTypes, iface, luaVisibleFullNames);

        foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public))
        {
            TryAddStubType(stubTypes, p.Type, luaVisibleFullNames);
        }

        foreach (var f in symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public))
        {
            TryAddStubType(stubTypes, f.Type, luaVisibleFullNames);
        }

        foreach (var m in symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public))
        {
            TryAddStubType(stubTypes, m.ReturnType, luaVisibleFullNames);
            foreach (var param in m.Parameters)
                TryAddStubType(stubTypes, param.Type, luaVisibleFullNames);
        }
    }

    private static void CollectMemberLuaVisibleTypes(INamedTypeSymbol symbol, Dictionary<string, ITypeSymbol> externalTypes, HashSet<string> luaVisibleFullNames, INamedTypeSymbol? memberLuaVisibleAttr)
    {
        if (memberLuaVisibleAttr == null) return;

        // Properties with [MemberLuaVisible]
        foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsImplicitlyDeclared && HasAttr(p, memberLuaVisibleAttr)))
        {
            TryAddExternalType(externalTypes, p.Type, luaVisibleFullNames);
        }

        // Fields with [MemberLuaVisible]
        foreach (var f in symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && HasAttr(f, memberLuaVisibleAttr)))
        {
            TryAddExternalType(externalTypes, f.Type, luaVisibleFullNames);
        }

        // Methods with [MemberLuaVisible] — add return type and parameter types
        foreach (var m in symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared && HasAttr(m, memberLuaVisibleAttr)))
        {
            TryAddExternalType(externalTypes, m.ReturnType, luaVisibleFullNames);
            foreach (var param in m.Parameters)
                TryAddExternalType(externalTypes, param.Type, luaVisibleFullNames);
        }

        // Methods with [MemberLuaVisible] on return value
        foreach (var m in symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared
                && m.GetReturnTypeAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, memberLuaVisibleAttr))))
        {
            TryAddExternalType(externalTypes, m.ReturnType, luaVisibleFullNames);
        }
    }

    private static void TryAddExternalType(Dictionary<string, ITypeSymbol> dict, ITypeSymbol type, HashSet<string> luaVisibleFullNames)
    {
        var displayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        if (dict.ContainsKey(displayName)) return;
        // Skip types that are themselves [LuaVisible] — they have their own metatable
        if (luaVisibleFullNames.Contains(displayName)) return;
        if (!NeedsStructWrap(displayName)) return;
        // Skip 1D arrays — they're stubbed inline as { [integer]: T }
        if (type is IArrayTypeSymbol arr && arr.Rank == 1) return;
        // Skip known Lua-CSharp base types — mapped to native Lua types in stubs
        if (displayName is "Lua.LuaTable" or "Lua.LuaFunction" or "Lua.LuaValue" or "Lua.ILuaUserData") return;
        dict[displayName] = type;
    }

    /// <summary>Add a type to the stub-only dictionary. Skips primitives, LuaVisible, 1D arrays, and Lua base types.</summary>
    private static void TryAddStubType(Dictionary<string, ITypeSymbol> dict, ITypeSymbol type, HashSet<string> luaVisibleFullNames)
    {
        var displayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        if (dict.ContainsKey(displayName)) return;
        if (luaVisibleFullNames.Contains(displayName)) return;
        if (!NeedsStructWrap(displayName)) return;
        if (type is IArrayTypeSymbol arr && arr.Rank == 1) return;
        if (displayName is "Lua.LuaTable" or "Lua.LuaFunction" or "Lua.LuaValue" or "Lua.ILuaUserData") return;
        dict[displayName] = type;
    }

    private static bool HasAttr(ISymbol s, INamedTypeSymbol? attr)
        => attr != null && s.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr));

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
        INamedTypeSymbol? symbol = typeSymbol as INamedTypeSymbol
            ?? compilation.GetTypeByMetadataName(typeName);
        if (symbol == null)
            return GenerateFallbackMetatable(typeName);

        var sb = new CodeBuilder();
        var safeName = SanitizeHint(typeName);
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Lua;");
        sb.AppendLine("using Lua.Runtime;");
        sb.AppendLine("using nfm_world_library.Lua;");
        sb.AppendLine();
        sb.AppendLine($"internal static class StructUserData_Metatable_{safeName}");
        sb.AppendLine("{");

        // Named properties and fields (exclude indexers — handled separately)
        var props = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public).ToArray();
        var fields = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public).ToArray();

        // Array detection
        var isArray = symbol.TypeKind == TypeKind.Array;
        var arrayElemTypeStr = isArray ? ((IArrayTypeSymbol)symbol).ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "") : null;

        // Integer indexer detection (this[int]) — for List<T>, IReadOnlyList<T>, etc.
        var intIndexers = symbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => p.Parameters.Length == 1 && p.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
            .ToArray();
        var hasIntIndexer = intIndexers.Length > 0;
        var indexerRetTypeStr = hasIntIndexer ? intIndexers[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "") : null;
        var hasIndexerSet = hasIntIndexer && intIndexers[0].SetMethod != null;
        // Look for Count or Length property for list-like types to use for bounds + __len
        string? countPropName = null;
        if (hasIntIndexer)
        {
            countPropName = props.FirstOrDefault(p => (p.Name == "Count" || p.Name == "Length") && p.GetMethod != null)?.Name;
            if (countPropName == null)
            {
                // For IReadOnlyList<T>, Count might be on the base interface
                foreach (var iface in symbol.AllInterfaces)
                {
                    var cp = iface.GetMembers().OfType<IPropertySymbol>()
                        .FirstOrDefault(p => p.Name is "Count" or "Length" && p.GetMethod != null);
                    if (cp != null) { countPropName = cp.Name; break; }
                }
            }
        }
        var hasListLen = countPropName != null;

        var hasIntAccess = isArray || hasIntIndexer;
        // Slots: __index, __tostring, + __newindex (if int access or writable props), + __len (if array or list-like)
        var slotCount = 2 + (hasIntAccess || props.Any(p => p.SetMethod != null && !p.SetMethod.IsInitOnly) || fields.Any(f => !f.IsReadOnly) ? 1 : 0) + ((isArray || hasListLen) ? 1 : 0);

        sb.AppendLine("    internal static readonly LuaTable Metatable;");
        sb.AppendLine($"    static StructUserData_Metatable_{safeName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        Metatable = new LuaTable(0, {slotCount});");
        sb.AppendLine();

        // ====================================================================
        // __index
        // ====================================================================
        sb.AppendLine("        Metatable[Metamethods.Index] = new LuaFunction(\"__index\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine("            var key = context.GetArgument(1);");

        // String key dispatch
        if (props.Length > 0 || fields.Length > 0)
        {
            sb.AppendLine("            if (key.TryRead<string>(out var sk))");
            sb.AppendLine("            {");
            foreach (var p in props.Where(p => p.GetMethod != null))
                sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") return new(context.Return({MarshalProperty($"wrapper.Value.{p.Name}", p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""))}));");
            foreach (var f in fields)
                sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") return new(context.Return({MarshalProperty($"wrapper.Value.{f.Name}", f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""))}));");
            sb.AppendLine("                return new(context.Return(LuaValue.Nil));");
            sb.AppendLine("            }");
        }

        // Integer index dispatch
        if (hasIntAccess)
        {
            sb.AppendLine("            if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)");
            sb.AppendLine("            {");
            sb.AppendLine("                var i = (int)n - 1;");

            if (isArray)
            {
                sb.AppendLine("                if ((uint)i < (uint)wrapper.Value.Length)");
                sb.AppendLine($"                    return new(context.Return({MarshalElement("wrapper.Value[i]", arrayElemTypeStr!)}));");
            }
            else if (hasIntIndexer)
            {
                if (countPropName != null)
                {
                    sb.AppendLine($"                if ((uint)i < (uint)wrapper.Value.{countPropName})");
                    sb.AppendLine($"                    return new(context.Return({MarshalElement("wrapper.Value[i]", indexerRetTypeStr!)}));");
                }
                else
                {
                    sb.AppendLine($"                return new(context.Return({MarshalElement("wrapper.Value[i]", indexerRetTypeStr!)}));");
                }
            }

            sb.AppendLine("            }");
        }

        sb.AppendLine("            return new(context.Return(LuaValue.Nil));");
        sb.AppendLine("        });");
        sb.AppendLine();

        // ====================================================================
        // __newindex
        // ====================================================================
        var hasWritableStrings = props.Any(p => p.SetMethod != null && !p.SetMethod.IsInitOnly)
                               || fields.Any(f => !f.IsReadOnly);
        var hasWritableInts = isArray || (hasIndexerSet && hasIntIndexer);
        if (hasWritableStrings || hasWritableInts)
        {
            sb.AppendLine("        Metatable[Metamethods.NewIndex] = new LuaFunction(\"__newindex\", (context, ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
            sb.AppendLine("            var key = context.GetArgument(1);");
            sb.AppendLine("            var val = context.GetArgument(2);");

            if (hasWritableStrings)
            {
                sb.AppendLine("            if (key.TryRead<string>(out var sk))");
                sb.AppendLine("            {");
                var isValueType = symbol.IsValueType;
                foreach (var p in props.Where(x => x.SetMethod != null && !x.SetMethod.IsInitOnly))
                {
                    var ts = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                    if (isValueType)
                        sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") {{ var __tmp = wrapper.Value; __tmp.{p.Name} = {UnmarshalRead("val", ts)}; wrapper.Value = __tmp; return new(context.Return()); }}");
                    else
                        sb.AppendLine($"                if (sk == \"{Camel(p.Name)}\") {{ wrapper.Value.{p.Name} = {UnmarshalRead("val", ts)}; return new(context.Return()); }}");
                }
                foreach (var f in fields.Where(x => !x.IsReadOnly))
                {
                    var ts = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                    if (isValueType)
                        sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") {{ var __tmp = wrapper.Value; __tmp.{f.Name} = {UnmarshalRead("val", ts)}; wrapper.Value = __tmp; return new(context.Return()); }}");
                    else
                        sb.AppendLine($"                if (sk == \"{Camel(f.Name)}\") {{ wrapper.Value.{f.Name} = {UnmarshalRead("val", ts)}; return new(context.Return()); }}");
                }
                sb.AppendLine("                throw new LuaRuntimeException(context.State, $\"'{sk}' is read-only or not found.\");");
                sb.AppendLine("            }");
            }

            // Integer __newindex
            if (isArray)
            {
                sb.AppendLine("            if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)");
                sb.AppendLine("            {");
                sb.AppendLine("                var i = (int)n - 1;");
                sb.AppendLine("                if ((uint)i < (uint)wrapper.Value.Length)");
                sb.AppendLine("                {");
                if (symbol.IsValueType)
                {
                    sb.AppendLine($"                    var __tmp = wrapper.Value; __tmp[i] = {UnmarshalRead("val", arrayElemTypeStr!)}; wrapper.Value = __tmp;");
                }
                else
                {
                    sb.AppendLine($"                    wrapper.Value[i] = {UnmarshalRead("val", arrayElemTypeStr!)};");
                }
                sb.AppendLine("                    return new(context.Return());");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
            }
            else if (hasIndexerSet && hasIntIndexer)
            {
                sb.AppendLine("            if (key.TryRead<double>(out var n) && double.IsFinite(n) && n >= 1.0 && n <= int.MaxValue)");
                sb.AppendLine("            {");
                sb.AppendLine("                var i = (int)n - 1;");
                var elemType = indexerRetTypeStr!;
                if (countPropName != null)
                {
                    sb.AppendLine($"                if ((uint)i < (uint)wrapper.Value.{countPropName})");
                    sb.AppendLine("                {");
                    if (symbol.IsValueType)
                        sb.AppendLine($"                    var __tmp = wrapper.Value; __tmp[i] = {UnmarshalRead("val", elemType)}; wrapper.Value = __tmp;");
                    else
                        sb.AppendLine($"                    wrapper.Value[i] = {UnmarshalRead("val", elemType)};");
                    sb.AppendLine("                    return new(context.Return());");
                    sb.AppendLine("                }");
                }
                else
                {
                    if (symbol.IsValueType)
                        sb.AppendLine($"                    var __tmp = wrapper.Value; __tmp[i] = {UnmarshalRead("val", elemType)}; wrapper.Value = __tmp;");
                    else
                        sb.AppendLine($"                    wrapper.Value[i] = {UnmarshalRead("val", elemType)};");
                    sb.AppendLine("                    return new(context.Return());");
                }
                sb.AppendLine("            }");
            }

            sb.AppendLine("            throw new LuaRuntimeException(context.State, $\"'{key}' not found.\");");
            sb.AppendLine("        });");
        }

        // ====================================================================
        // __len
        // ====================================================================
        if (isArray)
        {
            sb.AppendLine();
            sb.AppendLine("        Metatable[Metamethods.Len] = new LuaFunction(\"__len\", (context, ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
            sb.AppendLine("            return new(context.Return((double)wrapper.Value.Length));");
            sb.AppendLine("        });");
        }
        else if (hasListLen)
        {
            sb.AppendLine();
            sb.AppendLine("        Metatable[Metamethods.Len] = new LuaFunction(\"__len\", (context, ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
            sb.AppendLine($"            return new(context.Return((double)wrapper.Value.{countPropName}));");
            sb.AppendLine("        });");
        }

        // ====================================================================
        // __tostring
        // ====================================================================
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

    /// <summary>
    /// Marshal a value to Lua. Primitives/FixedMath get a bare cast; everything else uses FromObject.
    /// StructUserData wrapping is opt-in via [MemberLuaVisible] which is handled by the binding generator.
    /// </summary>
    private static string MarshalProperty(string expr, string typeName)
    {
        if (typeName is "int" or "long" or "float" or "double" or "bool" or "string"
            or "byte" or "sbyte" or "short" or "ushort" or "uint" or "ulong" or "decimal")
            return $"({expr})";
        if (IsFixedMathBaseType(typeName))
            return $"({expr})";
        return $"Lua.LuaValue.FromObject({expr})";
    }

    /// <summary>
    /// Marshal an array or indexer element to Lua. Same logic as MarshalProperty —
    /// StructUserData wrapping is handled by the binding generator for [MemberLuaVisible] types.
    /// </summary>
    private static string MarshalElement(string expr, string typeName) => MarshalProperty(expr, typeName);

    /// <summary>
    /// Read a LuaValue back to C# for assignment into StructUserData.
    /// </summary>
    private static string UnmarshalRead(string valExpr, string typeName)
    {
        return $"{valExpr}.Read<{typeName}>()";
    }

    /// <summary>
    /// Generate a StructUserData metatable + TypeTable for a [LuaVisible] enum.
    /// Enums can't implement ILuaUserData, so they're wrapped in StructUserData.
    /// The metatable provides __tostring (enum name), and the TypeTable maps
    /// member names to their integer values.
    /// </summary>
    private static string? GenerateEnumMetatable(LuaTypeMetadata typeMeta)
    {
        var symbol = typeMeta.Symbol;
        if (symbol.TypeKind != TypeKind.Enum) return null;

        var typeName = typeMeta.FullTypeName;
        var safeName = SanitizeHint(typeName);
        var luaName = typeMeta.LuaName;
        var enumMembers = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue && !f.IsImplicitlyDeclared).ToArray();

        var sb = new CodeBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Lua;");
        sb.AppendLine("using Lua.Runtime;");
        sb.AppendLine("using nfm_world_library.Lua;");
        sb.AppendLine();

        // --- StructUserData metatable ---
        sb.AppendLine($"internal static class StructUserData_Metatable_{safeName}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly LuaTable Metatable;");
        sb.AppendLine($"    static StructUserData_Metatable_{safeName}()");
        sb.AppendLine("    {");
        sb.AppendLine("        Metatable = new LuaTable(0, 1);");
        sb.AppendLine();
        sb.AppendLine("        Metatable[Metamethods.ToString] = new LuaFunction(\"__tostring\", (context, ct) =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var wrapper = context.GetArgument<StructUserData<{typeName}>>(0);");
        sb.AppendLine($"            return new(context.Return(System.Enum.GetName(typeof({typeName}), wrapper.Value) ?? wrapper.Value.ToString()));");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // --- TypeTable with enum members ---
        sb.AppendLine($"internal static class EnumTypeTable_{safeName}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly LuaTable TypeTable;");
        sb.AppendLine($"    static EnumTypeTable_{safeName}()");
        sb.AppendLine("    {");
        sb.AppendLine("        TypeTable = new LuaTable();");
        foreach (var member in enumMembers)
        {
            var memberName = member.Name;
            var memberValue = member.ConstantValue;
            // Use the Camel name for Lua (e.g. CheckPoint → checkPoint)
            var memberLuaName = Camel(memberName);
            if (memberValue is int intVal)
                sb.AppendLine($"        TypeTable[\"{memberLuaName}\"] = {intVal};");
            else if (memberValue is long longVal)
                sb.AppendLine($"        TypeTable[\"{memberLuaName}\"] = {longVal}L;");
            else
                sb.AppendLine($"        TypeTable[\"{memberLuaName}\"] = Lua.LuaValue.FromObject({memberValue});");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
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
            if (t.Symbol.TypeKind == TypeKind.Enum)
                sb.AppendLine($"        state.Environment[\"{luaName}\"] = EnumTypeTable_{SanitizeHint(t.FullTypeName)}.TypeTable;");
            else
                sb.AppendLine($"        state.Environment[\"{luaName}\"] = {t.FullTypeName}.TypeTable;");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateStructUserDataMetatableInitializer(List<(string ClassName, string TypeName)> metatableClasses)
    {
        var sb = new CodeBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using nfm_world_library.Lua;");
        sb.AppendLine();
        sb.AppendLine("internal static class StructUserDataMetatableInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    public static void Init()");
        sb.AppendLine("    {");
        foreach (var (className, typeName) in metatableClasses)
        {
            sb.AppendLine($"        StructUserDataMetatableRegistry<{typeName}>.Register({className}.Metatable);");
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
        return baseT is "Fixed64" or "Vector3d" or "f64AngleSingle" or "Fixed4x4"
            || baseT.EndsWith(".Fixed64") || baseT.EndsWith(".Vector3d")
            || baseT.EndsWith(".f64AngleSingle")
            || baseT.EndsWith(".Fixed4x4");
    }

    private static void WriteStubFiles(string dir, string name, string luaStub, string tsStub)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"{name}.d.lua"), luaStub);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"{name}.d.ts"), tsStub);
    }

    private static string GenerateExternalTypeLuaStub(string stubName, ITypeSymbol typeSymbol, Compilation compilation, Dictionary<string, string> luaVisibleNameMap)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"---@class {stubName}Instance");

        if (typeSymbol is INamedTypeSymbol named)
        {
            // Public instance properties (non-indexer)
            foreach (var prop in named.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public && p.GetMethod != null))
            {
                var elemDisplay = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                sb.AppendLine($"---@field {Camel(prop.Name)} {ToExternalLuaTypeName(elemDisplay, luaVisibleNameMap)}");
            }

            // Public instance fields
            foreach (var f in named.GetMembers().OfType<IFieldSymbol>()
                .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public))
            {
                var elemDisplay = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                sb.AppendLine($"---@field {Camel(f.Name)} {ToExternalLuaTypeName(elemDisplay, luaVisibleNameMap)}");
            }

            // Integer indexers
            foreach (var prop in named.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.IsIndexer && !p.IsImplicitlyDeclared))
            {
                foreach (var param in prop.Parameters)
                {
                    if (param.Type.SpecialType == SpecialType.System_Int32)
                    {
                        var elemDisplay = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                        var elemLua = ToExternalLuaTypeName(elemDisplay, luaVisibleNameMap);
                        sb.AppendLine($"---@field [integer] {elemLua}");
                    }
                }
            }
        }

        sb.AppendLine($"{stubName}Instance = {{}}");
        sb.AppendLine();
        sb.AppendLine($"---@class (exact) {stubName}");
        return sb.ToString();
    }

    /// <summary>Convert a full C# type name to a Lua stub type name for external type stubs.</summary>
    private static string ToExternalLuaTypeName(string t, Dictionary<string, string> luaVisibleNameMap)
    {
        if (t.EndsWith("?")) return $"{ToExternalLuaTypeName(t.Substring(0, t.Length - 1), luaVisibleNameMap)}|nil";
        // Handle 1D arrays
        if (t.EndsWith("[]") && !t.Contains("[")) // simple 1D
        {
            var elemType = t.Substring(0, t.Length - 2);
            return $"{{ [integer]: {ToExternalLuaTypeName(elemType, luaVisibleNameMap)} }}";
        }
        return t switch
        {
            "int" or "long" or "float" or "double" or "byte" or "sbyte"
                or "short" or "ushort" or "uint" or "ulong" or "decimal" => "number",
            "bool" => "boolean",
            "string" => "string",
            "void" => "nil",
            "object" => "any",
            "Lua.LuaTable" => "table",
            "Lua.LuaFunction" => "function",
            "Lua.LuaValue" => "any",
            _ => IsFixedMathBaseType(t) ? FixedMathBaseToLuaName(t)
                 : luaVisibleNameMap.TryGetValue(t, out var luaName) ? $"{luaName}Instance"
                 : $"{SanitizeHint(t).Replace("_Array", "Array")}Instance"
        };
    }

    private static string FixedMathBaseToLuaName(string t)
    {
        var baseT = t.Contains('<') ? t.Substring(0, t.IndexOf('<')) : t;
        if (baseT == "Fixed64" || baseT.EndsWith(".Fixed64")) return "fixed64";
        if (baseT == "Vector3d" || baseT.EndsWith(".Vector3d")) return "fixed64vector3";
        if (baseT == "f64AngleSingle" || baseT.EndsWith(".f64AngleSingle")) return "f64angle";
        if (baseT == "f64Euler" || baseT.EndsWith(".f64Euler")) return "f64euler";
        return SanitizeHint(baseT);
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

        var members = new List<string>();

        if (symbol is INamedTypeSymbol named)
        {
            // Public instance properties (non-indexer)
            foreach (var prop in named.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.DeclaredAccessibility == Accessibility.Public && p.GetMethod != null))
            {
                var ts = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                var tsName = TsSimpleName(ts);
                var prefix = prop.SetMethod != null && !prop.SetMethod.IsInitOnly ? "" : "readonly ";
                members.Add($"    {prefix}{Camel(prop.Name)}: {tsName};");
            }

            // Public instance fields
            foreach (var f in named.GetMembers().OfType<IFieldSymbol>()
                .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared && f.DeclaredAccessibility == Accessibility.Public))
            {
                var ts = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                members.Add($"    {Camel(f.Name)}: {TsSimpleName(ts)};");
            }

            // Integer indexers
            foreach (var prop in named.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.IsIndexer && !p.IsImplicitlyDeclared))
            {
                foreach (var param in prop.Parameters)
                {
                    if (param.Type.SpecialType == SpecialType.System_Int32)
                    {
                        var elemDisplay = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                        members.Add($"    [index: number]: {TsSimpleName(elemDisplay)};");
                    }
                }
            }
        }

        if (members.Count > 0)
            return $"declare class {stubName} {{\n{string.Join("\n", members)}\n}}\n";

        return $"declare class {stubName} {{\n}}\n";
    }

    private static string TsSimpleName(string t)
    {
        if (t.EndsWith("?")) return $"{TsSimpleName(t.Substring(0, t.Length - 1))} | null";
        return t switch
        {
            "int" or "long" or "float" or "double" or "byte" or "sbyte"
                or "short" or "ushort" or "uint" or "ulong" or "decimal" => "number",
            "bool" => "boolean",
            "string" => "string",
            "void" => "void",
            "object" => "any",
            _ => "any"
        };
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
    public INamedTypeSymbol? MemberLuaVisibleAttribute { get; }

    private SymbolReferences(Compilation compilation)
    {
        LuaVisibleAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaVisibleAttribute");
        LuaNameAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaNameAttribute");
        LuaHiddenAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.LuaHiddenAttribute");
        MemberLuaVisibleAttribute = compilation.GetTypeByMetadataName("nfm_world_library.Lua.MemberLuaVisibleAttribute");
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
