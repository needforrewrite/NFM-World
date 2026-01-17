using System.Runtime.InteropServices;

namespace LuaJIT;

using size_t = nuint;
using lua_Number = double;
using lua_Integer = long;

public static unsafe partial class Methods
{
    public static void lua_pop(lua_State L, int n)
    {
        lua_settop(L, -n - 1);
    }

    public static void lua_newtable(lua_State L)
    {
        lua_createtable(L, 0, 0);
    }

    public static void lua_register(lua_State L, string n,
        [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> f)
    {
        lua_pushcfunction(L, f);
        lua_setglobal(L, n);
    }

    public static ulong lua_strlen(lua_State L, int i)
    {
        return lua_objlen(L, i);
    }

    public static int lua_isfunction(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TFUNCTION) ? 1 : 0;
    }

    public static int lua_istable(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TTABLE) ? 1 : 0;
    }

    public static int lua_islightuserdata(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TLIGHTUSERDATA) ? 1 : 0;
    }

    public static int lua_isnil(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TNIL) ? 1 : 0;
    }

    public static int lua_isboolean(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TBOOLEAN) ? 1 : 0;
    }

    public static int lua_isthread(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TTHREAD) ? 1 : 0;
    }

    public static int lua_isnone(lua_State L, int n)
    {
        return (lua_type(L, n) == LUA_TNONE) ? 1 : 0;
    }

    public static int lua_isnoneornil(lua_State L, int n)
    {
        return (lua_type(L, n) <= 0) ? 1 : 0;
    }

    public static void lua_pushliteral(lua_State L, string s)
    {
        lua_pushlstring(L, s);
    }

    public static void lua_setglobal(lua_State L, string s)
    {
        lua_setfield(L, LUA_GLOBALSINDEX, s);
    }

    public static void lua_getglobal(lua_State L, string s)
    {
        lua_getfield(L, LUA_GLOBALSINDEX, s);
    }

    public static lua_State lua_open()
    {
        return luaL_newstate();
    }

    public static void lua_getregistry(lua_State L)
    {
        lua_pushvalue(L, LUA_REGISTRYINDEX);
    }

    public static int lua_getgccount(lua_State L)
    {
        return lua_gc(L, LUA_GCCOUNT, 0);
    }

    #region Common Convenience Helpers

    /// <summary>
    /// Execute a Lua string. Equivalent to luaL_dostring macro.
    /// Returns 0 on success, non-zero on error (with error message on stack).
    /// </summary>
    public static int luaL_dostring(lua_State L, string str)
    {
        var result = luaL_loadstring(L, str);
        if (result != 0) return result;
        return Methods.lua_pcall(L, 0, -1, 0);
    }

    /// <summary>
    /// Execute a Lua string. Equivalent to luaL_dostring macro.
    /// Returns 0 on success, non-zero on error (with error message on stack).
    /// </summary>
    public static int luaL_dostring(lua_State L, ReadOnlySpan<byte> str)
    {
        var result = luaL_loadstring(L, str);
        if (result != 0) return result;
        return Methods.lua_pcall(L, 0, -1, 0);
    }

    /// <summary>
    /// Execute a Lua file. Equivalent to luaL_dofile macro.
    /// Returns 0 on success, non-zero on error (with error message on stack).
    /// </summary>
    public static int luaL_dofile(lua_State L, string filename)
    {
        var result = luaL_loadfile(L, filename);
        if (result != 0) return result;
        return Methods.lua_pcall(L, 0, -1, 0);
    }

    /// <summary>
    /// Execute a Lua file. Equivalent to luaL_dofile macro.
    /// Returns 0 on success, non-zero on error (with error message on stack).
    /// </summary>
    public static int luaL_dofile(lua_State L, ReadOnlySpan<byte> filename)
    {
        var result = luaL_loadfile(L, filename);
        if (result != 0) return result;
        return Methods.lua_pcall(L, 0, -1, 0);
    }

    /// <summary>
    /// Get global variable by name.
    /// </summary>
    public static void lua_getglobal(lua_State L, ReadOnlySpan<byte> name)
    {
        lua_getfield(L, -10002, name); // LUA_GLOBALSINDEX = -10002
    }

    /// <summary>
    /// Set global variable by name.
    /// </summary>
    public static void lua_setglobal(lua_State L, ReadOnlySpan<byte> name)
    {
        lua_setfield(L, -10002, name); // LUA_GLOBALSINDEX = -10002
    }

    /// <summary>
    /// Push a C function without upvalues.
    /// </summary>
    public static void lua_pushcfunction(lua_State L, delegate* unmanaged[Cdecl]<lua_State, int> f)
    {
        Methods.lua_pushcclosure(L, f, 0);
    }

    #endregion

    public static double lua_version(lua_State L)
    {
        var mem = _lua_version(L);
        if (mem == null)
            return 0;
        return *mem;
    }
}