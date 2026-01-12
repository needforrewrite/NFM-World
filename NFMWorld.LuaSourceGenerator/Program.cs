// Lua Source Generator
// This program uses reflection to find all types marked with [LuaVisible]
// and generates Lua binding code for them.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                            _assemblyPaths[assemblyName] = assemblyPath;
                        }
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
            var indent = new string(' ', _indent * 4);
            foreach (var range in line.AsSpan().Split('\n'))
            {
                _sb.Append(indent);
                _sb.AppendLine($"{line.AsSpan(range).TrimEnd()}");
            }
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
    /// Check if a type is a ref struct (cannot be marshalled to Lua).
    /// </summary>
    private static bool IsRefStruct(Type type)
    {
        return type.IsValueType && type.IsByRefLike;
    }

    /// <summary>
    /// Check if a type has the InlineArrayAttribute (indexable types without reflection-visible indexers).
    /// </summary>
    private static bool HasInlineArrayAttribute(Type type)
    {
        return type.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == "InlineArrayAttribute" || a.AttributeType.FullName == "System.Runtime.CompilerServices.InlineArrayAttribute");
    }

    /// <summary>
    /// Get the length of an inline array from its InlineArrayAttribute.
    /// Returns null if the type doesn't have the attribute.
    /// </summary>
    private static int? GetInlineArrayLength(Type type)
    {
        var attr = type.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "InlineArrayAttribute" || a.AttributeType.FullName == "System.Runtime.CompilerServices.InlineArrayAttribute");

        if (attr != null && attr.ConstructorArguments.Count > 0)
        {
            return (int)attr.ConstructorArguments[0].Value!;
        }

        return null;
    }

    /// <summary>
    /// Get the element type of an inline array by finding its first field.
    /// </summary>
    private static Type? GetInlineArrayElementType(Type type)
    {
        var field = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
        return field?.FieldType;
    }

    /// <summary>
    /// Check if a method has any byref parameters (ref, out, in).
    /// </summary>
    private static bool HasByRefParameters(MethodBase method)
    {
        return method.GetParameters().Any(p => p.ParameterType.IsByRef || IsRefStruct(p.ParameterType));
    }

    /// <summary>
    /// Check if a property has a ref return type.
    /// </summary>
    private static bool HasRefReturn(PropertyInfo property)
    {
        return property.PropertyType.IsByRef || IsRefStruct(property.PropertyType);
    }

    /// <summary>
    /// Check if a method has a ref return type.
    /// </summary>
    private static bool HasRefReturn(MethodInfo method)
    {
        return method.ReturnType.IsByRef || IsRefStruct(method.ReturnType);
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
                // Skip ref structs - they cannot be marshalled to Lua
                if (IsRefStruct(type))
                    continue;

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
            types.Add(new TypeInfo(type));
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

        // Skip nested types from system assemblies UNLESS they are from a constructed generic type
        // (like List<int>.Enumerator which is valid and needed)
        if (type.IsNested && type.Assembly != assembly)
        {
            // Allow nested types of constructed generics (e.g., List<int>.Enumerator)
            // The DeclaringType will be the generic definition (List`1), but if the nested type
            // itself has all generic parameters resolved, it means it's from a constructed generic
            bool isFromConstructedGeneric = type.DeclaringType != null &&
                                           type.DeclaringType.IsGenericType &&
                                           !type.ContainsGenericParameters;

            if (!isFromConstructedGeneric)
                return;
        }

        // Skip ref structs - they cannot be marshalled to Lua
        if (IsRefStruct(type))
            return;

        // For generic types, use the constructed generic type
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            discovered.Add(type);
            queue.Enqueue(type);
            additional.Add(type);
        }
        // For nested types of constructed generics (like List<int>.Enumerator)
        else if (type.IsNested && type.DeclaringType != null && type.DeclaringType.IsGenericType && !type.DeclaringType.IsGenericTypeDefinition)
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

        GenerateLuaBindingsBaseFile(outputDirectory, types);

        // Generate Lua stubs file with LuaCATS annotations
        GenerateLuaStubsFile(outputDirectory, types);
    }

    private void GenerateLuaBindingsBaseFile(string outputDirectory, IEnumerable<TypeInfo> types)
    {
        _sb.Clear();
        _indent = 0;

        AppendLine(
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

                // Global storage for all managed objects (non-generic to allow runtime type queries), and  their .NET types (for overload resolution)
                // Also track parent relationships for struct fields (structId -> (parentId, memberName, parentType))
                private static readonly DictionarySlim<int, (object Obj, Type Type, (int parentId, Action<object, object> updateStructInParent)? StructParents)> _objects = [];

                private static class TypeInfo<T>
                {
                    // Maps C# types to their Lua metatable names
                    public static string? Name;
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
                    _objects.Clear();
            """);

        _indent += 2;
        foreach (var typeInfo in types)
        {
            var type = typeInfo.Type;
            var fullTypeName = GetFullTypeName(type);
            AppendLine(
                $"""
                TypeInfo<{fullTypeName}>.Name = null;
                """
            );
        }
        _indent -= 2;

        AppendLine(
            $$"""
                }

                #region Object Storage

                private static int _objectCount = 0;

                /// <summary>
                /// Store a managed object and return its ID.
                /// </summary>
                private static int StoreObject<T>(T obj, in (int parentId, Action<object, object> updateStructInParent)? structParent = null)
                {
                    var id = _nextObjectId++;
                    _objects.GetOrAddValueRef(id) = (obj!, typeof(T), structParent);
                    _objectCount++;
                    return id;
                }

                /// <summary>
                /// Get a managed object by ID.
                /// </summary>
                private static T? GetObject<T>(int id)
                {
                    if (_objects.TryGetValue(id, out var obj) && obj.Obj is T typedObj)
                        return typedObj;
                    return default;
                }

                /// <summary>
                /// Remove a managed object by ID.
                /// </summary>
                private static void RemoveObject<T>(int id)
                {
                    _objects.Remove(id);
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
                /// Push a struct userdata with parent tracking (for field/property access).
                /// When the struct is modified, changes will be written back to the parent.
                /// </summary>
                private static void PushStructWithParent<T>(lua_State L, T obj, string metatableName, int parentId, Action<object, object> updateValueInParent) where T : struct
                {
                    var id = StoreObject(obj, (parentId, updateValueInParent));
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
                /// If the struct has a parent relationship, also writes it back to the parent's field.
                /// </summary>
                private static void UpdateStruct<T>(lua_State L, int idx, T value) where T : struct
                {
                    var ptr = lua_touserdata(L, idx);
                    if (ptr == 0) return;
                    unsafe
                    {
                        var id = *(int*)ptr;
                        ref var existingValue = ref _objects.GetOrAddValueRef(id);
                        existingValue = (value, typeof(T), existingValue.StructParents);

                        // If this struct has a parent, write it back to the parent's field
                        if (existingValue.StructParents is {} parentInfo)
                        {
                            var (parentId, updateStructInParent) = parentInfo;
                            if (_objects.TryGetValue(parentId, out var parentObj))
                            {
                                updateStructInParent(parentObj.Obj, value);
                            }
                        }
                    }
                }

                /// <summary>
                /// Get the .NET type of an object stored in Lua userdata at the given stack index.
                /// Returns null if the value is not userdata or the object is not found.
                /// </summary>
                private static Type? GetUserdataType(lua_State L, int idx)
                {
                    if (lua_type(L, idx) != LUA_TUSERDATA) return null;

                    var ptr = lua_touserdata(L, idx);
                    if (ptr == 0) return null;

                    unsafe
                    {
                        var id = *(int*)ptr;
                        return _objects.GetValueOrDefault(id).Type;
                    }
                }

                /// <summary>
                /// Get information about the type of value at a Lua stack position.
                /// Returns (luaType, dotnetType) where dotnetType is non-null only for userdata.
                /// </summary>
                private static (int luaType, Type? dotnetType) GetLuaStackValueType(lua_State L, int idx)
                {
                    var luaType = lua_type(L, idx);
                    Type? dotnetType = null;

                    if (luaType == LUA_TUSERDATA)
                    {
                        dotnetType = GetUserdataType(L, idx);
                    }

                    return (luaType, dotnetType);
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
            """
        );

        _indent += 3;

        foreach (var typeInfo in types)
        {
            var type = typeInfo.Type;
            var luaName = typeInfo.LuaName;
            var safeName = GetSafeTypeName(type);
            var metatableName = $"MT_{safeName}";
            var isStruct = type.IsValueType;
            var fullTypeName = GetFullTypeName(type);

            if (typeInfo.Type.IsArray)
            {
                AppendLine(
                    $"""
                    // Special case: Handle array type: {fullTypeName} since arrays need runtime type info
                    case {fullTypeName} arr_{luaName}:
                        PushObject(L, arr_{luaName}, "{metatableName}");
                        break;
                    """
                );
            }
        }

        _indent -= 3;

        AppendLine(
            $$"""
                            default:
                                // For all other types, push as userdata if we have a registered metatable
                                if (TypeInfo<T>.Name is {} metatable)
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

                        // Handle Lua tables being converted to arrays
                        if (luaType == LUA_TTABLE && typeof(T).IsArray)
                        {
                            #region Handle Lua tables being converted to arrays
                """);

        _indent += 3;

        foreach (var typeInfo in types)
        {
            if (!typeInfo.Type.IsArray || !typeInfo.Type.HasElementType || typeInfo.Type.GetArrayRank() != 1) continue;

            var elementType = typeInfo.Type.GetElementType()!;
            var fullElementTypeName = GetFullTypeName(elementType);
            var fullTypeName = GetFullTypeName(typeInfo.Type);

            AppendLine(
                $$"""
                if (typeof(T) == typeof({{fullTypeName}}))
                {
                    // Get the length of the table
                    var length = (int)lua_objlen(L, idx);

                    // Create the array
                    var array = new {{fullElementTypeName}}[length];

                    for (int i = 0; i < length; i++)
                    {
                        // Push table[i+1] onto stack (Lua arrays are 1-indexed)
                        lua_rawgeti(L, idx, i + 1);

                        // Convert the element
                        array[i] = ToObject<{{fullElementTypeName}}>(L, -1)!;

                        // Pop the element from stack
                        lua_pop(L, 1);
                    }

                    return (T)(object)array;
                }
                """);
        }

        _indent -= 3;

        AppendLine(
            $$"""
                        #endregion
                    }

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

                #region Overload Resolution

                /// <summary>
                /// Score how compatible a Lua value is with a .NET parameter type.
                /// Higher scores indicate better compatibility. -1 indicates incompatible.
                /// </summary>
                private static int ScoreParameterCompatibility<T>(lua_State L, int stackIdx)
                {
                    var (luaType, dotnetType) = GetLuaStackValueType(L, stackIdx);

                    // Nil can match nullable/reference types
                    if (luaType == LUA_TNIL)
                    {
                        return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null ? 0 : -1;
                    }

                    // Boolean matches
                    if (luaType == LUA_TBOOLEAN)
                    {
                        if (typeof(T) == typeof(bool)) return 100; // Exact match
                        return -1; // No implicit conversions from bool
                    }

                    // String matches
                    if (luaType == LUA_TSTRING)
                    {
                        if (typeof(T) == typeof(string)) return 100; // Exact match
                        return -1; // No implicit conversions from string
                    }

                    // Number matches (Lua numbers are always double)
                    if (luaType == LUA_TNUMBER)
                    {
                        // Check if the number is an integer value (no fractional part)
                        var numVal = lua_tonumber(L, stackIdx);
                        var isInteger = Math.Floor(numVal) == numVal;

                        if (isInteger)
                        {
                            // For integer values, check range compatibility and prefer appropriate integer types
                            // Priority: most appropriate integer type > double > float > other integer types
                            if (typeof(T) == typeof(int) && numVal >= int.MinValue && numVal <= int.MaxValue) return 100;
                            if (typeof(T) == typeof(long) && numVal >= long.MinValue && numVal <= long.MaxValue) return 100;
                            if (typeof(T) == typeof(double)) return 90;   // Can hold any integer
                            if (typeof(T) == typeof(float) && numVal >= float.MinValue && numVal <= float.MaxValue) return 85;
                            if (typeof(T) == typeof(uint) && numVal >= uint.MinValue && numVal <= uint.MaxValue) return 80;
                            if (typeof(T) == typeof(ulong) && numVal >= 0 && numVal <= ulong.MaxValue) return 80;
                            if (typeof(T) == typeof(short) && numVal >= short.MinValue && numVal <= short.MaxValue) return 75;
                            if (typeof(T) == typeof(ushort) && numVal >= ushort.MinValue && numVal <= ushort.MaxValue) return 75;
                            if (typeof(T) == typeof(byte) && numVal >= byte.MinValue && numVal <= byte.MaxValue) return 70;
                            if (typeof(T) == typeof(sbyte) && numVal >= sbyte.MinValue && numVal <= sbyte.MaxValue) return 70;
                            // Out of range for all integer types
                            if (typeof(T) == typeof(double)) return 90;
                            if (typeof(T) == typeof(float)) return 85;
                            return -1; // Can't fit in any numeric type
                        }
                        else
                        {
                            // For floating-point values, prefer floating-point types
                            // Priority: double > float > long > ulong > int > uint > other integer types
                            if (typeof(T) == typeof(double)) return 100; // Exact match to Lua's number type
                            if (typeof(T) == typeof(float)) return 90;   // Slight precision loss
                            if (typeof(T) == typeof(long)) return 80;    // Large range, signed
                            if (typeof(T) == typeof(ulong)) return 75;   // Large range, unsigned
                            if (typeof(T) == typeof(int)) return 70;     // Standard integer
                            if (typeof(T) == typeof(uint)) return 65;    // Unsigned variant
                            if (typeof(T) == typeof(short)) return 60;   // Smaller range
                            if (typeof(T) == typeof(ushort)) return 55;  // Smaller range, unsigned
                            if (typeof(T) == typeof(byte)) return 50;    // Smallest range
                            if (typeof(T) == typeof(sbyte)) return 45;   // Smallest range, signed
                        }
                        return -1; // Not a numeric type
                    }

                    // Table matches (can convert to 1D array)
                    if (luaType == LUA_TTABLE)
                    {
                        if (typeof(T).IsArray && typeof(T).GetArrayRank() == 1)
                        {
                            return 50; // Table can convert to array (but we can't check element types easily)
                        }
                        return -1; // Tables don't convert to other types
                    }

                    // Userdata matches (custom .NET types)
                    if (luaType == LUA_TUSERDATA && dotnetType != null)
                    {
                        if (typeof(T) == dotnetType) return 100; // Exact type match
                        if (typeof(T).IsAssignableFrom(dotnetType)) return 80; // Inheritance/interface match
                        return -1; // Type mismatch
                    }

                    return -1; // Unknown or incompatible
                }

                #endregion

                #region Delegate Management

                /// <summary>
                /// Keep a delegate alive to prevent GC collection while registered with Lua.
                /// </summary>
                private static lua_CFunction KeepAlive(lua_CFunction func)
                {
                    // Unnecessary, static delegates are not collected
                    // _delegates.Add(func);
                    return func;
                }

                #endregion

            }
            """
        );

        var filePath = Path.Combine(outputDirectory, "LuaBindings.Base.g.cs");
        File.WriteAllText(filePath, _sb.ToString());
    }

    private void GenerateLuaStubsFile(string outputDirectory, List<TypeInfo> types)
    {
        _sb.Clear();
        _indent = 0;

        AppendLine("---@meta");
        AppendLine();
        AppendLine("-- Auto-generated Lua stubs with LuaCATS annotations");
        AppendLine("-- Generated by NFMWorld.LuaSourceGenerator");
        AppendLine();

        foreach (var typeInfo in types)
        {
            GenerateLuaTypeStub(typeInfo);
            AppendLine();
        }

        var filePath = Path.Combine(outputDirectory, "LuaBindings.lua");
        File.WriteAllText(filePath, _sb.ToString());
    }

    private void GenerateLuaTypeStub(TypeInfo typeInfo)
    {
        var type = typeInfo.Type;
        var luaName = typeInfo.LuaName;
        var isStruct = type.IsValueType && !type.IsPrimitive && !type.IsEnum;
        var isArray = type.IsArray;

        // Generate class instance annotation
        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var baseTypeName = GetSafeTypeName(type.BaseType);
            AppendLine($"---@class (exact) {luaName}Instance : {baseTypeName}Instance");
        }
        else
        {
            AppendLine($"---@class (exact) {luaName}Instance");
        }

        // Generate field annotations for properties and fields
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(p => !p.IsSpecialName && !HasAttribute(p, nameof(LuaHiddenAttribute)) && !HasRefReturn(p))
            .ToList();

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(f => !f.IsSpecialName && !HasAttribute(f, nameof(LuaHiddenAttribute)) && !IsRefStruct(f.FieldType))
            .ToList();

        foreach (var prop in properties.Where(p => !p.GetMethod?.IsStatic ?? true))
        {
            var propLuaName = GetLuaPropertyName(prop);
            var propType = GetLuaStubTypeName(prop.PropertyType);
            AppendLine($"---@field {propLuaName} {propType}");
        }

        foreach (var field in fields.Where(f => !f.IsStatic))
        {
            var fieldLuaName = GetLuaFieldName(field);
            var fieldType = GetLuaStubTypeName(field.FieldType);
            AppendLine($"---@field {fieldLuaName} {fieldType}");
        }

        if (isArray)
        {
            var elementType = type.GetElementType()!;
            var elementTypeName = GetLuaStubTypeName(elementType);
            AppendLine($"---@field [integer] {elementTypeName}");
        }

        AppendLine($"local {luaName}Instance = {{}}");
        AppendLine();

        // Generate class annotation
        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var baseTypeName = GetSafeTypeName(type.BaseType);
            AppendLine($"---@class (exact) {luaName} : {baseTypeName}");
        }
        else
        {
            AppendLine($"---@class (exact) {luaName}");
        }

        // Generate static property accessor stubs
        foreach (var prop in properties.Where(p => p.GetMethod?.IsStatic ?? false))
        {
            var propLuaName = GetLuaPropertyName(prop);
            var propType = GetLuaStubTypeName(prop.PropertyType);
            AppendLine($"---@field {propLuaName} {propType}");
        }

        // Generate constructor stub
        if (!isArray)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(c => !HasAttribute(c, nameof(LuaHiddenAttribute)) && !HasByRefParameters(c))
                .ToList();

            if (constructors.Count > 0 || isStruct)
            {
                if (isStruct)
                {
                    AppendLine($"---Creates a new {luaName}");
                    AppendLine($"---@return {luaName}");
                    AppendLine($"function {luaName}.new() end");
                    AppendLine();
                }

                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();

                    AppendLine($"---Creates a new {luaName}");
                    foreach (var param in parameters)
                    {
                        var paramType = GetLuaStubTypeName(param.ParameterType);
                        var paramName = param.Name ?? $"param{param.Position}";
                        var optional = IsNullableParameter(param) ? "?" : "";
                        AppendLine($"---@param {paramName}{optional} {paramType}");
                    }
                    AppendLine($"---@return {GetLuaStubTypeName(typeInfo.Type)}");

                    var paramNames = string.Join(", ", parameters.Select(p => p.Name ?? $"param{p.Position}"));
                    AppendLine($"function {luaName}.new({paramNames}) end");
                    AppendLine();
                }
            }
        }
        else
        {
            // Array constructor - generate based on rank
            var rank = type.GetArrayRank();
            AppendLine($"---Creates a new {luaName}");

            if (rank == 1)
            {
                AppendLine($"---@param length integer");
            }
            else
            {
                for (int i = 0; i < rank; i++)
                {
                    AppendLine($"---@param dim{i} integer");
                }
            }

            AppendLine($"---@return {GetLuaStubTypeName(typeInfo.Type)}");

            if (rank == 1)
            {
                AppendLine($"function {luaName}.new(length) end");
            }
            else
            {
                var paramList = string.Join(", ", Enumerable.Range(0, rank).Select(i => $"dim{i}"));
                AppendLine($"function {luaName}.new({paramList}) end");
            }
            AppendLine();
        }

        // Generate static method stubs
        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)) && !HasByRefParameters(m) && !HasRefReturn(m))
            .ToList();

        foreach (var method in staticMethods)
        {
            var luaMethodName = GetLuaMethodName(method);
            var parameters = method.GetParameters();

            foreach (var param in parameters)
            {
                var paramType = GetLuaTypeName(param.ParameterType);
                var paramName = param.Name ?? $"param{param.Position}";
                var optional = IsNullableParameter(param) ? "?" : "";
                AppendLine($"---@param {paramName}{optional} {paramType}");
            }

            if (method.ReturnType != typeof(void))
            {
                var returnType = GetLuaTypeName(method.ReturnType);
                AppendLine($"---@return {returnType}");
            }

            var paramNames = string.Join(", ", parameters.Select(p => p.Name ?? $"param{p.Position}"));
            AppendLine($"function {luaName}.{luaMethodName}({paramNames}) end");
            AppendLine();
        }

        // Generate instance method stubs
        var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)) && !HasByRefParameters(m) && !HasRefReturn(m))
            .ToList();

        foreach (var method in instanceMethods)
        {
            var luaMethodName = GetLuaMethodName(method);
            var parameters = method.GetParameters();

            AppendLine($"---@param self {GetLuaStubTypeName(typeInfo.Type)}");
            foreach (var param in parameters)
            {
                var paramType = GetLuaStubTypeName(param.ParameterType);
                var paramName = param.Name ?? $"param{param.Position}";
                var optional = IsNullableParameter(param) ? "?" : "";
                AppendLine($"---@param {paramName}{optional} {paramType}");
            }

            if (method.ReturnType != typeof(void))
            {
                var returnType = GetLuaStubTypeName(method.ReturnType);
                AppendLine($"---@return {returnType}");
            }

            var paramNames = string.Join(", ", parameters.Select(p => p.Name ?? $"param{p.Position}"));
            AppendLine($"function {luaName}Instance:{luaMethodName}({paramNames}) end");
            AppendLine();
        }
    }

    private static string GetLuaStubTypeName(Type type)
    {
        // Primitive types
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";

        // Nullable types
        if (IsNullable(type))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            return GetLuaStubTypeName(underlyingType) + "?";
        }

        // Array types
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var elementTypeName = GetLuaStubTypeName(elementType);
            return $"{elementTypeName}[]";
        }

        // Other types - use the safe type name
        return GetSafeTypeName(type) + "Instance";
    }

    private string GetLuaTypeName(Type type)
    {
        // Primitive types
        if (type == typeof(void)) return "nil";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort))
            return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";

        // Nullable types
        if (IsNullable(type))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            return GetLuaTypeName(underlyingType) + "?";
        }

        // Array types
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var elementTypeName = GetLuaTypeName(elementType);
            return $"{elementTypeName}[]";
        }

        // Generic types
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            // For generic types, use the safe type name (e.g., List_Int32)
            return GetSafeTypeName(type);
        }

        // Other types - use the simple name or the safe type name
        if (type.IsPrimitive || type == typeof(object))
            return "any";

        return GetSafeTypeName(type);
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
                .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)) && !m.IsGenericMethod && !HasByRefParameters(m) && !HasRefReturn(m))
                .GroupBy(GetLuaMethodName)
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
                .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) && !HasRefReturn(p))
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
            .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) && !HasRefReturn(p))
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
                            !HasRefReturn(p) &&
                            p.CanRead &&
                            p.GetIndexParameters().Length > 0)
                .ToList();

            var isInlineArray = HasInlineArrayAttribute(type);
            var inlineArrayLength = isInlineArray ? GetInlineArrayLength(type) : null;
            var inlineArrayElementType = isInlineArray ? GetInlineArrayElementType(type) : null;

            if (isArray || indexers.Count > 0 || isInlineArray)
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
                    else if (isInlineArray && inlineArrayElementType != null)
                    {
                        // Handle inline array indexing
                        AppendLine("// Inline array indexing (single int index)");
                        AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                        if (inlineArrayLength.HasValue)
                        {
                            AppendLine($"if (index < 0 || index >= {inlineArrayLength.Value})");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("return luaL_error(L, \"Index out of range\");");
                            }
                            AppendLine("}");
                        }
                        var elementTypeName = GetFullTypeName(inlineArrayElementType);
                        AppendLine($"var span = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref System.Runtime.CompilerServices.Unsafe.As<{fullTypeName}, {elementTypeName}>(ref obj), {inlineArrayLength ?? 1});");
                        AppendLine("var element = span[index];");
                        GeneratePushValue("element", inlineArrayElementType);
                        AppendLine("return 1;");
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
                                !HasRefReturn(p) &&
                                p.CanRead &&
                                p.GetIndexParameters().Length == 0) // Skip indexers
                    .ToList();

                foreach (var prop in props)
                {
                    var luaName = GetLuaPropertyName(prop);
                    AppendLine($"case \"{luaName}\":");
                    using (Indent())
                    {
                        GeneratePushValueWithParentTracking($"{prop.Name}", prop.PropertyType, type, prop.Name, isStruct, prop.SetMethod == null);
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
                        GeneratePushValueWithParentTracking($"{field.Name}", field.FieldType, type, field.Name, isStruct, field.IsInitOnly);
                    }
                }

                // Instance methods
                // For array types, exclude methods declared on the array type itself (Get/Set/Address)
                // as well as methods from Array and object base classes
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName &&
                                !HasAttribute(m, nameof(LuaHiddenAttribute)) &&
                                !m.IsGenericMethod &&
                                !HasByRefParameters(m) &&
                                !HasRefReturn(m) &&
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
                            !HasRefReturn(p) &&
                            p.CanWrite &&
                            p.GetIndexParameters().Length > 0)
                .ToList();

            var isInlineArray = HasInlineArrayAttribute(type);
            var inlineArrayLength = isInlineArray ? GetInlineArrayLength(type) : null;
            var inlineArrayElementType = isInlineArray ? GetInlineArrayElementType(type) : null;

            if (isArray || indexers.Count > 0 || isInlineArray)
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
                    else if (isInlineArray && inlineArrayElementType != null)
                    {
                        // Handle inline array indexing
                        AppendLine("// Inline array indexing (single int index)");
                        AppendLine("var index = (int)lua_tointeger(L, 2) - 1; // Convert from 1-indexed to 0-indexed");
                        if (inlineArrayLength.HasValue)
                        {
                            AppendLine($"if (index < 0 || index >= {inlineArrayLength.Value})");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("return luaL_error(L, \"Index out of range\");");
                            }
                            AppendLine("}");
                        }
                        var elementTypeName = GetFullTypeName(inlineArrayElementType);
                        AppendLine($"var span = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref System.Runtime.CompilerServices.Unsafe.As<{fullTypeName}, {elementTypeName}>(ref obj), {inlineArrayLength ?? 1});");
                        AppendLine($"var value = ToObject<{elementTypeName}>(L, 3)!;");
                        AppendLine("span[index] = value;");
                        if (isStruct)
                        {
                            AppendLine("UpdateStruct(L, 1, obj);");
                        }
                        AppendLine("return 0;");
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
                                !HasRefReturn(p) &&
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

        // Group operators by Lua metamethod name and argument count
        var operatorGroups = operators
            .Where(op => GetLuaMetamethodName(op.Name) != null)
            .GroupBy(op => (metamethod: GetLuaMetamethodName(op.Name)!, argCount: op.Parameters.Length))
            .ToList();

        foreach (var group in operatorGroups)
        {
            var (luaMetamethod, argCount) = group.Key;
            var overloads = group.ToList();

            // Use first operator name as the method name (they should all map to the same metamethod)
            var operatorName = overloads[0].Name;

            AppendLine($"private static int {safeName}_op_{GetSafeMethodName(operatorName)}(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                if (overloads.Count == 1)
                {
                    // Single operator overload
                    var op = overloads[0];
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
                else
                {
                    // Multiple operator overloads with same argument count - use overload resolution
                    AppendLine("// Multiple operator overloads - find best match");
                    AppendLine("int bestScore = -1;");
                    AppendLine("int bestIndex = -1;");
                    AppendLine();

                    for (int opIdx = 0; opIdx < overloads.Count; opIdx++)
                    {
                        var op = overloads[opIdx];
                        var parameters = op.Parameters;

                        AppendLine($"// Try overload {opIdx}: {op.Name}({string.Join(", ", parameters.Select(p => GetFullTypeName(p.ParameterType)))})");
                        AppendLine("{");
                        using (Indent())
                        {
                            AppendLine("int score = 0;");

                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var paramTypeName = GetFullTypeName(parameters[i].ParameterType);
                                AppendLine($"int score{i} = ScoreParameterCompatibility<{paramTypeName}>(L, {i + 1});");
                                AppendLine($"if (score{i} < 0) goto next{opIdx};");
                                AppendLine($"else score += score{i};");
                            }

                            AppendLine("if (score > bestScore)");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine("bestScore = score;");
                                AppendLine($"bestIndex = {opIdx};");
                            }
                            AppendLine("}");
                        }
                        AppendLine("}");
                        AppendLine($"next{opIdx}:");
                        AppendLine();
                    }

                    // Now invoke the best match
                    AppendLine("switch (bestIndex)");
                    AppendLine("{");
                    using (Indent())
                    {
                        for (int opIdx = 0; opIdx < overloads.Count; opIdx++)
                        {
                            var op = overloads[opIdx];
                            var parameters = op.Parameters;

                            AppendLine($"case {opIdx}:");
                            using (Indent())
                            {
                                AppendLine("{");
                                using (Indent())
                                {
                                    if (parameters.Length == 2)
                                    {
                                        var leftType = GetFullTypeName(parameters[0].ParameterType);
                                        var rightType = GetFullTypeName(parameters[1].ParameterType);
                                        AppendLine($"var left = ToObject<{leftType}>(L, 1)!;");
                                        AppendLine($"var right = ToObject<{rightType}>(L, 2)!;");

                                        // Use the actual operator syntax
                                        var opSymbol = GetOperatorSymbol(op.Name);
                                        if (opSymbol != null && op.Name != "op_Equality")
                                        {
                                            AppendLine($"var result = left {opSymbol} right;");
                                            GeneratePushValue("result", op.Method.ReturnType);
                                        }
                                        else if (op.Name == "op_Equality")
                                        {
                                            AppendLine($"lua_pushboolean(L, left == right ? 1 : 0);");
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
                            }
                        }

                        AppendLine("default:");
                        using (Indent())
                        {
                            AppendLine($"luaL_error(L, \"No compatible operator overload found\");");
                            AppendLine("return 0;");
                        }
                    }
                    AppendLine("}");
                }
            }
            AppendLine("}");
            AppendLine();
        }
    }

    private void GenerateConstructorMethod(Type type, string safeName, bool isStruct, string fullTypeName)
    {
        // Arrays have special constructor logic - create array with specified dimensions
        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            var elementType = type.GetElementType()!;
            var elementTypeName = GetFullTypeName(elementType);

            AppendLine($"private static int {safeName}_new(lua_State L)");
            AppendLine("{");
            using (Indent())
            {
                AppendLine("var argCount = lua_gettop(L);");
                AppendLine();

                AppendLine($"if (argCount != {rank})");
                AppendLine("{");
                using (Indent())
                {
                    if (rank == 1)
                    {
                        AppendLine($"luaL_error(L, \"Expected 1 argument (length) for 1D array constructor\");");
                    }
                    else
                    {
                        AppendLine($"luaL_error(L, \"Expected {rank} arguments (dimensions) for {rank}D array constructor\");");
                    }
                    AppendLine("return 0;");
                }
                AppendLine("}");
                AppendLine();

                // Read dimension arguments
                for (int i = 0; i < rank; i++)
                {
                    AppendLine($"var dim{i} = (int)lua_tointeger(L, {i + 1});");
                    AppendLine($"if (dim{i} < 0)");
                    AppendLine("{");
                    using (Indent())
                    {
                        AppendLine($"luaL_error(L, \"Array dimension {i} must be non-negative\");");
                        AppendLine("return 0;");
                    }
                    AppendLine("}");
                }
                AppendLine();

                // Create the array
                if (rank == 1)
                {
                    AppendLine($"var array = new {elementTypeName}[dim0];");
                }
                else
                {
                    var dimList = string.Join(", ", Enumerable.Range(0, rank).Select(i => $"dim{i}"));
                    AppendLine($"var array = new {elementTypeName}[{dimList}];");
                }

                AppendLine($"PushObject(L, array, \"MT_{safeName}\");");
                AppendLine("return 1;");
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
                .Where(c => !HasByRefParameters(c) && (!isStruct || c.GetParameters().Length > 0))
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
                        AppendLine("try");
                        AppendLine("{");
                        using (Indent())
                        {
                            AppendLine($"var obj = new {fullTypeName}({argList});");
                            AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                            AppendLine("return 1;");
                        }
                        AppendLine("}");
                        AppendLine("catch (System.Exception ex)");
                        AppendLine("{");
                        using (Indent())
                        {
                            AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                            AppendLine("return 0;");
                        }
                        AppendLine("}");
                    }
                    else
                    {
                        // Multiple constructors with same argument count - use overload resolution
                        AppendLine("// Multiple constructors with same argument count - find best match");
                        AppendLine("int bestScore = -1;");
                        AppendLine("int bestIndex = -1;");
                        AppendLine();

                        for (int ctorIdx = 0; ctorIdx < ctorsForCount.Count; ctorIdx++)
                        {
                            var ctor = ctorsForCount[ctorIdx];
                            var parameters = ctor.GetParameters();

                            AppendLine($"// Try constructor {ctorIdx}: new {type.Name}({string.Join(", ", parameters.Select(p => GetFullTypeName(p.ParameterType)))})");
                            AppendLine("{");
                            using (Indent())
                            {
                                AppendLine($"int score = 0;");

                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    var paramTypeName = GetFullTypeName(parameters[i].ParameterType);
                                    AppendLine($"int score{i} = ScoreParameterCompatibility<{paramTypeName}>(L, {i + 1});");
                                    AppendLine($"if (score{i} < 0) goto next{ctorIdx};");
                                    AppendLine($"else score += score{i};");
                                }

                                AppendLine($"if (score > bestScore)");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"bestScore = score;");
                                    AppendLine($"bestIndex = {ctorIdx};");
                                }
                                AppendLine("}");
                            }
                            AppendLine("}");
                            AppendLine($"next{ctorIdx}:");
                            AppendLine();
                        }

                        // Now invoke the best match
                        AppendLine("switch (bestIndex)");
                        AppendLine("{");
                        using (Indent())
                        {
                            for (int ctorIdx = 0; ctorIdx < ctorsForCount.Count; ctorIdx++)
                            {
                                var ctor = ctorsForCount[ctorIdx];
                                var parameters = ctor.GetParameters();

                                AppendLine($"case {ctorIdx}:");
                                using (Indent())
                                {
                                    AppendLine("{");
                                    using (Indent())
                                    {
                                        for (int i = 0; i < parameters.Length; i++)
                                        {
                                            GenerateParameterRead(parameters[i], i);
                                        }

                                        var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));
                                        AppendLine("try");
                                        AppendLine("{");
                                        using (Indent())
                                        {
                                            AppendLine($"var obj = new {fullTypeName}({argList});");
                                            AppendLine($"PushObject(L, obj, \"MT_{safeName}\");");
                                            AppendLine("return 1;");
                                        }
                                        AppendLine("}");
                                        AppendLine("catch (System.Exception ex)");
                                        AppendLine("{");
                                        using (Indent())
                                        {
                                            AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace});");
                                            AppendLine("return 0;");
                                        }
                                        AppendLine("}");
                                    }
                                    AppendLine("}");
                                }
                            }

                            AppendLine("default:");
                            using (Indent())
                            {
                                AppendLine($"luaL_error(L, \"No compatible constructor found for {type.Name}\");");
                                AppendLine("return 0;");
                            }
                        }
                        AppendLine("}");
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
            .Where(m => !m.IsSpecialName && !HasAttribute(m, nameof(LuaHiddenAttribute)) && !HasByRefParameters(m) && !HasRefReturn(m))
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

                // Group overloads by argument count
                var overloadsByArgCount = overloads.GroupBy(m => m.GetParameters().Length).ToList();

                foreach (var argCountGroup in overloadsByArgCount)
                {
                    var argCount = argCountGroup.Key;
                    var methods = argCountGroup.ToList();

                    AppendLine($"if (argCount == {argCount})");
                    AppendLine("{");
                    using (Indent())
                    {
                        if (methods.Count == 1)
                        {
                            // Single overload - no resolution needed
                            var method = methods[0];
                            var parameters = method.GetParameters();

                            for (int i = 0; i < parameters.Length; i++)
                            {
                                GenerateParameterRead(parameters[i], i);
                            }

                            var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));

                            if (method.ReturnType == typeof(void))
                            {
                                AppendLine("try");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"{fullTypeName}.{method.Name}({argList});");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                                AppendLine("catch (System.Exception ex)");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                            }
                            else
                            {
                                AppendLine("try");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"var result = {fullTypeName}.{method.Name}({argList});");
                                    GeneratePushValue("result", method.ReturnType);
                                    AppendLine("return 1;");
                                }
                                AppendLine("}");
                                AppendLine("catch (System.Exception ex)");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                            }
                        }
                        else
                        {
                            // Multiple overloads with same argument count - use overload resolution
                            AppendLine("// Multiple overloads with same argument count - find best match");
                            AppendLine("int bestScore = -1;");
                            AppendLine("int bestIndex = -1;");
                            AppendLine();

                            for (int methodIdx = 0; methodIdx < methods.Count; methodIdx++)
                            {
                                var method = methods[methodIdx];
                                var parameters = method.GetParameters();

                                AppendLine($"// Try overload {methodIdx}: {method.Name}({string.Join(", ", parameters.Select(p => GetFullTypeName(p.ParameterType)))})");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"int score = 0;");

                                    for (int i = 0; i < parameters.Length; i++)
                                    {
                                        var paramTypeName = GetFullTypeName(parameters[i].ParameterType);
                                        AppendLine($"int score{i} = ScoreParameterCompatibility<{paramTypeName}>(L, {i + 1});");
                                        AppendLine($"if (score{i} < 0) goto next{methodIdx};");
                                        AppendLine($"else score += score{i};");
                                    }

                                    AppendLine($"if (score > bestScore)");
                                    AppendLine("{");
                                    using (Indent())
                                    {
                                        AppendLine($"bestScore = score;");
                                        AppendLine($"bestIndex = {methodIdx};");
                                    }
                                    AppendLine("}");
                                }
                                AppendLine("}");
                                AppendLine($"next{methodIdx}:");
                                AppendLine();
                            }

                            // Now invoke the best match
                            AppendLine("switch (bestIndex)");
                            AppendLine("{");
                            using (Indent())
                            {
                                for (int methodIdx = 0; methodIdx < methods.Count; methodIdx++)
                                {
                                    var method = methods[methodIdx];
                                    var parameters = method.GetParameters();

                                    AppendLine($"case {methodIdx}:");
                                    using (Indent())
                                    {
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
                                                AppendLine("try");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine($"{fullTypeName}.{method.Name}({argList});");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                                AppendLine("catch (System.Exception ex)");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                            }
                                            else
                                            {
                                                AppendLine("try");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine($"var result = {fullTypeName}.{method.Name}({argList});");
                                                    GeneratePushValue("result", method.ReturnType);
                                                    AppendLine("return 1;");
                                                }
                                                AppendLine("}");
                                                AppendLine("catch (System.Exception ex)");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                            }
                                        }
                                        AppendLine("}");
                                    }
                                }

                                AppendLine("default:");
                                using (Indent())
                                {
                                    AppendLine($"luaL_error(L, \"No compatible overload found for {methodName}\");");
                                    AppendLine("return 0;");
                                }
                            }
                            AppendLine("}");
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
                        !m.IsGenericMethod &&
                        !HasByRefParameters(m) &&
                        !HasRefReturn(m)) // Skip generic methods and byref parameters
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

                // Group overloads by argument count
                var overloadsByArgCount = overloads.GroupBy(m => m.GetParameters().Length).ToList();

                foreach (var argCountGroup in overloadsByArgCount)
                {
                    var argCount = argCountGroup.Key;
                    var methodsWithSameArgCount = argCountGroup.ToList();

                    AppendLine($"if (argCount == {argCount})");
                    AppendLine("{");
                    using (Indent())
                    {
                        if (methodsWithSameArgCount.Count == 1)
                        {
                            // Single overload - no resolution needed
                            var method = methodsWithSameArgCount[0];
                            var parameters = method.GetParameters();

                            for (int i = 0; i < parameters.Length; i++)
                            {
                                GenerateParameterRead(parameters[i], i, 2);
                            }

                            var argList = string.Join(", ", parameters.Select((_, i) => $"arg{i}"));

                            if (method.ReturnType == typeof(void))
                            {
                                AppendLine("try");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"self.{method.Name}({argList});");
                                    if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                                AppendLine("catch (System.Exception ex)");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                            }
                            else
                            {
                                AppendLine("try");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"var result = self.{method.Name}({argList});");
                                    if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                                    GeneratePushValue("result", method.ReturnType);
                                    AppendLine("return 1;");
                                }
                                AppendLine("}");
                                AppendLine("catch (System.Exception ex)");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                    AppendLine("return 0;");
                                }
                                AppendLine("}");
                            }
                        }
                        else
                        {
                            // Multiple overloads with same argument count - use overload resolution
                            AppendLine("// Multiple overloads with same argument count - find best match");
                            AppendLine("int bestScore = -1;");
                            AppendLine("int bestIndex = -1;");
                            AppendLine();

                            for (int methodIdx = 0; methodIdx < methodsWithSameArgCount.Count; methodIdx++)
                            {
                                var method = methodsWithSameArgCount[methodIdx];
                                var parameters = method.GetParameters();

                                AppendLine($"// Try overload {methodIdx}: {method.Name}({string.Join(", ", parameters.Select(p => GetFullTypeName(p.ParameterType)))})");
                                AppendLine("{");
                                using (Indent())
                                {
                                    AppendLine($"int score = 0;");

                                    for (int i = 0; i < parameters.Length; i++)
                                    {
                                        var paramTypeName = GetFullTypeName(parameters[i].ParameterType);
                                        AppendLine($"int score{i} = ScoreParameterCompatibility<{paramTypeName}>(L, {i + 2});");
                                        AppendLine($"if (score{i} < 0) goto next{methodIdx};");
                                        AppendLine($"else score += score{i};");
                                    }

                                    AppendLine($"if (score > bestScore)");
                                    AppendLine("{");
                                    using (Indent())
                                    {
                                        AppendLine($"bestScore = score;");
                                        AppendLine($"bestIndex = {methodIdx};");
                                    }
                                    AppendLine("}");
                                }
                                AppendLine("}");
                                AppendLine($"next{methodIdx}:");
                                AppendLine();
                            }

                            // Now invoke the best match
                            AppendLine("switch (bestIndex)");
                            AppendLine("{");
                            using (Indent())
                            {
                                for (int methodIdx = 0; methodIdx < methodsWithSameArgCount.Count; methodIdx++)
                                {
                                    var method = methodsWithSameArgCount[methodIdx];
                                    var parameters = method.GetParameters();

                                    AppendLine($"case {methodIdx}:");
                                    using (Indent())
                                    {
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
                                                AppendLine("try");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine($"self.{method.Name}({argList});");
                                                    if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                                AppendLine("catch (System.Exception ex)");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                            }
                                            else
                                            {
                                                AppendLine("try");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine($"var result = self.{method.Name}({argList});");
                                                    if (isStruct) AppendLine("UpdateStruct(L, 1, self);");
                                                    GeneratePushValue("result", method.ReturnType);
                                                    AppendLine("return 1;");
                                                }
                                                AppendLine("}");
                                                AppendLine("catch (System.Exception ex)");
                                                AppendLine("{");
                                                using (Indent())
                                                {
                                                    AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
                                                    AppendLine("return 0;");
                                                }
                                                AppendLine("}");
                                            }
                                        }
                                        AppendLine("}");
                                    }
                                }

                                AppendLine("default:");
                                using (Indent())
                                {
                                    AppendLine($"luaL_error(L, \"No compatible overload found for {methodName}\");");
                                    AppendLine("return 0;");
                                }
                            }
                            AppendLine("}");
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
            .Where(p => !HasAttribute(p, nameof(LuaHiddenAttribute)) && !HasRefReturn(p))
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

    private void GeneratePushValueWithParentTracking(string memberExpression, Type type, Type parentType, string memberName, bool isStruct, bool isReadOnly)
    {
        // Only use parent tracking for value types (structs)
        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum && !IsNullable(type))
        {
            var typeName = GetFullTypeName(type);
            var metatable = GetSafeTypeName(type);
            var parentTypeName = GetFullTypeName(parentType);

            // For structs, we need to get the ID from userdata on the stack
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
                        AppendLine("var parentId = *(int*)ptr;");
                        if (parentType.IsValueType)
                        {
                            AppendLine($"PushStructWithParent(L, obj.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => {{ System.Diagnostics.Debug.WriteLine($\"Attempted to assign value of struct {{obj}} ({{obj.GetType()}}) member '{memberExpression}' to {{value}} but Lua only owns a temporary value and there is no way to track it to its parent. Nothing will be set.\"); }});");
                        }
                        else if (isReadOnly)
                        {
                            AppendLine($"PushStructWithParent(L, obj.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => {{ System.Diagnostics.Debug.WriteLine($\"Attempted to assign value of struct {{obj}} ({{obj.GetType()}}) member '{memberExpression}' to {{value}} but the field is read-only. Nothing will be set.\"); }});");
                        }
                        else
                        {
                            AppendLine($"PushStructWithParent(L, obj.{memberExpression}, \"MT_{metatable}\", parentId, static (obj, value) => (({parentTypeName})obj).{memberExpression} = ({typeName})value);");
                        }
                    }
                    AppendLine("}");
                }
                AppendLine("}");
            }
            AppendLine("}");
            AppendLine("return 1;");
        }
        else
        {
            GeneratePushValue($"obj.{memberExpression}", type);
            AppendLine("return 1;");
        }
    }

    private void GenerateToObjectCode(string targetExpression, Type type, string luaStackIndex)
    {
        var fullTypeName = GetFullTypeName(type);
        AppendLine("try");
        AppendLine("{");
        using (Indent())
        {
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
        AppendLine("}");
        AppendLine("catch (System.Exception ex)");
        AppendLine("{");
        using (Indent())
        {
            AppendLine("luaL_error(L, $\"{ex.GetType().Name}: {ex.Message}\\n{ex.StackTrace}\");");
            AppendLine("return 0;");
        }
        AppendLine("}");
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
            if (type.IsNested && type.DeclaringType != null && type.DeclaringType.IsGenericType)
            {
                // For nested types of constructed generics (e.g., List<int>.Enumerator),
                // the DeclaringType is the generic definition (List`1), but the nested type
                // itself has the resolved generic arguments from the constructed parent.
                // We need to build the declaring type name using those arguments.
                var allGenericArgs = type.GetGenericArguments();
                var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;
                var declaringBaseName = type.DeclaringType.Name.Split('`')[0];
                var nestedName = type.Name.Split('`')[0];

                // Get generic args that belong to the declaring type
                var declaringTypeArgs = allGenericArgs.Take(declaringTypeArgCount).ToArray();

                // Get generic args that belong to this nested type only
                var nestedGenericArgs = allGenericArgs.Skip(declaringTypeArgCount).ToArray();

                // Build the declaring type name with its generic arguments
                string declaringName;
                if (declaringTypeArgs.Length > 0)
                {
                    var declaringArgNames = string.Join("_", declaringTypeArgs.Select(GetSafeTypeName));
                    declaringName = $"{declaringBaseName}_{declaringArgNames}";
                }
                else
                {
                    declaringName = declaringBaseName;
                }

                // Build the full name
                if (nestedGenericArgs.Length > 0)
                {
                    var nestedArgNames = string.Join("_", nestedGenericArgs.Select(GetSafeTypeName));
                    return $"{declaringName}_{nestedName}_{nestedArgNames}";
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
                // For nested types of constructed generics (e.g., List<int>.Enumerator),
                // the DeclaringType is the generic definition (List`1), but the nested type
                // itself has the resolved generic arguments from the constructed parent.
                // We need to reconstruct the declaring type with those arguments.
                var allGenericArgs = type.GetGenericArguments();
                var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;

                // Get the declaring type's base name and namespace
                var declaringNamespace = type.DeclaringType.Namespace;
                var declaringBaseName = type.DeclaringType.Name.Split('`')[0];

                // Build the full declaring type name with generic arguments
                string declaringTypeName;
                if (declaringTypeArgCount > 0 && allGenericArgs.Length >= declaringTypeArgCount)
                {
                    var declaringTypeArgs = allGenericArgs.Take(declaringTypeArgCount).ToArray();
                    var argNames = string.Join(", ", declaringTypeArgs.Select(GetFullTypeName));
                    declaringTypeName = $"{declaringNamespace}.{declaringBaseName}<{argNames}>";
                }
                else
                {
                    declaringTypeName = $"{declaringNamespace}.{declaringBaseName}";
                }

                // Get just the nested type's name (after the +)
                var nestedName = type.Name;
                var tickIndex = nestedName.IndexOf('`');
                if (tickIndex > 0)
                {
                    nestedName = nestedName.Substring(0, tickIndex);
                }

                // If the nested type itself has generic arguments beyond the declaring type's, add them
                var nestedGenericArgs = allGenericArgs.Skip(declaringTypeArgCount).ToArray();
                if (nestedGenericArgs.Length > 0)
                {
                    var nestedArgNames = string.Join(", ", nestedGenericArgs.Select(GetFullTypeName));
                    return $"{declaringTypeName}.{nestedName}<{nestedArgNames}>";
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
        "op_GreaterThan" => "__gt",
        "op_GreaterThanOrEqual" => "__ge",
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
        "op_LessThan" => "<",
        "op_LessThanOrEqual" => "<=",
        "op_GreaterThan" => ">",
        "op_GreaterThanOrEqual" => ">=",
        _ => null
    };

    #endregion
}

public partial record TypeInfo(Type Type, LuaVisibleAttribute? Attribute = null)
{
    public string LuaName => Attribute?.Name ?? GetGenericTypeLuaName(Type);

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

        // Handle nested types (like List<int>.Enumerator)
        if (type.IsNested && type.DeclaringType != null)
        {
            // For nested types of generic types, we need to reconstruct the parent's generic arguments
            // E.g., List<int>.Enumerator has DeclaringType = List`1, but we want "List_Int32"
            if (type.DeclaringType.IsGenericType)
            {
                // Get the generic arguments that apply to the declaring type
                // For List<int>.Enumerator, the Enumerator shares the same generic argument as List
                var declaringTypeArgs = type.GetGenericArguments();
                var declaringTypeArgCount = type.DeclaringType.GetGenericArguments().Length;

                // Build the declaring type name with its generic arguments
                var declaringBaseName = type.DeclaringType.Name.Split('`')[0];
                if (declaringTypeArgCount > 0 && declaringTypeArgs.Length >= declaringTypeArgCount)
                {
                    var declaringArgNames = string.Join("_", declaringTypeArgs.Take(declaringTypeArgCount).Select(t => GetSimpleTypeName(t)));
                    var declaringName = $"{declaringBaseName}_{declaringArgNames}";
                    return $"{declaringName}_{type.Name}";
                }
                else
                {
                    return $"{declaringBaseName}_{type.Name}";
                }
            }
            else
            {
                var declaringName = GetGenericTypeLuaName(type.DeclaringType);
                return $"{declaringName}_{type.Name}";
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
}

public record OperatorInfo(string Name, ParameterInfo[] Parameters, MethodInfo Method);
