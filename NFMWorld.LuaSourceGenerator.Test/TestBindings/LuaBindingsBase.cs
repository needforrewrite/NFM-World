// Lua Bindings Base Infrastructure for Tests
// This is a copy of the binding infrastructure for use in the test project.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LuaNET.LuaJIT;
using static LuaNET.LuaJIT.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.TestBindings;

public partial class LuaBindings
{
    // Storage for managed objects referenced by Lua userdata
    private static readonly Dictionary<int, object> _objects = new();
    private static int _nextObjectId = 1;

    // Maps C# types to their Lua metatable names
    private static readonly Dictionary<Type, string> _typeMetatables = new();

    // Keep delegates alive to prevent GC collection
    private static readonly List<lua_CFunction> _delegates = new();

    /// <summary>
    /// Reset the bindings state (for test isolation).
    /// </summary>
    public static void Reset()
    {
        _objects.Clear();
        _nextObjectId = 1;
        _typeMetatables.Clear();
        _delegates.Clear();
    }

    #region Object Storage

    /// <summary>
    /// Store a managed object and return its ID.
    /// </summary>
    private static int StoreObject(object obj)
    {
        var id = _nextObjectId++;
        _objects[id] = obj;
        return id;
    }

    /// <summary>
    /// Get a managed object by ID.
    /// </summary>
    private static object? GetObject(int id)
    {
        return _objects.TryGetValue(id, out var obj) ? obj : null;
    }

    /// <summary>
    /// Remove a managed object by ID.
    /// </summary>
    private static void RemoveObject(int id)
    {
        _objects.Remove(id);
    }

    /// <summary>
    /// Get the current object count (for testing).
    /// </summary>
    public static int ObjectCount => _objects.Count;

    #endregion

    #region Lua Stack Operations

    /// <summary>
    /// Push a userdata for a managed object onto the Lua stack.
    /// </summary>
    private static void PushObject(lua_State L, object obj, string metatableName)
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
            return GetObject(id) as T;
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
            var obj = GetObject(id);
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
            _objects[id] = value;
        }
    }

    /// <summary>
    /// Push a value to Lua stack based on its runtime type.
    /// </summary>
    private static void PushValue(lua_State L, object? value)
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
            case long l:
                lua_pushinteger(L, l);
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
                // For other types, push as userdata if we have a registered metatable
                var type = value.GetType();
                if (_typeMetatables.TryGetValue(type, out var metatable))
                {
                    PushObject(L, value, metatable);
                }
                else
                {
                    // Fallback: push as light userdata (pointer)
                    var handle = GCHandle.Alloc(value);
                    lua_pushlightuserdata(L, (nuint)GCHandle.ToIntPtr(handle));
                }
                break;
        }
    }

    /// <summary>
    /// Convert Lua value at stack index to a C# object of the target type.
    /// </summary>
    private static object? ToObject(lua_State L, int idx, Type targetType)
    {
        var luaType = lua_type(L, idx);

        if (luaType == LUA_TNIL) return null;

        if (targetType == typeof(bool) || luaType == LUA_TBOOLEAN)
            return lua_toboolean(L, idx) != 0;

        if (targetType == typeof(int))
            return (int)lua_tointeger(L, idx);

        if (targetType == typeof(long))
            return lua_tointeger(L, idx);

        if (targetType == typeof(float))
            return (float)lua_tonumber(L, idx);

        if (targetType == typeof(double))
            return lua_tonumber(L, idx);

        if (targetType == typeof(string) || luaType == LUA_TSTRING)
            return lua_tostring(L, idx);

        if (luaType == LUA_TUSERDATA)
        {
            var ptr = lua_touserdata(L, idx);
            if (ptr != 0)
            {
                unsafe
                {
                    var id = *(int*)ptr;
                    return GetObject(id);
                }
            }
        }

        return null;
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

    #region Type Registration

    /// <summary>
    /// Register a type's metatable name for use with PushValue.
    /// </summary>
    private static void RegisterMetatable(Type type, string metatableName)
    {
        _typeMetatables[type] = metatableName;
    }

    #endregion
}
