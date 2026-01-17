using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace LuaJIT;

using size_t = nuint;
using lua_Number = double;
using lua_Integer = nint;

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

    public static void lua_pushliteral(lua_State L, ReadOnlySpan<byte> s)
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
        lua_getfield(L, LUA_GLOBALSINDEX, name);
    }

    /// <summary>
    /// Set global variable by name.
    /// </summary>
    public static void lua_setglobal(lua_State L, ReadOnlySpan<byte> name)
    {
        lua_setfield(L, LUA_GLOBALSINDEX, name);
    }

    /// <summary>
    /// Push a C function without upvalues.
    /// </summary>
    public static void lua_pushcfunction(lua_State L, delegate* unmanaged[Cdecl]<lua_State, int> f)
    {
        lua_pushcclosure(L, f, 0);
    }

    #endregion

    public static double lua_version(lua_State L)
    {
        var mem = _lua_version(L);
        if (mem == null)
            return 0;
        return *mem;
    }
	
    public static void luaL_argcheck(lua_State L, bool cond, int numarg, string extramsg)
    {
        if (cond == false)
            luaL_argerror(L, numarg, extramsg);
    }
	
    public static string? luaL_checkstring(lua_State L, int n)
    {
        return luaL_checklstring(L, n, out _);
    }
	
    public static string? luaL_optstring(lua_State L, int n, string d)
    {
        return luaL_optlstring(L, n, d, out _);
    }
	
    public static int luaL_checkint(lua_State L, int n)
    {
        return (int) luaL_checkinteger(L, n);
    }

    public static int luaL_optint(lua_State L, int n, lua_Integer d)
    {
        return (int) luaL_optinteger(L, n, d);
    }
	
    public static long luaL_checklong(lua_State L, int n)
    {
        return luaL_checkinteger(L, n);
    }
	
    public static long luaL_optlong(lua_State L, int n, lua_Integer d)
    {
        return luaL_optinteger(L, n, d);
    }
    public static string? luaL_typename(lua_State L, int i)
    {
        return lua_typename(L, lua_type(L, i));
    }
	
    public static void luaL_getmetatable(lua_State L, string n)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, n);
    }
    
    public delegate T luaL_Function<T>(lua_State L, int n);
	
    public static T luaL_opt<T>(lua_State L, luaL_Function<T> f, int n, T d)
    {
        return lua_isnoneornil(L, n) > 0 ? d : f(L, n);
    }
	
    public static void luaL_newlibtable(lua_State L, luaL_Reg* l)
    {
        int n = 0;
        for (luaL_Reg* curr = l; curr->name != null; curr++)
            n++;
        lua_createtable(L, 0, n);
    }
	
    public static void luaL_newlib(lua_State L, luaL_Reg* l)
    {
        luaL_newlibtable(L, l);
        luaL_setfuncs(L, l, 0);
    }
	
    public static void luaL_addchar(luaL_Buffer* B, sbyte c)
    {
        if (B->p >= &B->buffer + LUAL_BUFFERSIZE)
            luaL_prepbuffer(B);
        *(B->p) = c;
        B->p++;
    }
	
    public static void luaL_putchar(luaL_Buffer* B, sbyte c)
    {
        luaL_addchar(B, c);
    }
	
    public static void luaL_addsize(luaL_Buffer* B, nint n)
    {
        B->p += n;
    }
    
    #region Lua 5.2/5.3 Compatibility Helpers
    // https://github.com/lunarmodules/lua-compat-5.3
    
    /// <summary>
    /// Converts a possibly negative stack index into an absolute index. Implemented as abs_index(L, i) macro in
    /// lauxlib.c.
    /// </summary>
    /// <param name="L"></param>
    /// <param name="i"></param>
    /// <returns></returns>
    public static int lua_absindex(lua_State L, int i)
    {
        return i is > 0 or <= LUA_REGISTRYINDEX ? i : lua_gettop(L) + i + 1;
    }

    public static ulong lua_rawlen(lua_State L, int i)
    {
        return lua_objlen(L, i);
    }
    
    public static void* lua_tolightuserdata(lua_State L, int n)
    {
        return lua_touserdata(L, n);
    }
    
    public static void lua_pushunsigned(lua_State L, nuint n)
    {
        lua_pushinteger(L, (lua_Integer)n);
    }
    
    public static nuint lua_tounsigned(lua_State L, int n, nuint* isnum)
    {
        return lua_tounsignedx(L, n, null);
    }
    
    public static nuint lua_tounsignedx(lua_State L, int n, int* isnum)
    {
        return (nuint) lua_tointegerx(L, n, isnum);
    }
    
    public static nuint luaL_checkunsigned(lua_State L, int n)
    {
        return (nuint) luaL_checkinteger(L, n);
    }
    
    public static nuint luaL_optunsigned(lua_State L, int n, nuint d)
    {
        return (nuint) luaL_optinteger(L, n, (lua_Integer)d);
    }
    
    public static void lua_getuservalue(lua_State L, int i)
    {
        lua_getfenv(L, i);
        lua_type(L, -1);
    }
    
    public static void lua_setuservalue(lua_State L, int i)
    {
        luaL_checktype(L, -1, LUA_TTABLE);
        lua_setfenv(L, i);
    }
    
    public static void lua_pushglobaltable(lua_State L)
    {
        lua_pushvalue(L, LUA_GLOBALSINDEX);
    }
    
    #endregion
}