using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace LuaJIT
{
    public unsafe partial struct luaL_Reg
    {
        [NativeTypeName("const char *")]
        public sbyte* name;

        [NativeTypeName("lua_CFunction")]
        public delegate* unmanaged[Cdecl]<lua_State, int> func;
    }

    public unsafe partial struct luaL_Buffer
    {
        [NativeTypeName("char *")]
        public sbyte* p;

        public int lvl;

        public lua_State L;

        [NativeTypeName("char[512]")]
        public _buffer_e__FixedBuffer buffer;

        [InlineArray(512)]
        public partial struct _buffer_e__FixedBuffer
        {
            public sbyte e0;
        }
    }

    public unsafe partial struct lua_Debug
    {
        public int @event;

        [NativeTypeName("const char *")]
        public sbyte* name;

        [NativeTypeName("const char *")]
        public sbyte* namewhat;

        [NativeTypeName("const char *")]
        public sbyte* what;

        [NativeTypeName("const char *")]
        public sbyte* source;

        public int currentline;

        public int nups;

        public int linedefined;

        public int lastlinedefined;

        [NativeTypeName("char[60]")]
        public _short_src_e__FixedBuffer short_src;

        public int i_ci;

        [InlineArray(60)]
        public partial struct _short_src_e__FixedBuffer
        {
            public sbyte e0;
        }
    }

    public struct lua_State : IEquatable<lua_State>
    {
        public nuint Handle;
	
        public readonly bool IsNull => Handle == 0;
        public readonly bool IsNotNull => Handle != 0;
	
        public static bool operator !(lua_State state) => state.Handle == 0;
        public static bool operator ==(lua_State state1, lua_State state2) => state1.Handle == state2.Handle;
        public static bool operator ==(lua_State state1, int handle) => state1.Handle == (nuint) handle;
        public static bool operator !=(lua_State state1, lua_State state2) => state1.Handle != state2.Handle;
        public static bool operator !=(lua_State state1, int handle) => state1.Handle != (nuint) handle;
	
        public readonly bool Equals(lua_State other) => Handle == other.Handle;
        public readonly override bool Equals(object? other) => other is lua_State state && Equals(state);
        public readonly override int GetHashCode() => Handle.GetHashCode();
    }

    public static unsafe partial class Methods
    {
        public const int LUAJIT_MODE_ENGINE = 0;
        public const int LUAJIT_MODE_DEBUG = 1;
        public const int LUAJIT_MODE_FUNC = 2;
        public const int LUAJIT_MODE_ALLFUNC = 3;
        public const int LUAJIT_MODE_ALLSUBFUNC = 4;
        public const int LUAJIT_MODE_TRACE = 5;
        public const int LUAJIT_MODE_WRAPCFUNC = 0x10;
        public const int LUAJIT_MODE_MAX = 17;

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaJIT_setmode(lua_State L, int idx, int mode);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_profile_start(lua_State L, [NativeTypeName("const char *")] sbyte* mode, [NativeTypeName("luaJIT_profile_callback")] delegate* unmanaged[Cdecl]<void*, lua_State, int, int, void> cb, void* data);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_profile_stop(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaJIT_profile_dumpstack(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, int depth, [NativeTypeName("size_t *")] nuint* len);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_version_2_1_1739213504();

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_base(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_math(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_string(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_table(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_io(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_os(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_package(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_debug(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_bit(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_jit(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_ffi(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_string_buffer(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaL_openlibs(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_openlib(lua_State L, [NativeTypeName("const char *")] sbyte* libname, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l, int nup);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_register(lua_State L, [NativeTypeName("const char *")] sbyte* libname, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_getmetafield(lua_State L, int obj, [NativeTypeName("const char *")] sbyte* e);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_callmeta(lua_State L, int obj, [NativeTypeName("const char *")] sbyte* e);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_typerror(lua_State L, int narg, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_argerror(lua_State L, int numarg, [NativeTypeName("const char *")] sbyte* extramsg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_checklstring(lua_State L, int numArg, [NativeTypeName("size_t *")] nuint* l);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_optlstring(lua_State L, int numArg, [NativeTypeName("const char *")] sbyte* def, [NativeTypeName("size_t *")] nuint* l);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Number")]
        public static extern double luaL_checknumber(lua_State L, int numArg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Number")]
        public static extern double luaL_optnumber(lua_State L, int nArg, [NativeTypeName("lua_Number")] double def);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Integer")]
        public static extern nint luaL_checkinteger(lua_State L, int numArg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Integer")]
        public static extern nint luaL_optinteger(lua_State L, int nArg, [NativeTypeName("lua_Integer")] nint def);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checkstack(lua_State L, int sz, [NativeTypeName("const char *")] sbyte* msg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checktype(lua_State L, int narg, int t);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checkany(lua_State L, int narg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_newmetatable(lua_State L, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* luaL_checkudata(lua_State L, int ud, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_where(lua_State L, int lvl);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_error(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, __arglist);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_checkoption(lua_State L, int narg, [NativeTypeName("const char *")] sbyte* def, [NativeTypeName("const char *const[]")] sbyte** lst);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_ref(lua_State L, int t);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_unref(lua_State L, int t, int @ref);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadfile(lua_State L, [NativeTypeName("const char *")] sbyte* filename);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadbuffer(lua_State L, [NativeTypeName("const char *")] sbyte* buff, [NativeTypeName("size_t")] nuint sz, [NativeTypeName("const char *")] sbyte* name);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadstring(lua_State L, [NativeTypeName("const char *")] sbyte* s);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern lua_State luaL_newstate();

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_gsub(lua_State L, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("const char *")] sbyte* p, [NativeTypeName("const char *")] sbyte* r);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_findtable(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* fname, int szhint);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_fileresult(lua_State L, int stat, [NativeTypeName("const char *")] sbyte* fname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_execresult(lua_State L, int stat);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadfilex(lua_State L, [NativeTypeName("const char *")] sbyte* filename, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadbufferx(lua_State L, [NativeTypeName("const char *")] sbyte* buff, [NativeTypeName("size_t")] nuint sz, [NativeTypeName("const char *")] sbyte* name, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaL_traceback(lua_State L, lua_State L1, [NativeTypeName("const char *")] sbyte* msg, int level);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_setfuncs(lua_State L, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l, int nup);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_pushmodule(lua_State L, [NativeTypeName("const char *")] sbyte* modname, int sizehint);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* luaL_testudata(lua_State L, int ud, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_setmetatable(lua_State L, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_buffinit(lua_State L, luaL_Buffer* B);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("char *")]
        public static extern sbyte* luaL_prepbuffer(luaL_Buffer* B);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addlstring(luaL_Buffer* B, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("size_t")] nuint l);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addstring(luaL_Buffer* B, [NativeTypeName("const char *")] sbyte* s);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addvalue(luaL_Buffer* B);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_pushresult(luaL_Buffer* B);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern lua_State lua_newstate([NativeTypeName("lua_Alloc")] delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> f, void* ud);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_close(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_State lua_newthread(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_CFunction")]
        public static extern delegate* unmanaged[Cdecl]<lua_State, int> lua_atpanic(lua_State L, [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> panicf);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gettop(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_settop(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushvalue(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_remove(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_insert(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_replace(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_checkstack(lua_State L, int sz);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_xmove(lua_State from, lua_State to, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isnumber(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isstring(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_iscfunction(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isuserdata(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_type(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_typename")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_typename(lua_State L, int tp);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_equal(lua_State L, int idx1, int idx2);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_rawequal(lua_State L, int idx1, int idx2);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_lessthan(lua_State L, int idx1, int idx2);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Number")]
        public static extern double lua_tonumber(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Integer")]
        public static extern nint lua_tointeger(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_toboolean(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_tolstring(lua_State L, int idx, [NativeTypeName("size_t *")] nuint* len);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("size_t")]
        public static extern nuint lua_objlen(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_CFunction")]
        public static extern delegate* unmanaged[Cdecl]<lua_State, int> lua_tocfunction(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_touserdata(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_State lua_tothread(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const void *")]
        public static extern void* lua_topointer(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushnil(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushnumber(lua_State L, [NativeTypeName("lua_Number")] double n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushinteger(lua_State L, [NativeTypeName("lua_Integer")] nint n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushlstring(lua_State L, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("size_t")] nuint l);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushstring(lua_State L, [NativeTypeName("const char *")] sbyte* s);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_pushvfstring(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, [NativeTypeName("va_list")] sbyte* argp);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_pushfstring(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, __arglist);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushcclosure(lua_State L, [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> fn, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushboolean(lua_State L, int b);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushlightuserdata(lua_State L, void* p);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_pushthread(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_gettable(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_getfield(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* k);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawget(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_rawgeti(lua_State L, int idx, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_createtable(lua_State L, int narr, int nrec);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_newuserdata(lua_State L, [NativeTypeName("size_t")] nuint sz);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_getmetatable(lua_State L, int objindex);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_getfenv(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_settable(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_setfield(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* k);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawset(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawseti(lua_State L, int idx, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_setmetatable(lua_State L, int objindex);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_setfenv(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_call(lua_State L, int nargs, int nresults);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_pcall(lua_State L, int nargs, int nresults, int errfunc);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_cpcall(lua_State L, [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> func, void* ud);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_load(lua_State L, [NativeTypeName("lua_Reader")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, [NativeTypeName("const char *")] sbyte* chunkname);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_dump(lua_State L, [NativeTypeName("lua_Writer")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint, void*, int> writer, void* data);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_yield(lua_State L, int nresults);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_resume(lua_State L, int narg);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_status(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_gc(lua_State L, int what, int data);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_error(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_next(lua_State L, int idx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_concat(lua_State L, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Alloc")]
        public static extern delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> lua_getallocf(lua_State L, void** ud);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_setallocf(lua_State L, [NativeTypeName("lua_Alloc")] delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> f, void* ud);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_setlevel(lua_State from, lua_State to);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_getstack(lua_State L, int level, lua_Debug* ar);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_getinfo(lua_State L, [NativeTypeName("const char *")] sbyte* what, lua_Debug* ar);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getlocal")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_getlocal(lua_State L, [NativeTypeName("const lua_Debug *")] lua_Debug* ar, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setlocal")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_setlocal(lua_State L, [NativeTypeName("const lua_Debug *")] lua_Debug* ar, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getupvalue")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_getupvalue(lua_State L, int funcindex, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setupvalue")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_setupvalue(lua_State L, int funcindex, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_sethook(lua_State L, [NativeTypeName("lua_Hook")] delegate* unmanaged[Cdecl]<lua_State, lua_Debug*, void> func, int mask, int count);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Hook")]
        public static extern delegate* unmanaged[Cdecl]<lua_State, lua_Debug*, void> lua_gethook(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gethookmask(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gethookcount(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_upvalueid(lua_State L, int idx, int n);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_upvaluejoin(lua_State L, int idx1, int n1, int idx2, int n2);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_loadx(lua_State L, [NativeTypeName("lua_Reader")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, [NativeTypeName("const char *")] sbyte* chunkname, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_version")]
        [SuppressGCTransition]
        [return: NativeTypeName("const lua_Number *")]
        public static extern double* _lua_version(lua_State L);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_copy(lua_State L, int fromidx, int toidx);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Number")]
        public static extern double lua_tonumberx(lua_State L, int idx, int* isnum);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Integer")]
        public static extern nint lua_tointegerx(lua_State L, int idx, int* isnum);

        [DllImport("lua51", CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isyieldable(lua_State L);
    }
}
