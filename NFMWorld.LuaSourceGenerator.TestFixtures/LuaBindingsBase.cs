// Lua Bindings Base Infrastructure for Tests
// This is a copy of the binding infrastructure for use in the test project.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LuaNET.LuaJIT;
using Maxine.Extensions;
using Microsoft.Collections.Extensions;
using static LuaNET.LuaJIT.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.TestBindings;

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
                // For other types, push as userdata if we have a registered metatable
                if (TypeInfo<T>.Name is {} metatable)
                {
                    PushObject(L, value, metatable);
                }
                else
                {
                    throw new InvalidOperationException($"Type {typeof(T)} is not supported");
                    // Fallback: push as light userdata (pointer)
                    // var handle = GCHandle.Alloc(value);
                    // lua_pushlightuserdata(L, (nuint)GCHandle.ToIntPtr(handle));
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

    #region Type Registration

    /// <summary>
    /// Register a type's metatable name for use with PushValue.
    /// </summary>
    private static void RegisterMetatable<T>(string metatableName)
    {
        TypeInfo<T>.Name = metatableName;
    }

    #endregion

}
