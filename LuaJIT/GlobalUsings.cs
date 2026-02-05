global using size_t = nuint;
global using lua_Number = double;
global using lua_Integer = nint;
global using unsafe lua_CFunction = delegate* unmanaged[Cdecl]<LuaJIT.lua_State, int>;
global using lua_Unsigned = nuint;