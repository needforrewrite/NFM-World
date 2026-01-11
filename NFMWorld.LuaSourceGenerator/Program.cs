// Lua Source Generator
// This program uses reflection to find all types marked with [LuaVisible]
// and generates Lua binding code for them.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Maxine.Extensions;
using nfm_world_library;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator;

public static class Program
{
    public static void Main(string[] args)
    {
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

        // Set up custom assembly load context to resolve dependencies from .deps.json
        var loadContext = new DependencyLoadContext(inputAssemblyPath);
        var assembly = loadContext.LoadFromAssemblyPath(inputAssemblyPath);
        
        var generator = new LuaBindingGenerator(assembly, @namespace);
        generator.GenerateToFiles(outputDirectory);
        Console.WriteLine($"Generated Lua bindings written to {outputDirectory}");
    }
}

/// <summary>
/// Custom AssemblyLoadContext that resolves dependencies from .deps.json file.
/// </summary>
internal class DependencyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly Dictionary<string, string> _assemblyPaths = new();

    public DependencyLoadContext(string mainAssemblyPath)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        
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
    }

    private void LoadDependenciesFromDepsJson(string depsJsonPath, string assemblyDirectory)
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
                        _assemblyPaths[assemblyName] = assemblyPath;
                    }
                }
            }
            break; // Only process first target
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try using the resolver first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to our parsed deps.json paths
        if (_assemblyPaths.TryGetValue(assemblyName.Name ?? "", out var path))
        {
            return LoadFromAssemblyPath(path);
        }

        // Let the default context handle it
        return null;
    }
}

/// <summary>
/// Generator that creates Lua bindings for the test sample types.
/// </summary>
public class LuaBindingGenerator(Assembly assembly, string @namespace)
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    private void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(new string(' ', _indent * 4));
            _sb.AppendLine(line);
        }
    }

    private IDisposable Indent()
    {
        _indent++;
        return new IndentDisposable(this);
    }

    private class IndentDisposable : IDisposable
    {
        private readonly LuaBindingGenerator _gen;
        public IndentDisposable(LuaBindingGenerator gen) => _gen = gen;
        public void Dispose() => _gen._indent--;
    }

    /// <summary>
    /// Check if a member has an attribute by name (to avoid assembly load context issues).
    /// </summary>
    private static bool HasAttribute(MemberInfo member, string attributeName)
    {
        return member.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == attributeName);
    }

    /// <summary>
    /// Generate bindings for all sample types in the test project.
    /// </summary>
    public List<TypeInfo> Generate()
    {
        // Find all types with [LuaVisible] in the test assembly
        var types = new List<TypeInfo>();
        var discoveredTypes = new HashSet<Type>();

        Console.WriteLine($"Scanning assembly: {assembly.FullName}");
        Console.WriteLine($"Type count: {assembly.GetTypes().Length}");

        foreach (var type in assembly.GetTypes())
        {
            // Look for [LuaVisible] by name to avoid assembly load context issues
            var luaVisibleAttr = type.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name == nameof(LuaVisibleAttribute));

            if (luaVisibleAttr != null)
            {
                Console.WriteLine($"Found [LuaVisible] type: {type.FullName}");
                
                // Extract the Name property if it exists
                string? customName = null;
                var nameProperty = luaVisibleAttr.NamedArguments
                    .FirstOrDefault(a => a.MemberName == "Name");
                if (nameProperty.TypedValue.Value is string name)
                {
                    customName = name;
                }

                var attr = new LuaVisibleAttribute { Name = customName };
                types.Add(new TypeInfo(type, attr));
                discoveredTypes.Add(type);
            }
        }

        Console.WriteLine($"Total [LuaVisible] types found: {types.Count}");

        // Discover referenced types
        var additionalTypes = DiscoverReferencedTypes(types.Select(t => t.Type).ToList(), discoveredTypes);
        foreach (var type in additionalTypes)
        {
            var luaName = type.IsGenericType
                ? GetGenericTypeLuaName(type)
                : type.Name;
            types.Add(new TypeInfo(type, new LuaVisibleAttribute { Name = luaName }));
        }

        return types;
    }

    private List<Type> DiscoverReferencedTypes(List<Type> rootTypes, HashSet<Type> discovered)
    {
        var queue = new Queue<Type>(rootTypes);
        var additional = new List<Type>();

        while (queue.Count > 0)
        {
            var currentType = queue.Dequeue();

            // Check properties
            foreach (var prop in currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (HasAttribute(prop, nameof(LuaHiddenAttribute))) continue;
                ProcessType(prop.PropertyType, discovered, queue, additional);
            }

            // Check fields
            foreach (var field in currentType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (HasAttribute(field, nameof(LuaHiddenAttribute))) continue;
                ProcessType(field.FieldType, discovered, queue, additional);
            }

            // Check methods
            foreach (var method in currentType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.IsSpecialName || HasAttribute(method, nameof(LuaHiddenAttribute))) continue;

                // Return type
                if (method.ReturnType != typeof(void))
                {
                    ProcessType(method.ReturnType, discovered, queue, additional);
                }

                // Parameters
                foreach (var param in method.GetParameters())
                {
                    ProcessType(param.ParameterType, discovered, queue, additional);
                }
            }

            // Check constructors
            foreach (var ctor in currentType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                if (HasAttribute(ctor, nameof(LuaHiddenAttribute))) continue;

                foreach (var param in ctor.GetParameters())
                {
                    ProcessType(param.ParameterType, discovered, queue, additional);
                }
            }
        }

        return additional;
    }

    private void ProcessType(Type type, HashSet<Type> discovered, Queue<Type> queue, List<Type> additional)
    {
        // Skip byref types (ref/out parameters)
        if (type.IsByRef)
            return;

        // Skip pointer types
        if (type.IsPointer)
            return;

        // Skip delegates
        if (typeof(Delegate).IsAssignableFrom(type))
            return;

        // Skip generic type definitions (open generics like List<T>)
        if (type.IsGenericTypeDefinition)
            return;

        // Skip types with unresolved generic parameters (like List<T>.Enumerator where T is not resolved)
        if (type.ContainsGenericParameters)
            return;

        // Skip types whose full name contains backticks (indicates unresolved generics)
        if (type.FullName?.Contains('`') == true && !type.IsGenericType)
            return;

        // Handle array types - add the array type itself AND process element type
        if (type.IsArray)
        {
            if (!discovered.Contains(type))
            {
                discovered.Add(type);
                queue.Enqueue(type);
                additional.Add(type);
            }

            // Also process element type
            var elementType = type.GetElementType()!;
            ProcessType(elementType, discovered, queue, additional);
            return;
        }

        // Unwrap nullable
        if (IsNullable(type))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        // Skip primitives and already discovered
        if (IsPrimitiveOrKnownType(type) || discovered.Contains(type))
            return;

        // Skip nested types from system assemblies (like List<T>.Enumerator)
        if (type.IsNested && type.Assembly != assembly)
            return;

        // For generic types, use the constructed generic type
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            discovered.Add(type);
            queue.Enqueue(type);
            additional.Add(type);
        }
        // For non-generic types from our assembly
        else if (!type.IsGenericType && type.Assembly == assembly)
        {
            discovered.Add(type);
            queue.Enqueue(type);
            additional.Add(type);
        }
    }

    private static bool IsPrimitiveOrKnownType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(object) ||
               type == typeof(void) ||
               type == typeof(decimal) ||
               type.IsEnum ||
               type.IsPointer ||
               type.IsByRef ||
               typeof(Delegate).IsAssignableFrom(type) ||
               typeof(MulticastDelegate).IsAssignableFrom(type);
    }

    private static string GetGenericTypeLuaName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var rank = type.GetArrayRank();
            if (rank == 1)
            {
                return $"ArrayOf{GetSimpleTypeName(elementType)}";
            }
            else
            {
                return $"ArrayOf{GetSimpleTypeName(elementType)}{rank}D";
            }
        }

        if (!type.IsGenericType)
            return type.Name;

        var baseName = type.Name.Split('`')[0];
        var genericArgs = type.GetGenericArguments();
        var argNames = string.Join("_", genericArgs.Select(t => GetSimpleTypeName(t)));
        return $"{baseName}_{argNames}";
    }

    private static string GetSimpleTypeName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var rank = type.GetArrayRank();
            return rank == 1 ? $"{GetSimpleTypeName(elementType)}Array" : $"{GetSimpleTypeName(elementType)}{rank}DArray";
        }
        if (type.IsGenericType)
        {
            return GetGenericTypeLuaName(type);
        }
        return type.Name;
    }

    public void GenerateToFiles(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var types = Generate();

        // Generate Initialize file
        GenerateInitializeFile(outputDirectory, types);

        // Generate one file per type
        foreach (var typeInfo in types)
        {
            GenerateTypeFile(outputDirectory, typeInfo);
        }

        GenerateLuaBindingsBaseFile(outputDirectory);
    }

    private void GenerateLuaBindingsBaseFile(string outputDirectory)
    {
        var luaBindingsBaseFile =
            $$"""
            // <auto-generated />
            // This file is auto-generated by NFMWorld.LuaSourceGenerator.
            // ReSharper disable All

            #nullable enable

            using System;
            using System.Collections.Generic;
            using System.Reflection;
            using System.Runtime.InteropServices;
            using LuaNET.LuaJIT;
            using Maxine.Extensions;
            using Microsoft.Collections.Extensions;
            using static LuaNET.LuaJIT.Lua;

            namespace {{@namespace}};

            public partial class LuaBindings
            {
                private static int _nextObjectId = 1;

                private static class TypeInfo<T>
                {
                    // Maps C# types to their Lua metatable names
                    public static string? Name;

                    // Storage for managed objects referenced by Lua userdata
                    public static readonly DictionarySlim<int, T> Objects = [];
                }

                // Keep delegates alive to prevent GC collection
                private static readonly HashSet<lua_CFunction> _delegates = [];

                /// <summary>
                /// Reset the bindings state (for test isolation).
                /// </summary>
                public static void Reset()
                {
                    _delegates.Clear();
                    _objectCount = 0;
                    _nextObjectId = 1;
                }

                public static void ResetType<T>()
                {
                    TypeInfo<T>.Name = null;
                    TypeInfo<T>.Objects.Clear();
                }

                #region Object Storage

                private static int _objectCount = 0;

                /// <summary>
                /// Store a managed object and return its ID.
                /// </summary>
                private static int StoreObject<T>(T obj)
                {
                    var id = _nextObjectId++;
                    TypeInfo<T>.Objects.GetOrAddValueRef(id) = obj;
                    _objectCount++;
                    return id;
                }

                /// <summary>
                /// Get a managed object by ID.
                /// </summary>
                private static T? GetObject<T>(int id)
                {
                    return TypeInfo<T>.Objects.GetValueOrDefault(id);
                }

                /// <summary>
                /// Remove a managed object by ID.
                /// </summary>
                private static void RemoveObject<T>(int id)
                {
                    TypeInfo<T>.Objects.Remove(id);
                    _objectCount--;
                }

                /// <summary>
                /// Get the current object count (for testing).
                /// </summary>
                public static int ObjectCount => _objectCount;

                #endregion

                #region Lua Stack Operations

                // Map to store metatable names for runtime type lookup (for arrays and dynamic types)
                private static readonly Dictionary<Type, string> _typeToMetatable = new();

                /// <summary>
                /// Register a metatable name for a type (called during initialization).
                /// </summary>
                private static void RegisterMetatable<T>(string metatableName)
                {
                    TypeInfo<T>.Name = metatableName;
                    _typeToMetatable[typeof(T)] = metatableName;
                }

                /// <summary>
                /// Get the metatable name for a runtime type (used for arrays and dynamic dispatch).
                /// </summary>
                private static string? GetMetatableNameForType(Type type)
                {
                    return _typeToMetatable.GetValueOrDefault(type);
                }

                /// <summary>
                /// Push a userdata for a managed object onto the Lua stack (dynamic version for runtime types).
                /// </summary>
                private static void PushObjectDynamic(lua_State L, object obj, string metatableName)
                {
                    // We need to call StoreObject with the correct generic type at runtime
                    var objType = obj.GetType();
                    var storeMethod = typeof(LuaBindings).GetMethod("StoreObject", BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(objType);
                    var id = (int)storeMethod.Invoke(null, new[] { obj })!;

                    var ptr = lua_newuserdata(L, (ulong)sizeof(int));
                    unsafe { *(int*)ptr = id; }
                    luaL_getmetatable(L, metatableName);
                    lua_setmetatable(L, -2);
                }

                /// <summary>
                /// Push a userdata for a managed object onto the Lua stack.
                /// </summary>
                private static void PushObject<T>(lua_State L, T obj, string metatableName)
                {
                    var id = StoreObject(obj);
                    var ptr = lua_newuserdata(L, (ulong)sizeof(int));
                    unsafe { *(int*)ptr = id; }
                    luaL_getmetatable(L, metatableName);
                    lua_setmetatable(L, -2);
                }

                /// <summary>
                /// Get a managed reference type object from userdata at stack index.
                /// </summary>
                private static T? GetObjectFromStack<T>(lua_State L, int idx) where T : class
                {
                    var ptr = lua_touserdata(L, idx);
                    if (ptr == 0) return null;
                    unsafe
                    {
                        var id = *(int*)ptr;
                        return GetObject<T>(id);
                    }
                }

                /// <summary>
                /// Get a struct from userdata at stack index (returns copy for value type semantics).
                /// </summary>
                private static T GetStructFromStack<T>(lua_State L, int idx) where T : struct
                {
                    var ptr = lua_touserdata(L, idx);
                    if (ptr == 0) return default;
                    unsafe
                    {
                        var id = *(int*)ptr;
                        var obj = GetObject<T>(id);
                        if (obj is T value) return value;
                        return default;
                    }
                }

                /// <summary>
                /// Update a struct in storage (for mutable operations that create copies).
                /// This maintains value type semantics where mutations create new values.
                /// </summary>
                private static void UpdateStruct<T>(lua_State L, int idx, T value) where T : struct
                {
                    var ptr = lua_touserdata(L, idx);
                    if (ptr == 0) return;
                    unsafe
                    {
                        var id = *(int*)ptr;
                        TypeInfo<T>.Objects.GetOrAddValueRef(id) = value;
                    }
                }

                /// <summary>
                /// Push a value to Lua stack based on its runtime type.
                /// </summary>
                private static void PushValue<T>(lua_State L, T value)
                {
                    switch (value)
                    {
                        case null:
                            lua_pushnil(L);
                            break;
                        case bool b:
                            lua_pushboolean(L, b ? 1 : 0);
                            break;
                        case int i:
                            lua_pushinteger(L, i);
                            break;
                        case uint u:
                            lua_pushinteger(L, u);
                            break;
                        case byte by:
                            lua_pushinteger(L, by);
                            break;
                        case sbyte sb:
                            lua_pushinteger(L, sb);
                            break;
                        case short s:
                            lua_pushinteger(L, s);
                            break;
                        case ushort us:
                            lua_pushinteger(L, us);
                            break;
                        case long l:
                            lua_pushinteger(L, l);
                            break;
                        case ulong ul:
                            lua_pushinteger(L, (long)ul);
                            break;
                        case float f:
                            lua_pushnumber(L, f);
                            break;
                        case double d:
                            lua_pushnumber(L, d);
                            break;
                        case string s:
                            lua_pushstring(L, s);
                            break;
                        default:
                            // For arrays, we need to check the actual runtime type since we don't have compile-time metatable info
                            var t = typeof(T);
                            if (t.IsArray)
                            {
                                // Use the array's actual type to find the metatable
                                var arrayType = value!.GetType();
                                var metatableName = GetMetatableNameForType(arrayType);
                                if (metatableName != null)
                                {
                                    PushObjectDynamic(L, value, metatableName);
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Array type {arrayType} is not registered");
                                }
                            }
                            // For all other types, push as userdata if we have a registered metatable
                            else if (TypeInfo<T>.Name is {} metatable)
                            {
                                PushObject(L, value, metatable);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Type {typeof(T)} is not supported");
                            }
                            break;
                    }
                }



                /// <summary>
                /// Convert Lua value at stack index to a C# object of the target type.
                /// </summary>
                private static T? ToObject<T>(lua_State L, int idx)
                {
                    var luaType = lua_type(L, idx);

                    if (luaType == LUA_TNIL) return default;

                    if (typeof(T) == typeof(bool) || luaType == LUA_TBOOLEAN) return (T)(object)(lua_toboolean(L, idx) != 0);
                    if (typeof(T) == typeof(int)) return (T)(object)(int)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(uint)) return (T)(object)(uint)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(byte)) return (T)(object)(byte)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(short)) return (T)(object)(short)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(long)) return (T)(object)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(ulong)) return (T)(object)(ulong)lua_tointeger(L, idx);
                    if (typeof(T) == typeof(float)) return (T)(object)(float)lua_tonumber(L, idx);
                    if (typeof(T) == typeof(double)) return (T)(object)lua_tonumber(L, idx);

                    if (typeof(T) == typeof(string) || luaType == LUA_TSTRING) return (T)(object)lua_tostring(L, idx)!;

                    // Handle userdata (objects, structs, arrays, etc.)
                    if (luaType == LUA_TUSERDATA)
                    {
                        var ptr = lua_touserdata(L, idx);
                        if (ptr != 0)
                        {
                            unsafe
                            {
                                var id = *(int*)ptr;
                                return GetObject<T>(id);
                            }
                        }
                    }

                    throw new InvalidOperationException($"Cannot convert Lua type {luaType} to {typeof(T)}");
                }

                #endregion

                #region Delegate Management

                /// <summary>
                /// Keep a delegate alive to prevent GC collection while registered with Lua.
                /// </summary>
                private static lua_CFunction KeepAlive(lua_CFunction func)
                {
                    _delegates.Add(func);
                    return func;
                }

                #endregion

            }
            """;

        var filePath = Path.Combine(outputDirectory, "LuaBindings.Base.g.cs");
        File.WriteAllText(filePath, luaBindingsBaseFile);
    }

    private void GenerateInitializeFile(string outputDirectory, List<TypeInfo> types)
    {
        _sb.Clear();
        _indent = 0;

        AppendLine("// <auto-generated />");
        AppendLine("// This file is auto-generated by NFMWorld.LuaSourceGenerator.");
        AppendLine("// ReSharper disable All");
        AppendLine("#nullable enable");
        AppendLine();
        AppendLine("using LuaNET.LuaJIT;");
        AppendLine("using static LuaNET.LuaJIT.Lua;");
        AppendLine();
        AppendLine($"namespace {@namespace};");
        AppendLine();
        AppendLine("public partial class LuaBindings");
        AppendLine("{");

        using (Indent())
        {
            GenerateInitializeMethod(types);
        }

        AppendLine("}");

        var filePath = Path.Combine(outputDirectory, "LuaBindings.Initialize.g.cs");
        File.WriteAllText(filePath, _sb.ToString());
    }

    private void GenerateTypeFile(string outputDirectory, TypeInfo typeInfo)
    {
        _sb.Clear();
        _indent = 0;

        AppendLine("// <auto-generated />");
        AppendLine("// This file is auto-generated by NFMWorld.LuaSourceGenerator.");
        AppendLine("// ReSharper disable All");
        AppendLine("#nullable enable");
        AppendLine();
        AppendLine("using LuaNET.LuaJIT;");
        AppendLine("using static LuaNET.LuaJIT.Lua;");
        AppendLine();
        AppendLine($"namespace {@namespace};");
        AppendLine();
        AppendLine("public partial class LuaBindings");
        AppendLine("{");

        using (Indent())
        {
            GenerateTypeBindings(typeInfo);
        }

        AppendLine("}");

        var safeTypeName = GetSafeTypeName(typeInfo.Type);
        var filePath = Path.Combine(outputDirectory, $"{safeTypeName}.g.cs");
        File.WriteAllText(filePath, _sb.ToString());
    }

    private void GenerateInitializeMethod(List<TypeInfo> types)
    {
        AppendLine("/// <summary>");
        AppendLine("/// Initialize all Lua bindings for types marked with [LuaVisible]");
        AppendLine("/// </summary>");
        AppendLine("public static void Initialize(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            foreach (var typeInfo in types)
            {
                AppendLine($"Register_{GetSafeTypeName(typeInfo.Type)}(L);");
            }
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateTypeBindings(TypeInfo typeInfo)
    {
        var type = typeInfo.Type;
        var luaName = typeInfo.LuaName;
        var safeName = GetSafeTypeName(type);
        var metatableName = $"MT_{safeName}";
        var isStruct = type.IsValueType;
        var fullTypeName = GetFullTypeName(type);

        AppendLine($"// =========== Bindings for {type.Name} ({luaName}) ===========");
        AppendLine($"private static void Register_{safeName}(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            AppendLine($"RegisterMetatable<{fullTypeName}>(\"{metatableName}\");");
            AppendLine();
            AppendLine("// Create metatable for instances");
            AppendLine($"luaL_newmetatable(L, \"{metatableName}\");");
            AppendLine();

            // __gc
            AppendLine("// __gc metamethod");
            AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}__gc));");
            AppendLine("lua_setfield(L, -2, \"__gc\");");
            AppendLine();

            // __index
            AppendLine("// __index metamethod");
            AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}__index));");
            AppendLine("lua_setfield(L, -2, \"__index\");");
            AppendLine();

            // __newindex
            AppendLine("// __newindex metamethod");
            AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}__newindex));");
            AppendLine("lua_setfield(L, -2, \"__newindex\");");
            AppendLine();

            // Operator metamethods
            GenerateOperatorMetamethods(type, safeName);

            // __tostring
            AppendLine("// __tostring metamethod");
            AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}__tostring));");
            AppendLine("lua_setfield(L, -2, \"__tostring\");");
            AppendLine();

            AppendLine("lua_pop(L, 1);");
            AppendLine();

            // Type table
            AppendLine($"// Create type table for {luaName}");
            AppendLine("lua_newtable(L);");
            AppendLine();

            // Constructor
            AppendLine("// Constructor: new()");
            AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_new));");
            AppendLine("lua_setfield(L, -2, \"new\");");
            AppendLine();

            // Static methods
            var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)) && !m.IsGenericMethod)
                .GroupBy(m => GetLuaMethodName(m))
                .ToList();

            foreach (var methodGroup in staticMethods)
            {
                var methodName = methodGroup.Key;
                AppendLine($"// Static method: {methodName}");
                AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_static_{GetSafeMethodName(methodName)}));");
                AppendLine($"lua_setfield(L, -2, \"{methodName}\");");
                AppendLine();
            }

            // Static properties metatable
            var staticProps = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)))
                .ToList();

            if (staticProps.Count > 0)
            {
                AppendLine("// Create metatable for type table (static properties)");
                AppendLine("lua_newtable(L);");
                AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_type__index));");
                AppendLine("lua_setfield(L, -2, \"__index\");");

                var writableStaticProps = staticProps.Where(p => p.CanWrite).ToList();
                if (writableStaticProps.Count > 0)
                {
                    AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_type__newindex));");
                    AppendLine("lua_setfield(L, -2, \"__newindex\");");
                }

                AppendLine("lua_setmetatable(L, -2);");
                AppendLine();
            }

            AppendLine($"lua_setglobal(L, \"{luaName}\");");
        }
        AppendLine("}");
        AppendLine();

        // Generate all method implementations
        GenerateGcMethod(safeName, fullTypeName);
        GenerateIndexMethod(type, safeName, isStruct, fullTypeName);
        GenerateNewIndexMethod(type, safeName, isStruct, fullTypeName);
        GenerateTostringMethod(safeName, isStruct, fullTypeName);
        GenerateOperatorMethods(type, safeName, fullTypeName);
        GenerateConstructorMethod(type, safeName, isStruct, fullTypeName);
        GenerateStaticMethods(type, safeName, fullTypeName);
        GenerateInstanceMethods(type, safeName, isStruct, fullTypeName);

        var staticPropsForAccessors = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)))
            .ToList();

        if (staticPropsForAccessors.Count > 0)
        {
            GenerateStaticPropertyAccessors(type, safeName, fullTypeName);
        }
    }

    private void GenerateOperatorMetamethods(Type type, string safeName)
    {
        var operators = GetOperatorMethods(type);

        foreach (var op in operators)
        {
            var luaMetamethod = GetLuaMetamethodName(op.Name);
            if (luaMetamethod != null)
            {
                AppendLine($"// Operator: {luaMetamethod}");
                AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_op_{GetSafeMethodName(op.Name)}));");
                AppendLine($"lua_setfield(L, -2, \"{luaMetamethod}\");");
                AppendLine();
            }
        }
    }

    private void GenerateGcMethod(string safeName, string fullTypeName)
    {
        AppendLine($"private static int {safeName}__gc(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            AppendLine("var ptr = lua_touserdata(L, 1);");
            AppendLine("if (ptr != 0)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine("unsafe");
                AppendLine("{");
                using (Indent())
                {
                    AppendLine("var id = *(int*)ptr;");
                    AppendLine($"RemoveObject<{fullTypeName}>(id);");
                }
                AppendLine("}");
            }
            AppendLine("}");
            AppendLine("return 0;");
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateIndexMethod(Type type, string safeName, bool isStruct, string fullTypeName)
    {
        AppendLine($"private static int {safeName}__index(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            if (isStruct)
            {
                AppendLine($"var obj = GetStructFromStack<{fullTypeName}>(L, 1);");
            }
            else
            {
                AppendLine($"var obj = GetObjectFromStack<{fullTypeName}>(L, 1);");
                AppendLine("if (obj == null) { lua_pushnil(L); return 1; }");
            }
            AppendLine();

            // Check for array or indexer access first (when key is number or table)
            var isArray = type.IsArray;
            var indexers = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) &&
                            p.CanRead &&
                            p.GetIndexParameters().Length > 0)
                .ToList();

            if (isArray || indexers.Count > 0)
            {
                AppendLine("// Check if key is a number (array/indexer access)");
                AppendLine("if (lua_type(L, 2) == LUA_TNUMBER)");
                AppendLine("{");
                using (Indent())
                {
                    if (isArray)
                    {
                        var rank = type.GetArrayRank();
                        if (rank == 1)
                        {
                            AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                            AppendLine($"if (index >= 0 && index < (obj as System.Array)!.Length)");
                            AppendLine("{");
                            using (Indent())
                            {
                                var elementType = type.GetElementType()!;
                                AppendLine($"var element = (obj as {fullTypeName})![index];");
                                GeneratePushValue("element", elementType);
                                AppendLine("return 1;");
                            }
                            AppendLine("}");
                            AppendLine("lua_pushnil(L);");
                            AppendLine("return 1;");
                        }
                        else
                        {
                            // Multi-dimensional array - expect single number fails, need table
                            AppendLine("luaL_error(L, \"Multi-dimensional arrays require table indices, e.g., arr[{0,1}]\");");
                            AppendLine("return 0;");
                        }
                    }
                    else if (indexers.Count > 0)
                    {
                        // Find single-parameter integer indexer
                        var singleIntIndexer = indexers.FirstOrDefault(p =>
                            p.GetIndexParameters().Length == 1 &&
                            (p.GetIndexParameters()[0].ParameterType == typeof(int) ||
                             p.GetIndexParameters()[0].ParameterType == typeof(long)));

                        if (singleIntIndexer != null)
                        {
                            AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                            AppendLine($"var element = obj[index];");
                            GeneratePushValue("element", singleIntIndexer.PropertyType);
                            AppendLine("return 1;");
                        }
                        else
                        {
                            AppendLine("// No single integer indexer found");
                            AppendLine("lua_pushnil(L);");
                            AppendLine("return 1;");
                        }
                    }
                }
                AppendLine("}");
                AppendLine();

                // Handle table-based indexing for multi-dimensional arrays and multi-parameter indexers
                if ((isArray && type.GetArrayRank() > 1) || indexers.Any(p => p.GetIndexParameters().Length > 1))
                {
                    AppendLine("// Check if key is a table (multi-dimensional array/indexer access)");
                    AppendLine("if (lua_type(L, 2) == LUA_TTABLE)");
                    AppendLine("{");
                    using (Indent())
                    {
                        if (isArray && type.GetArrayRank() > 1)
                        {
                            var rank = type.GetArrayRank();
                            AppendLine($"// Multi-dimensional array with rank {rank}");
                            AppendLine($"var indices = new int[{rank}];");
                            AppendLine($"for (int i = 0; i < {rank}; i++)");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("lua_rawgeti(L, 2, i + 1);");
                                AppendLine("indices[i] = (int)lua_tointeger(L, -1) - 1; // Convert from 1-indexed to 0-indexed");
                                AppendLine("lua_pop(L, 1);");
                            }
                            AppendLine("}");
                            var elementType = type.GetElementType()!;
                            AppendLine($"var element = (obj as {fullTypeName})!.GetValue(indices);");
                            GeneratePushValue("element", elementType);
                            AppendLine("return 1;");
                        }
                        else
                        {
                            // Handle multi-parameter indexers
                            AppendLine("// Extract indices from table");
                            AppendLine("var tableLen = 0;");
                            AppendLine("while (true)");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("lua_rawgeti(L, 2, tableLen + 1);");
                                AppendLine("if (lua_type(L, -1) == LUA_TNIL) { lua_pop(L, 1); break; }");
                                AppendLine("lua_pop(L, 1);");
                                AppendLine("tableLen++;");
                            }
                            AppendLine("}");
                            AppendLine();

                            // Generate overload matching for multi-param indexers
                            foreach (var indexer in indexers.Where(p => p.GetIndexParameters().Length > 1))
                            {
                                var indexParams = indexer.GetIndexParameters();
                                AppendLine($"if (tableLen == {indexParams.Length})");
                                AppendLine("{");
                                using (Indent())
                                {
                                    for (int i = 0; i < indexParams.Length; i++)
                                    {
                                        AppendLine($"lua_rawgeti(L, 2, {i + 1});");
                                        var paramType = GetFullTypeName(indexParams[i].ParameterType);
                                        AppendLine($"var idx{i} = ToObject<{paramType}>(L, -1)!;");
                                        AppendLine("lua_pop(L, 1);");
                                    }
                                    var indexArgList = string.Join(", ", Enumerable.Range(0, indexParams.Length).Select(i => $"idx{i}"));
                                    AppendLine($"var element = obj[{indexArgList}];");
                                    GeneratePushValue("element", indexer.PropertyType);
                                    AppendLine("return 1;");
                                }
                                AppendLine("}");
                            }

                            AppendLine("lua_pushnil(L);");
                            AppendLine("return 1;");
                        }
                    }
                    AppendLine("}");
                    AppendLine();
                }
            }

            // String key for named properties/fields/methods
            AppendLine("var key = lua_tostring(L, 2);");
            AppendLine("if (key == null) { lua_pushnil(L); return 1; }");
            AppendLine();

            AppendLine("switch (key)");
            AppendLine("{");
            using (Indent())
            {
                // Properties
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) &&
                                p.CanRead &&
                                p.GetIndexParameters().Length == 0) // Skip indexers
                    .ToList();

                foreach (var prop in props)
                {
                    var luaName = GetLuaPropertyName(prop);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GeneratePushValue($"obj.{prop.Name}", prop.PropertyType);
                        AppendLine("return 1;");
                    }
                }

                // Fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !HasAttribute(f, nameof(LuaHiddenAttribute)))
                    .ToList();

                foreach (var field in fields)
                {
                    var luaName = GetLuaFieldName(field);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GeneratePushValue($"obj.{field.Name}", field.FieldType);
                        AppendLine("return 1;");
                    }
                }

                // Instance methods
                // For array types, exclude methods declared on the array type itself (Get/Set/Address)
                // as well as methods from Array and object base classes
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName &&
                                !HasAttribute(m, nameof(LuaHiddenAttribute)) &&
                                !m.IsGenericMethod &&
                                (!type.IsArray || (m.DeclaringType != type && m.DeclaringType != typeof(Array) && m.DeclaringType != typeof(object)))) // Skip array-specific and inherited methods
                    .GroupBy(m => GetLuaMethodName(m))
                    .ToList();

                foreach (var methodGroup in methods)
                {
                    var methodName = methodGroup.Key;
                    AppendLine($"case \"{methodName}\":");
                    using (Indent())
                    {
                        AppendLine($"lua_pushcfunction(L, KeepAlive({safeName}_method_{GetSafeMethodName(methodName)}));");
                        AppendLine("return 1;");
                    }
                }

                AppendLine("default:");
                using (Indent())
                {
                    AppendLine("lua_pushnil(L);");
                    AppendLine("return 1;");
                }
            }
            AppendLine("}");
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateNewIndexMethod(Type type, string safeName, bool isStruct, string fullTypeName)
    {
        AppendLine($"private static int {safeName}__newindex(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            if (isStruct)
            {
                AppendLine($"var obj = GetStructFromStack<{fullTypeName}>(L, 1);");
            }
            else
            {
                AppendLine($"var obj = GetObjectFromStack<{fullTypeName}>(L, 1);");
                AppendLine("if (obj == null) return 0;");
            }
            AppendLine();

            // Check for array or indexer assignment first (when key is number or table)
            var isArray = type.IsArray;
            var indexers = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) &&
                            p.CanWrite &&
                            p.GetIndexParameters().Length > 0)
                .ToList();

            if (isArray || indexers.Count > 0)
            {
                AppendLine("// Check if key is a number (array/indexer assignment)");
                AppendLine("if (lua_type(L, 2) == LUA_TNUMBER)");
                AppendLine("{");
                using (Indent())
                {
                    if (isArray)
                    {
                        var rank = type.GetArrayRank();
                        if (rank == 1)
                        {
                            AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                            AppendLine($"if (index >= 0 && index < (obj as System.Array)!.Length)");
                            AppendLine("{");
                            using (Indent())
                            {
                                var elementType = type.GetElementType()!;
                                var elementTypeName = GetFullTypeName(elementType);
                                AppendLine($"var value = ToObject<{elementTypeName}>(L, 3)!;");
                                AppendLine($"(obj as {fullTypeName})![index] = value;");
                            }
                            AppendLine("}");
                            AppendLine("return 0;");
                        }
                        else
                        {
                            // Multi-dimensional array - expect single number fails, need table
                            AppendLine("luaL_error(L, \"Multi-dimensional arrays require table indices, e.g., arr[{0,1}] = value\");");
                            AppendLine("return 0;");
                        }
                    }
                    else if (indexers.Count > 0)
                    {
                        // Find single-parameter integer indexer
                        var singleIntIndexer = indexers.FirstOrDefault(p =>
                            p.GetIndexParameters().Length == 1 &&
                            (p.GetIndexParameters()[0].ParameterType == typeof(int) ||
                             p.GetIndexParameters()[0].ParameterType == typeof(long)));

                        if (singleIntIndexer != null)
                        {
                            AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                            var indexerTypeName = GetFullTypeName(singleIntIndexer.PropertyType);
                            AppendLine($"var value = ToObject<{indexerTypeName}>(L, 3)!;");
                            AppendLine($"obj[index] = value;");
                            AppendLine("return 0;");
                        }
                        else
                        {
                            AppendLine("return 0;");
                        }
                    }
                }
                AppendLine("}");
                AppendLine();

                // Handle table-based indexing for multi-dimensional arrays and multi-parameter indexers
                if ((isArray && type.GetArrayRank() > 1) || indexers.Any(p => p.GetIndexParameters().Length > 1))
                {
                    AppendLine("// Check if key is a table (multi-dimensional array/indexer assignment)");
                    AppendLine("if (lua_type(L, 2) == LUA_TTABLE)");
                    AppendLine("{");
                    using (Indent())
                    {
                        if (isArray && type.GetArrayRank() > 1)
                        {
                            var rank = type.GetArrayRank();
                            AppendLine($"// Multi-dimensional array with rank {rank}");
                            AppendLine($"var indices = new int[{rank}];");
                            AppendLine($"for (int i = 0; i < {rank}; i++)");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("lua_rawgeti(L, 2, i + 1);");
                                AppendLine("indices[i] = (int)lua_tointeger(L, -1) - 1; // Convert from 1-indexed to 0-indexed");
                                AppendLine("lua_pop(L, 1);");
                            }
                            AppendLine("}");
                            var elementType = type.GetElementType()!;
                            var elementTypeName = GetFullTypeName(elementType);
                            AppendLine($"var value = ToObject<{elementTypeName}>(L, 3)!;");
                            AppendLine($"(obj as {fullTypeName})!.SetValue(value, indices);");
                            AppendLine("return 0;");
                        }
                        else
                        {
                            // Handle multi-parameter indexers
                            AppendLine("// Extract indices from table");
                            AppendLine("var tableLen = 0;");
                            AppendLine("while (true)");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("lua_rawgeti(L, 2, tableLen + 1);");
                                AppendLine("if (lua_type(L, -1) == LUA_TNIL) { lua_pop(L, 1); break; }");
                                AppendLine("lua_pop(L, 1);");
                                AppendLine("tableLen++;");
                            }
                            AppendLine("}");
                            AppendLine();

                            // Generate overload matching for multi-param indexers
                            foreach (var indexer in indexers.Where(p => p.GetIndexParameters().Length > 1))
                            {
                                var indexParams = indexer.GetIndexParameters();
                                AppendLine($"if (tableLen == {indexParams.Length})");
                                AppendLine("{");
                                using (Indent())
                                {
                                    for (int i = 0; i < indexParams.Length; i++)
                                    {
                                        AppendLine($"lua_rawgeti(L, 2, {i + 1});");
                                        var paramType = GetFullTypeName(indexParams[i].ParameterType);
                                        AppendLine($"var idx{i} = ToObject<{paramType}>(L, -1)!;");
                                        AppendLine("lua_pop(L, 1);");
                                    }
                                    var indexArgList = string.Join(", ", Enumerable.Range(0, indexParams.Length).Select(i => $"idx{i}"));
                                    var valueTypeName = GetFullTypeName(indexer.PropertyType);
                                    AppendLine($"var value = ToObject<{valueTypeName}>(L, 3)!;");
                                    AppendLine($"obj[{indexArgList}] = value;");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                            }

                            AppendLine("return 0;");
                        }
                    }
                    AppendLine("}");
                    AppendLine();
                }
            }

            // String key for named properties/fields
            AppendLine("var key = lua_tostring(L, 2);");
            AppendLine("if (key == null) return 0;");
            AppendLine();

            AppendLine("switch (key)");
            AppendLine("{");
            using (Indent())
            {
                // Writable properties (excluding init-only)
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) &&
                                p.CanWrite &&
                                !IsInitOnly(p) &&
                                p.GetIndexParameters().Length == 0) // Skip indexers
                    .ToList();

                foreach (var prop in props)
                {
                    var luaName = GetLuaPropertyName(prop);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GenerateToObjectCode($"obj.{prop.Name}", prop.PropertyType, "3");
                        if (isStruct) AppendLine("UpdateStruct(L, 1, obj);");
                        AppendLine("break;");
                    }
                }

                // Writable fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !HasAttribute(f, nameof(LuaHiddenAttribute)) && !f.IsInitOnly)
                    .ToList();

                foreach (var field in fields)
                {
                    var luaName = GetLuaFieldName(field);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GenerateToObjectCode($"obj.{field.Name}", field.FieldType, "3");
                        if (isStruct) AppendLine("UpdateStruct(L, 1, obj);");
                        AppendLine("break;");
                    }
                }
            }
            AppendLine("}");
            AppendLine("return 0;");
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateTostringMethod(string safeName, bool isStruct, string fullTypeName)
    {
        AppendLine($"private static int {safeName}__tostring(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            if (isStruct)
            {
                AppendLine($"var obj = GetStructFromStack<{fullTypeName}>(L, 1);");
                AppendLine("lua_pushstring(L, obj.ToString() ?? \"\");");
            }
            else
            {
                AppendLine($"var obj = GetObjectFromStack<{fullTypeName}>(L, 1);");
                AppendLine("lua_pushstring(L, obj?.ToString() ?? \"nil\");");
            }
            AppendLine("return 1;");
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateOperatorMethods(Type type, string safeName, string fullTypeName)
    {
        var operators = GetOperatorMethods(type);

        foreach (var op in operators)
        {
            var luaMetamethod = GetLuaMetamethodName(op.Name);
            if (luaMetamethod == null) continue;

            AppendLine($"private static int {safeName}_op_{GetSafeMethodName(op.Name)}(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                var parameters = op.Parameters;

                if (parameters.Length == 2)
                {
                    var leftType = GetFullTypeName(parameters[0].ParameterType);
                    var rightType = GetFullTypeName(parameters[1].ParameterType);
                    AppendLine($"var left = ToObject<{leftType}>(L, 1)!;");
                    AppendLine($"var right = ToObject<{rightType}>(L, 2)!;");

                    // Use the actual operator syntax
                    var opSymbol = GetOperatorSymbol(op.Name);
                    if (opSymbol != null)
                    {
                        if (op.Name == "op_Equality")
                        {
                            AppendLine($"lua_pushboolean(L, left == right ? 1 : 0);");
                        }
                        else
                        {
                            AppendLine($"var result = left {opSymbol} right;");
                            GeneratePushValue("result", op.Method.ReturnType);
                        }
                    }
                    else
                    {
                        AppendLine($"var result = {fullTypeName}.{op.Name}(left, right);");
                        GeneratePushValue("result", op.Method.ReturnType);
                    }
                    AppendLine("return 1;");
                }
                else if (parameters.Length == 1)
                {
                    var operandType = GetFullTypeName(parameters[0].ParameterType);
                    AppendLine($"var operand = ToObject<{operandType}>(L, 1)!;");

                    var opSymbol = GetOperatorSymbol(op.Name);
                    if (opSymbol != null)
                    {
                        AppendLine($"var result = {opSymbol}operand;");
                    }
                    else
                    {
                        AppendLine($"var result = {fullTypeName}.{op.Name}(operand);");
                    }
                    GeneratePushValue("result", op.Method.ReturnType);
                    AppendLine("return 1;");
                }
            }
            AppendLine("}");
            AppendLine();
        }
    }

    private void GenerateConstructorMethod(Type type, string safeName, bool isStruct, string fullTypeName)
    {
        // Arrays don't have constructors - they're created via static methods or new T[size]
        if (type.IsArray)
        {
            AppendLine($"private static int {safeName}_new(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine($"luaL_error(L, \"Cannot directly construct arrays. Use array creation methods instead.\");");
                AppendLine("return 0;");
            }
            AppendLine("}");
            AppendLine();
            return;
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => !HasAttribute(c, nameof(LuaHiddenAttribute)) &&
                        !c.ContainsGenericParameters) // Skip constructors with generic parameters
            .OrderBy(c => c.GetParameters().Length)
            .ToList();

        AppendLine($"private static int {safeName}_new(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            AppendLine("var argCount = lua_gettop(L);");
            AppendLine();

            // Default constructor for structs
            if (isStruct)
            {
                AppendLine("if (argCount == 0)");
                AppendLine("{");
                using (Indent())
                {
                    AppendLine($"var obj = new {fullTypeName}();");
                    AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                    AppendLine("return 1;");
                }
                AppendLine("}");
                AppendLine();
            }

            // Group constructors by parameter count to handle overloads
            var ctorGroups = constructors
                .Where(c => !isStruct || c.GetParameters().Length > 0)
                .GroupBy(c => c.GetParameters().Length)
                .ToList();

            foreach (var group in ctorGroups)
            {
                var paramCount = group.Key;
                var ctorsForCount = group.ToList();

                AppendLine($"if (argCount == {paramCount})");
                AppendLine("{");
                using (Indent())
                {
                    if (ctorsForCount.Count == 1)
                    {
                        // Single constructor - no ambiguity
                        var ctor = ctorsForCount[0];
                        var parameters = ctor.GetParameters();
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            GenerateParameterRead(parameters[i], i);
                        }

                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));
                        AppendLine($"var obj = new {fullTypeName}({argList});");
                        AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                        AppendLine("return 1;");
                    }
                    else
                    {
                        // Multiple constructors with same arg count - need runtime disambiguation
                        // Sort: nullable constructors first (they have more specific conditions)
                        var sortedCtors = ctorsForCount.OrderByDescending(c =>
                            c.GetParameters().Any(p => IsNullableParameter(p) || IsNullable(p.ParameterType))
                        ).ToList();

                        bool hadNullableConstructor = false;
                        bool hadNonNullableConstructor = false;

                        for (int ctorIdx = 0; ctorIdx < sortedCtors.Count; ctorIdx++)
                        {
                            var ctor = sortedCtors[ctorIdx];
                            var parameters = ctor.GetParameters();

                            // Check if this constructor has any nullable parameters
                            var hasNullableParams = parameters.Any(p => IsNullableParameter(p) || IsNullable(p.ParameterType));

                            if (hasNullableParams)
                            {
                                // Generate condition to check if this overload matches
                                // A nullable constructor matches if at least one nil corresponds to a nullable param
                                var conditions = new List<string>();
                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    if (IsNullableParameter(parameters[i]) || IsNullable(parameters[i].ParameterType))
                                    {
                                        conditions.Add($"lua_isnil(L, {i + 1}) != 0");
                                    }
                                }

                                if (conditions.Any())
                                {
                                    var condition = string.Join(" || ", conditions);
                                    AppendLine($"if ({condition})");
                                    AppendLine("{");
                                    using (Indent())
                                    {
                                        for (int i = 0; i < parameters.Length; i++)
                                        {
                                            GenerateParameterRead(parameters[i], i);
                                        }

                                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));
                                        AppendLine($"var obj = new {fullTypeName}({argList});");
                                        AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                                        AppendLine("return 1;");
                                    }
                                    AppendLine("}");
                                    hadNullableConstructor = true;
                                }
                            }
                            else
                            {
                                // Non-nullable constructor
                                // Only generate the first non-nullable constructor as fallback
                                // (Multiple non-nullable overloads would need Lua type checking which is complex)
                                if (!hadNonNullableConstructor)
                                {
                                    if (hadNullableConstructor)
                                    {
                                        AppendLine("else");
                                        AppendLine("{");
                                        using (Indent())
                                        {
                                            for (int i = 0; i < parameters.Length; i++)
                                            {
                                                GenerateParameterRead(parameters[i], i);
                                            }

                                            var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));
                                            AppendLine($"var obj = new {fullTypeName}({argList});");
                                            AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                                            AppendLine("return 1;");
                                        }
                                        AppendLine("}");
                                    }
                                    else
                                    {
                                        for (int i = 0; i < parameters.Length; i++)
                                        {
                                            GenerateParameterRead(parameters[i], i);
                                        }

                                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));
                                        AppendLine($"var obj = new {fullTypeName}({argList});");
                                        AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                                        AppendLine("return 1;");
                                    }
                                    hadNonNullableConstructor = true;
                                }
                            }
                        }
                    }
                }
                AppendLine("}");
                AppendLine();
            }

            AppendLine($"luaL_error(L, \"Invalid arguments for {type.Name} constructor\");");
            AppendLine("return 0;");
        }
        AppendLine("}");
        AppendLine();
    }

    private void GenerateStaticMethods(Type type, string safeName, string fullTypeName)
    {
        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)))
            .GroupBy(m => GetLuaMethodName(m))
            .ToList();

        foreach (var methodGroup in staticMethods)
        {
            var methodName = methodGroup.Key;
            var overloads = methodGroup.OrderBy(m => m.GetParameters().Length).ToList();

            AppendLine($"private static int {safeName}_static_{GetSafeMethodName(methodName)}(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine("var argCount = lua_gettop(L);");
                AppendLine();

                foreach (var method in overloads)
                {
                    var parameters = method.GetParameters();
                    AppendLine($"if (argCount == {parameters.Length})");
                    AppendLine("{");
                    using (Indent())
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            GenerateParameterRead(parameters[i], i);
                        }

                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));

                        if (method.ReturnType == typeof(void))
                        {
                            AppendLine($"{fullTypeName}.{method.Name}({argList});");
                            AppendLine("return 0;");
                        }
                        else
                        {
                            AppendLine($"var result = {fullTypeName}.{method.Name}({argList});");
                            GeneratePushValue("result", method.ReturnType);
                            AppendLine("return 1;");
                        }
                    }
                    AppendLine("}");
                    AppendLine();
                }

                AppendLine($"luaL_error(L, \"Invalid arguments for {methodName}\");");
                AppendLine("return 0;");
            }
            AppendLine("}");
            AppendLine();
        }
    }

    private void GenerateInstanceMethods(Type type, string safeName, bool isStruct, string fullTypeName)
    {
        // Arrays don't need instance method wrappers - indexing is handled via __index/__newindex
        if (type.IsArray)
        {
            return;
        }

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName &&
                        !HasAttribute(m, nameof(LuaHiddenAttribute)) &&
                        !m.IsGenericMethod) // Skip generic methods
            .GroupBy(m => GetLuaMethodName(m))
            .ToList();

        foreach (var methodGroup in methods)
        {
            var methodName = methodGroup.Key;
            var overloads = methodGroup.OrderBy(m => m.GetParameters().Length).ToList();

            AppendLine($"private static int {safeName}_method_{GetSafeMethodName(methodName)}(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine("var argCount = lua_gettop(L) - 1; // First arg is self");
                AppendLine();

                if (isStruct)
                {
                    AppendLine($"var self = GetStructFromStack<{fullTypeName}>(L, 1);");
                }
                else
                {
                    AppendLine($"var self = GetObjectFromStack<{fullTypeName}>(L, 1);");
                    AppendLine("if (self == null)");
                    AppendLine("{");
                    using (Indent())
                    {
                        AppendLine($"luaL_error(L, \"Expected {type.Name} as first argument\");");
                        AppendLine("return 0;");
                    }
                    AppendLine("}");
                }
                AppendLine();

                foreach (var method in overloads)
                {
                    var parameters = method.GetParameters();
                    AppendLine($"if (argCount == {parameters.Length})");
                    AppendLine("{");
                    using (Indent())
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            GenerateParameterRead(parameters[i], i, 2);
                        }

                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));

                        if (method.ReturnType == typeof(void))
                        {
                            AppendLine($"self.{method.Name}({argList});");
                            if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                            AppendLine("return 0;");
                        }
                        else
                        {
                            AppendLine($"var result = self.{method.Name}({argList});");
                            if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                            GeneratePushValue("result", method.ReturnType);
                            AppendLine("return 1;");
                        }
                    }
                    AppendLine("}");
                    AppendLine();
                }

                AppendLine($"luaL_error(L, \"Invalid arguments for {methodName}\");");
                AppendLine("return 0;");
            }
            AppendLine("}");
            AppendLine();
        }
    }

    private void GenerateStaticPropertyAccessors(Type type, string safeName, string fullTypeName)
    {
        var staticProps = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)))
            .ToList();

        // __index for static properties
        AppendLine($"private static int {safeName}_type__index(lua_State L)");
        AppendLine("{");
        using (Indent())
        {
            AppendLine("var key = lua_tostring(L, 2);");
            AppendLine("if (key == null) { lua_pushnil(L); return 1; }");
            AppendLine();
            AppendLine("switch (key)");
            AppendLine("{");
            using (Indent())
            {
                foreach (var prop in staticProps.Where(p => p.CanRead))
                {
                    var luaName = GetLuaPropertyName(prop);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GeneratePushValue($"{fullTypeName}.{prop.Name}", prop.PropertyType);
                        AppendLine("return 1;");
                    }
                }

                AppendLine("default:");
                using (Indent())
                {
                    AppendLine("lua_rawget(L, 1);");
                    AppendLine("return 1;");
                }
            }
            AppendLine("}");
        }
        AppendLine("}");
        AppendLine();

        // __newindex for static properties (if any writable, excluding init-only)
        var writableProps = staticProps.Where(p => p.CanWrite && !IsInitOnly(p)).ToList();
        if (writableProps.Count > 0)
        {
            AppendLine($"private static int {safeName}_type__newindex(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine("var key = lua_tostring(L, 2);");
                AppendLine("if (key == null) return 0;");
                AppendLine();
                AppendLine("switch (key)");
                AppendLine("{");
                using (Indent())
                {
                    foreach (var prop in writableProps)
                    {
                        var luaName = GetLuaPropertyName(prop);
                        AppendLine($"case \"{luaName}\":");
                        using (Indent())
                        {
                            GenerateToObjectCode($"{fullTypeName}.{prop.Name}", prop.PropertyType, "3");
                            AppendLine("return 0;");
                        }
                    }

                    AppendLine("default:");
                    using (Indent())
                    {
                        AppendLine("lua_rawset(L, 1);");
                        AppendLine("return 0;");
                    }
                }
                AppendLine("}");
            }
            AppendLine("}");
            AppendLine();
        }
    }

    #region Helpers

    private static bool IsInitOnly(PropertyInfo property)
    {
        if (property.SetMethod == null) return false;
        return property.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
            .Any(t => t.Name == "IsExternalInit");
    }

    private static bool IsNullable(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    private static bool IsNullableParameter(ParameterInfo param)
    {
        // Check for Nullable<T> value types
        if (IsNullable(param.ParameterType))
            return true;

        // Check for nullable reference types using NullabilityInfo
        var context = new NullabilityInfoContext();
        var nullability = context.Create(param);
        return nullability.WriteState == NullabilityState.Nullable;
    }

    private void GeneratePushValue(string valueExpression, Type type)
    {
        if (IsNullable(type))
        {
            AppendLine($"if ({valueExpression}.HasValue)");
            using (Indent())
            {
                AppendLine($"PushValue(L, {valueExpression}.Value);");
            }
            AppendLine("else");
            using (Indent())
            {
                AppendLine("lua_pushnil(L);");
            }
        }
        else
        {
            AppendLine($"PushValue(L, {valueExpression});");
        }
    }

    private void GenerateToObjectCode(string targetExpression, Type type, string luaStackIndex)
    {
        var fullTypeName = GetFullTypeName(type);
        if (IsNullable(type))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            var underlyingTypeName = GetFullTypeName(underlyingType);
            AppendLine($"if (lua_isnil(L, {luaStackIndex}) != 0)");
            using (Indent())
            {
                AppendLine($"{targetExpression} = null;");
            }
            AppendLine("else");
            using (Indent())
            {
                AppendLine($"{targetExpression} = ToObject<{underlyingTypeName}>(L, {luaStackIndex})!;");
            }
        }
        else
        {
            AppendLine($"{targetExpression} = ToObject<{fullTypeName}>(L, {luaStackIndex})!;");
        }
    }

    private void GenerateParameterRead(ParameterInfo param, int index, int stackOffset = 1)
    {
        var varName = $"arg{index}";
        var stackIndex = index + stackOffset;
        var paramType = param.ParameterType;
        var fullTypeName = GetFullTypeName(paramType);

        if (IsNullable(paramType))
        {
            // Nullable value type (int?, float?, etc.)
            var underlyingType = Nullable.GetUnderlyingType(paramType)!;
            var underlyingTypeName = GetFullTypeName(underlyingType);
            AppendLine($"{fullTypeName} {varName};");
            AppendLine($"if (lua_isnil(L, {stackIndex}) != 0)");
            using (Indent())
            {
                AppendLine($"{varName} = null;");
            }
            AppendLine("else");
            using (Indent())
            {
                AppendLine($"{varName} = ToObject<{underlyingTypeName}>(L, {stackIndex})!;");
            }
        }
        else if (IsNullableParameter(param) && !paramType.IsValueType)
        {
            // Nullable reference type (string?, MyClass?, etc.)
            AppendLine($"{fullTypeName}? {varName};");
            AppendLine($"if (lua_isnil(L, {stackIndex}) != 0)");
            using (Indent())
            {
                AppendLine($"{varName} = null;");
            }
            AppendLine("else");
            using (Indent())
            {
                AppendLine($"{varName} = ToObject<{fullTypeName}>(L, {stackIndex})!;");
            }
        }
        else
        {
            AppendLine($"var {varName} = ToObject<{fullTypeName}>(L, {stackIndex})!;");
        }
    }

    private static string GetSafeTypeName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var rank = type.GetArrayRank();
            var suffix = rank == 1 ? "Array" : $"Array{rank}D";
            return GetSafeTypeName(elementType) + suffix;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            // Handle nested generic types like List<int>.Enumerator
            if (type.IsNested && type.DeclaringType != null)
            {
                var declaringName = GetSafeTypeName(type.DeclaringType);
                var nestedName = type.Name.Split('`')[0];

                // Get generic args that belong to this nested type only
                var nestedGenericArgs = type.GetGenericArguments()
                    .Skip(type.DeclaringType.GetGenericArguments().Length)
                    .ToArray();

                if (nestedGenericArgs.Length > 0)
                {
                    var argNames = string.Join("_", nestedGenericArgs.Select(GetSafeTypeName));
                    return $"{declaringName}_{nestedName}_{argNames}";
                }
                else
                {
                    return $"{declaringName}_{nestedName}";
                }
            }

            var baseName = type.Name.Split('`')[0];
            var genericArgs = type.GetGenericArguments();
            var argNames2 = string.Join("_", genericArgs.Select(GetSafeTypeName));
            return $"{baseName}_{argNames2}".Replace(".", "_").Replace("+", "_");
        }

        // Handle nested non-generic types
        if (type.IsNested && type.DeclaringType != null)
        {
            return GetSafeTypeName(type.DeclaringType) + "_" + type.Name;
        }

        return type.Name.Replace(".", "_").Replace("+", "_").Replace("`", "_");
    }

    private static string GetFullTypeName(Type type)
    {
        // Handle array types
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var rank = type.GetArrayRank();
            var brackets = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
            return GetFullTypeName(elementType) + brackets;
        }

        // Handle nullable types
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            return GetFullTypeName(underlyingType) + "?";
        }

        // Handle generic types
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();

            // Get the name, handling nested types
            var name = genericTypeDef.FullName ?? genericTypeDef.Name;

            // For nested types like List<T>.Enumerator, the name includes +
            // We need to handle the outer generic type first
            if (type.IsNested && type.DeclaringType != null && type.DeclaringType.IsGenericType)
            {
                // Build the declaring type with its generic arguments
                var declaringTypeName = GetFullTypeName(type.DeclaringType);

                // Get just the nested type's name (after the +)
                var nestedName = type.Name;
                var tickIndex = nestedName.IndexOf('`');
                if (tickIndex > 0)
                {
                    nestedName = nestedName.Substring(0, tickIndex);
                }

                // If the nested type itself has generic arguments, add them
                var nestedGenericArgs = type.GetGenericArguments().Skip(type.DeclaringType.GetGenericArguments().Length).ToArray();
                if (nestedGenericArgs.Length > 0)
                {
                    var argNames = string.Join(", ", nestedGenericArgs.Select(GetFullTypeName));
                    return $"{declaringTypeName}.{nestedName}<{argNames}>";
                }
                else
                {
                    return $"{declaringTypeName}.{nestedName}";
                }
            }

            // Remove the `1, `2, etc. from the name
            var tickIndex2 = name.IndexOf('`');
            if (tickIndex2 > 0)
            {
                name = name.Substring(0, tickIndex2);
            }

            var genericArgs = type.GetGenericArguments();
            var argNames2 = string.Join(", ", genericArgs.Select(GetFullTypeName));
            return $"{name}<{argNames2}>";
        }

        // Handle nested types (non-generic)
        if (type.IsNested && type.DeclaringType != null)
        {
            return GetFullTypeName(type.DeclaringType) + "." + type.Name;
        }

        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(void)) return "void";
        if (type == typeof(object)) return "object";
        return type.FullName ?? type.Name;
    }

    private static string GetSafeMethodName(string methodName) =>
        methodName.Replace(".", "_").Replace("+", "_").Replace("<", "_").Replace(">", "_");

    private static string GetLuaMethodName(MethodInfo method)
    {
        var luaNameAttr = method.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == nameof(LuaNameAttribute));
        if (luaNameAttr != null)
        {
            var nameArg = luaNameAttr.ConstructorArguments.FirstOrDefault();
            if (nameArg.Value is string name)
                return name;
        }
        return ToCamelCase(method.Name);
    }

    private static string GetLuaPropertyName(PropertyInfo prop)
    {
        var luaNameAttr = prop.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == nameof(LuaNameAttribute));
        if (luaNameAttr != null)
        {
            var nameArg = luaNameAttr.ConstructorArguments.FirstOrDefault();
            if (nameArg.Value is string name)
                return name;
        }
        return ToCamelCase(prop.Name);
    }

    private static string GetLuaFieldName(FieldInfo field)
    {
        var luaNameAttr = field.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == nameof(LuaNameAttribute));
        if (luaNameAttr != null)
        {
            var nameArg = luaNameAttr.ConstructorArguments.FirstOrDefault();
            if (nameArg.Value is string name)
                return name;
        }
        return ToCamelCase(field.Name.TrimStart('_'));
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static List<OperatorInfo> GetOperatorMethods(Type type)
    {
        var operators = new List<OperatorInfo>();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.IsSpecialName && m.Name.StartsWith("op_"));

        foreach (var method in methods)
        {
            operators.Add(new OperatorInfo(method.Name, method.GetParameters(), method));
        }

        return operators;
    }

    private static string? GetLuaMetamethodName(string operatorName) => operatorName switch
    {
        "op_Addition" => "__add",
        "op_Subtraction" => "__sub",
        "op_Multiply" => "__mul",
        "op_Division" => "__div",
        "op_Modulus" => "__mod",
        "op_UnaryNegation" => "__unm",
        "op_Equality" => "__eq",
        "op_LessThan" => "__lt",
        "op_LessThanOrEqual" => "__le",
        _ => null
    };

    private static string? GetOperatorSymbol(string operatorName) => operatorName switch
    {
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_Modulus" => "%",
        "op_UnaryNegation" => "-",
        "op_Equality" => "==",
        _ => null
    };

    #endregion
}

public record TypeInfo(Type Type, LuaVisibleAttribute Attribute)
{
    public string LuaName => Attribute.Name ?? Type.Name;
}

public record OperatorInfo(string Name, ParameterInfo[] Parameters, MethodInfo Method);
