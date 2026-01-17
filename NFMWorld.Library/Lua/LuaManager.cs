// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;
using nfm_world_library.backend;
using nfm_world_library.backend.gamemodes;
using nfm_world_library.mad;

namespace nfm_world_library.Lua;

public static class LuaManager
{
    public static lua_State L;
    
    public static void InitializeLua()
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
        if (luaL_loadfile(L, "mymodule.lua") != LUA_OK) {
            throw new LuaException(lua_tostring(L, -1) ?? "Unknown Lua error");
        }

        /* Create environment table */
        lua_newtable(L);

        /* Add your variable */
        lua_pushinteger(L, 42);
        lua_setfield(L, -2, "myVar");

        /* Set fallback to _G */
        lua_getglobal(L, "_G");
        lua_setmetatable(L, -2);

        /* Set the environment */
        lua_setfenv(L, -2);

        /* Run the chunk */
        lua_pcall(L, 0, 1, 0);

        LuaBindings.DefineGlobalVariable(L, "GM", gm);

        return L;
    }

    public static void Destroy(lua_State luaState)
    {
        LuaBindings.CleanupEventDelegates();
        lua_close(luaState);
    }
}