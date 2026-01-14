// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;
using nfm_world_library.backend;
using nfm_world_library.backend.gamemodes;

namespace nfm_world_library.Lua;

public static class LuaManager
{
    public static void InitializeLua(out lua_State L)
    {
        L = luaL_newstate();

        // Load standard libraries
        // https://stackoverflow.com/a/4552146
        lua_pushcfunction(L, luaopen_base);
        lua_pushliteral(L, "");
        lua_call(L, 1, 0);

        lua_pushcfunction(L, luaopen_table);
        lua_pushliteral(L, LUA_TABLIBNAME);
        lua_call(L, 1, 0);
        
        lua_pushcfunction(L, luaopen_string);
        lua_pushliteral(L, LUA_STRLIBNAME);
        lua_call(L, 1, 0);

        lua_pushcfunction(L, luaopen_math);
        lua_pushliteral(L, LUA_MATHLIBNAME);
        lua_call(L, 1, 0);

        LuaBindings.Initialize(L);
        
        // Expose print function
        LuaBindings.DefineGlobalFunction(L, "print", (string str) =>
        {
            Console.WriteLine("[Lua] " + str);
        });
    }
    
    public static lua_State LoadGamemodeLua(LuaGamemode gm, string gamemodeLuaPath)
    {
        InitializeLua(out var L);
        
        LuaBindings.CleanupEventDelegates();
        
        LuaBindings.DefineGlobalVariable(L, "GM", gm);

        if (luaL_dofile(L, gamemodeLuaPath) != LUA_OK)
        {
            string error = lua_tostring(L, -1) ?? "Unknown error";
            throw new Exception($"Error loading gamemode Lua file: {error}");
        }

        return L;
    }

    public static void Destroy(lua_State luaState)
    {
        LuaBindings.CleanupEventDelegates();
        lua_close(luaState);
    }
}