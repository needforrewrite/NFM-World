namespace nfm_world_library.Lua;

public static class LuaManager
{
    public static lua_State L;
    
    public static void InitializeLua()
    {
        L = luaL_newstate();
        
        // Load standard libraries
        // https://stackoverflow.com/a/4552146
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
    }
}