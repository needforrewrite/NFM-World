// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Maxine.Extensions;
using nfm_world_library.backend;
using nfm_world_library.backend.gamemodes;
using nfm_world_library.mad;

namespace nfm_world_library.Lua;

public static class LuaManager
{
    private delegate int lua_CFunction(lua_State L);
    
    public static lua_State L;

    public static unsafe void InitializeLua()
    {
        L = luaL_newstate();

        // Load standard libraries
        // https://stackoverflow.com/a/4552146
        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State, int>)Marshal.GetFunctionPointerForDelegate<lua_CFunction>(luaopen_base));
        lua_pushliteral(L, "");
        lua_call(L, 1, 0);

        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State, int>)Marshal.GetFunctionPointerForDelegate<lua_CFunction>(luaopen_table));
        lua_pushliteral(L, LUA_TABLIBNAME);
        lua_call(L, 1, 0);

        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State, int>)Marshal.GetFunctionPointerForDelegate<lua_CFunction>(luaopen_string));
        lua_pushliteral(L, LUA_STRLIBNAME);
        lua_call(L, 1, 0);

        lua_pushcfunction(L, (delegate* unmanaged[Cdecl]<lua_State, int>)Marshal.GetFunctionPointerForDelegate<lua_CFunction>(luaopen_math));
        lua_pushliteral(L, LUA_MATHLIBNAME);
        lua_call(L, 1, 0);

        LuaBindings.Initialize(L);

        // Expose print function
        lua_pushcfunction(L, &luaB_print);
        lua_setglobal(L, "print");
    }
    
    /*
     ** If your system does not support `stdout', you can just remove this function.
     ** If you need, you can define your own `print' function, following this
     ** model but changing `fputs' to put the strings at a proper place
     ** (a console window or a log file, for instance).
     */
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int luaB_print(lua_State L) {
        int n = lua_gettop(L);  /* number of arguments */
        int i;
        lua_getglobal(L, "tostring");
        var sb = new StringBuilder();
        for (i=1; i<=n; i++) {
            lua_pushvalue(L, -1);  /* function to be called */
            lua_pushvalue(L, i);   /* value to print */
            lua_call(L, 1, 1);
            if (i>1) sb.Append('\t');
            var s = lua_tostringintostringbuilder(L, -1, sb) /* get result */;
            if (!s)
                return luaL_error(L, "'tostring' must return a string to 'print'");
            lua_pop(L, 1);  /* pop result */
        }
        Console.WriteLine(sb.ToString());
        return 0;
    }

    public static unsafe bool lua_tostringintostringbuilder(lua_State L, int idx, StringBuilder sb)
    {
        nuint len;
        var strPtr = lua_tolstring(L, idx, &len);
        if (strPtr == null)
        {
            return false;
        }

        sb.Append(new Span<byte>(strPtr, (int)len), Encoding.ASCII);
        return true;
    }
    
    public static LuaTable LoadLuaWithContext<T>(string path, IReadOnlyDictionary<string, T> contextVariables)
    {
        if (luaL_loadfile(L, path) != LUA_OK)
        {
            throw new LuaException(lua_tostring(L, -1) ?? "Unknown Lua error");
        }

        /* Create environment table */
        lua_newtable(L);
        // Stack: [chunk, env]

        /* Add context variables */
        foreach (var (key, value) in contextVariables)
        {
            LuaBindings.PushValue(L, value);
            lua_setfield(L, -2, key);
        }
        // Stack: [chunk, env]

        /* Create metatable with __index = _G for fallback lookups */
        lua_newtable(L);              // Stack: [chunk, env, mt]
        lua_getglobal(L, "_G");       // Stack: [chunk, env, mt, _G]
        lua_setfield(L, -2, "__index"); // Stack: [chunk, env, mt]
        lua_setmetatable(L, -2);      // Stack: [chunk, env]

        /* Save reference to environment table before setfenv pops it */
        lua_pushvalue(L, -1);         // Stack: [chunk, env, env]
        int envTableRef = luaL_ref(L, LUA_REGISTRYINDEX);
        // Stack: [chunk, env]

        /* Verify chunk is a function before setfenv */
        int chunkType = lua_type(L, -2);
        Console.WriteLine($"Chunk type: {chunkType} (should be {LUA_TFUNCTION})");

        /* Set the environment on the chunk */
        int setfenvResult = lua_setfenv(L, -2);
        Console.WriteLine($"setfenv result: {setfenvResult} (should be 1)");
        // Stack: [chunk]

        /* Run the chunk */
        if (lua_pcall(L, 0, 0, 0) != LUA_OK)
        {
            throw new LuaException(lua_tostring(L, -1) ?? "Unknown Lua error");
        }
        
        /* Debug: check what's in the env table */
        lua_rawgeti(L, LUA_REGISTRYINDEX, envTableRef);
        lua_pushnil(L);
        Console.WriteLine("Environment table contents:");
        while (lua_next(L, -2) != 0)
        {
            string key = lua_tostring(L, -2) ?? "(non-string key)";
            int valType = lua_type(L, -1);
            Console.WriteLine($"  {key} = <{lua_typename(L, valType)}>");
            lua_pop(L, 1);
        }
        lua_pop(L, 1);
        
        /* Get the environment table */
        return new LuaTable(L, envTableRef);
    }

    public static void Destroy(lua_State luaState)
    {
        LuaBindings.CleanupEventDelegates();
        lua_close(luaState);
    }
}