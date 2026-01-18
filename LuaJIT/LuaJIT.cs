using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace LuaJIT
{
    public unsafe partial struct luaL_RegManaged
    {
        public string name;
        public lua_CFunction func;
    }

    public unsafe partial struct luaL_Reg
    {
        [NativeTypeName("const char *")]
        public sbyte* name;

        public lua_CFunction func;
    }

    public unsafe partial struct luaL_Buffer
    {
        [NativeTypeName("char *")]
        public sbyte* p;

        public int lvl;

        public lua_State L;

        [NativeTypeName("char[512]")]
        public luaL_Buffer_FixedBuffer buffer;

        [InlineArray(512)]
        public partial struct luaL_Buffer_FixedBuffer
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
        public lua_Debug_ShortSrc_FixedBuffer short_src;

        public int i_ci;

        [InlineArray(60)]
        public partial struct lua_Debug_ShortSrc_FixedBuffer
        {
            public sbyte e0;
        }
    }

    public struct lua_State : IEquatable<lua_State>
    {
        public nuint Handle;

        public readonly bool IsNull => Handle == 0;
        public readonly bool IsNotNull => Handle != 0;
        public static lua_State Null => new() { Handle = 0 };

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

#if USE_LUA51
        private const string DllName = "lua515";
#else
        private const string DllName = "lua51";
#endif

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaJIT_setmode(lua_State L, int idx, int mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_profile_start(lua_State L, [NativeTypeName("const char *")] sbyte* mode, [NativeTypeName("luaJIT_profile_callback")] delegate* unmanaged[Cdecl]<void*, lua_State, int, int, void> cb, void* data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_profile_stop(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaJIT_profile_dumpstack(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, int depth, size_t* len);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaJIT_version_2_1_1739213504();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaopen_base(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaopen_math(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaopen_string(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaopen_table(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_io(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_os(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_package(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_debug(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_bit(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_jit(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_ffi(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static extern int luaopen_string_buffer(lua_State L);

        /// <summary>
        /// Opens all standard Lua libraries into the given state.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaL_openlibs(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_openlib(lua_State L, [NativeTypeName("const char *")] sbyte* libname, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l, int nup);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_register(lua_State L, [NativeTypeName("const char *")] sbyte* libname, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_getmetafield(lua_State L, int obj, [NativeTypeName("const char *")] sbyte* e);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_callmeta(lua_State L, int obj, [NativeTypeName("const char *")] sbyte* e);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_typerror(lua_State L, int narg, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_argerror(lua_State L, int numarg, [NativeTypeName("const char *")] sbyte* extramsg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_checklstring(lua_State L, int numArg, size_t* l);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_optlstring(lua_State L, int numArg, [NativeTypeName("const char *")] sbyte* def, size_t* l);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Number luaL_checknumber(lua_State L, int numArg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Number luaL_optnumber(lua_State L, int nArg, [NativeTypeName("lua_Number")] double def);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Integer luaL_checkinteger(lua_State L, int numArg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Integer luaL_optinteger(lua_State L, int nArg, [NativeTypeName("lua_Integer")] nint def);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checkstack(lua_State L, int sz, [NativeTypeName("const char *")] sbyte* msg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checktype(lua_State L, int narg, int t);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_checkany(lua_State L, int narg);

        /// <summary>
        /// If the registry already has the key tname, returns 0. Otherwise, creates a new table to be used as a metatable
        /// for userdata, adds it to the registry with key tname, and returns 1. In both cases pushes onto the stack
        /// the final value associated with tname in the registry.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="tname">The metatable name (null-terminated string).</param>
        /// <returns>1 if a new metatable was created, 0 if it already existed.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_newmetatable(lua_State L, [NativeTypeName("const char *")] sbyte* tname);

        /// <summary>
        /// Checks whether the function argument narg is a userdata of the type tname (see luaL_newmetatable).
        /// It returns the userdata address (see lua_touserdata).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="ud">The argument index.</param>
        /// <param name="tname">The expected metatable name.</param>
        /// <returns>Pointer to the userdata.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* luaL_checkudata(lua_State L, int ud, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_where(lua_State L, int lvl);

        /// <summary>
        /// Raises an error. The error message format is given by fmt plus any extra arguments,
        /// following the same rules of lua_pushfstring. It also adds at the beginning of the
        /// message the file name and the line number where the error occurred, if this information is available.
        /// This function never returns, but it is an idiom to use it as return luaL_error(args).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="fmt">The error message format string.</param>
        /// <returns>Never returns (longjmps to error handler).</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_error(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, __arglist);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_checkoption(lua_State L, int narg, [NativeTypeName("const char *")] sbyte* def, [NativeTypeName("const char *const[]")] sbyte** lst);

        /// <summary>
        /// Creates and returns a reference, in the table at index t, for the object at the top of the stack (and pops the object).
        /// A reference is a unique integer key. As long as you do not manually add integer keys into table t,
        /// luaL_ref ensures the uniqueness of the key it returns. You can retrieve an object referred by reference r
        /// by calling lua_rawgeti(L, t, r). Function luaL_unref frees a reference and its associated object.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="t">The table index (usually LUA_REGISTRYINDEX).</param>
        /// <returns>The reference integer key.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_ref(lua_State L, int t);

        /// <summary>
        /// Releases reference ref from the table at index t (see luaL_ref). The entry is removed from the table,
        /// so that the referred object can be collected. The reference ref is also freed to be used again.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="t">The table index (usually LUA_REGISTRYINDEX).</param>
        /// <param name="ref">The reference to release.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_unref(lua_State L, int t, int @ref);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadfile(lua_State L, [NativeTypeName("const char *")] sbyte* filename);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadbuffer(lua_State L, [NativeTypeName("const char *")] sbyte* buff, size_t sz, [NativeTypeName("const char *")] sbyte* name);

        /// <summary>
        /// Loads a string as a Lua chunk. This function uses lua_load to load the chunk in the zero-terminated string s.
        /// This function returns the same results as lua_load. Also as lua_load, this function only loads the chunk;
        /// it does not run it.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="s">Null-terminated string containing Lua code.</param>
        /// <returns>0 if no errors, or an error code (LUA_ERRSYNTAX, LUA_ERRMEM).</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadstring(lua_State L, [NativeTypeName("const char *")] sbyte* s);

        /// <summary>
        /// Creates a new Lua state. It calls lua_newstate with an allocator based on the standard C realloc function
        /// and then sets a panic function (see lua_atpanic) that prints an error message to the standard error output
        /// in case of fatal errors. Returns the new state, or NULL if there is a memory allocation error.
        /// </summary>
        /// <returns>A new Lua state, or NULL on allocation failure.</returns>
        /// <summary>
        /// Creates a new Lua state. It calls lua_newstate with an allocator based on the standard C realloc function
        /// and then sets a panic function (see lua_atpanic) that prints an error message to the standard error output
        /// in case of fatal errors. Returns the new state, or NULL if there is a memory allocation error.
        /// </summary>
        /// <returns>A new Lua state, or NULL on allocation failure.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern lua_State luaL_newstate();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_gsub(lua_State L, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("const char *")] sbyte* p, [NativeTypeName("const char *")] sbyte* r);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* luaL_findtable(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* fname, int szhint);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_fileresult(lua_State L, int stat, [NativeTypeName("const char *")] sbyte* fname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int luaL_execresult(lua_State L, int stat);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadfilex(lua_State L, [NativeTypeName("const char *")] sbyte* filename, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_loadbufferx(lua_State L, [NativeTypeName("const char *")] sbyte* buff, size_t sz, [NativeTypeName("const char *")] sbyte* name, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void luaL_traceback(lua_State L, lua_State L1, [NativeTypeName("const char *")] sbyte* msg, int level);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_setfuncs(lua_State L, [NativeTypeName("const luaL_Reg *")] luaL_Reg* l, int nup);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_pushmodule(lua_State L, [NativeTypeName("const char *")] sbyte* modname, int sizehint);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* luaL_testudata(lua_State L, int ud, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_setmetatable(lua_State L, [NativeTypeName("const char *")] sbyte* tname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_buffinit(lua_State L, luaL_Buffer* B);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("char *")]
        public static extern sbyte* luaL_prepbuffer(luaL_Buffer* B);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addlstring(luaL_Buffer* B, [NativeTypeName("const char *")] sbyte* s, size_t l);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addstring(luaL_Buffer* B, [NativeTypeName("const char *")] sbyte* s);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_addvalue(luaL_Buffer* B);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void luaL_pushresult(luaL_Buffer* B);

        /// <summary>
        /// Creates a new, independent state. Returns NULL if cannot create the state (due to lack of memory).
        /// The argument f is the allocator function; Lua does all memory allocation for this state through this function.
        /// The second argument, ud, is an opaque pointer that Lua simply passes to the allocator in every call.
        /// </summary>
        /// <param name="f">The allocator function.</param>
        /// <param name="ud">Opaque pointer passed to the allocator.</param>
        /// <returns>A new Lua state, or NULL on allocation failure.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern lua_State lua_newstate([NativeTypeName("lua_Alloc")] delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> f, void* ud);

        /// <summary>
        /// Destroys all objects in the given Lua state (calling the corresponding garbage-collection metamethods, if any)
        /// and frees all dynamic memory used by this state. On several platforms, you may not need to call this function,
        /// because all resources are naturally released when the host program ends. On the other hand, long-running programs,
        /// such as a daemon or a web server, might need to release states as soon as they are not needed,
        /// to avoid growing too large.
        /// </summary>
        /// <param name="L">The Lua state to close.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_close(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_State lua_newthread(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_CFunction lua_atpanic(lua_State L, [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> panicf);

        /// <summary>
        /// Returns the index of the top element in the stack. Because indices start at 1,
        /// this result is equal to the number of elements in the stack (and so 0 means an empty stack).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <returns>The number of elements in the stack.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gettop(lua_State L);

        /// <summary>
        /// Accepts any acceptable index, or 0, and sets the stack top to this index.
        /// If the new top is larger than the old one, then the new elements are filled with nil.
        /// If index is 0, then all stack elements are removed.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The new top index, or 0 to clear the stack.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_settop(lua_State L, int idx);

        /// <summary>
        /// Pushes a copy of the element at the given valid index onto the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The index of the element to copy.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushvalue(lua_State L, int idx);

        /// <summary>
        /// Removes the element at the given valid index, shifting down the elements above this index to fill the gap.
        /// Cannot be called with a pseudo-index, because a pseudo-index is not an actual stack position.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The index of the element to remove.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_remove(lua_State L, int idx);

        /// <summary>
        /// Moves the top element into the given valid index, shifting up the elements above this index to open space.
        /// Cannot be called with a pseudo-index, because a pseudo-index is not an actual stack position.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The index where to insert the top element.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_insert(lua_State L, int idx);

        /// <summary>
        /// Moves the top element into the given position (and pops it), without shifting any element
        /// (therefore replacing the value at the given position).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The index where to place the top element.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_replace(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_checkstack(lua_State L, int sz);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_xmove(lua_State from, lua_State to, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isnumber(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isstring(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_iscfunction(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isuserdata(lua_State L, int idx);

        /// <summary>
        /// Returns the type of the value in the given acceptable index, or LUA_TNONE for a non-valid index
        /// (that is, an index to an "empty" stack position). The types returned by lua_type are coded by the following constants
        /// defined in lua.h: LUA_TNIL, LUA_TNUMBER, LUA_TBOOLEAN, LUA_TSTRING, LUA_TTABLE, LUA_TFUNCTION, LUA_TUSERDATA, LUA_TTHREAD, and LUA_TLIGHTUSERDATA.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index to check.</param>
        /// <returns>The type constant, or LUA_TNONE for invalid index.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_type(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_typename")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_typename(lua_State L, int tp);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_equal(lua_State L, int idx1, int idx2);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_rawequal(lua_State L, int idx1, int idx2);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_lessthan(lua_State L, int idx1, int idx2);

        /// <summary>
        /// Converts the Lua value at the given acceptable index to a C double (lua_Number).
        /// The Lua value must be a number or a string convertible to a number; otherwise, lua_tonumber returns 0.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index.</param>
        /// <returns>The value as a double, or 0 if not a number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Number lua_tonumber(lua_State L, int idx);

        /// <summary>
        /// Converts the Lua value at the given acceptable index to a signed integer (lua_Integer).
        /// The Lua value must be a number or a string convertible to a number; otherwise, lua_tointeger returns 0.
        /// If the number is not an integer, it is truncated in some non-specified way.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index.</param>
        /// <returns>The value as an integer, or 0 if not a number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Integer lua_tointeger(lua_State L, int idx);

        /// <summary>
        /// Converts the Lua value at the given acceptable index to a C boolean value (0 or 1).
        /// Like all tests in Lua, lua_toboolean returns 1 for any Lua value different from false and nil;
        /// otherwise it returns 0. It also returns 0 when called with a non-valid index.
        /// (If you want to accept only actual boolean values, use lua_isboolean to test the value's type.)
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index.</param>
        /// <returns>1 if the value is true (not false or nil), 0 otherwise.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_toboolean(lua_State L, int idx);

        /// <summary>
        /// Converts the Lua value at the given acceptable index to a C string. If len is not NULL,
        /// it also sets *len with the string length. The Lua value must be a string or a number;
        /// otherwise, the function returns NULL. If the value is a number, then lua_tolstring also changes
        /// the actual value in the stack to a string. (This change confuses lua_next when lua_tolstring is applied
        /// to keys during a table traversal.) lua_tolstring returns a fully aligned pointer to a string inside the Lua state.
        /// This string always has a zero ('\0') after its last character (as in C), but can contain other zeros in its body.
        /// Because Lua has garbage collection, there is no guarantee that the pointer returned by lua_tolstring will be valid
        /// after the corresponding value is removed from the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index.</param>
        /// <param name="len">Pointer to receive the string length (can be NULL).</param>
        /// <returns>Pointer to the string, or NULL if not a string or number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_tolstring(lua_State L, int idx, size_t* len);

        /// <summary>
        /// Returns the "length" of the value at the given acceptable index: for strings, this is the string length;
        /// for tables, this is the result of the length operator ('#'); for userdata, this is the size of the block
        /// of memory allocated for the userdata; for other values, it is 0.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The stack index.</param>
        /// <returns>The length of the value.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern size_t lua_objlen(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_CFunction lua_tocfunction(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_touserdata(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_State lua_tothread(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("const void *")]
        public static extern void* lua_topointer(lua_State L, int idx);

        /// <summary>
        /// Pushes a nil value onto the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushnil(lua_State L);

        /// <summary>
        /// Pushes a number with value n onto the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="n">The number to push.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushnumber(lua_State L, [NativeTypeName("lua_Number")] double n);

        /// <summary>
        /// Pushes an integer with value n onto the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="n">The integer to push.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushinteger(lua_State L, [NativeTypeName("lua_Integer")] nint n);

        /// <summary>
        /// Pushes the string pointed to by s with size len onto the stack.
        /// Lua makes (or reuses) an internal copy of the given string, so the memory at s can be freed or reused
        /// immediately after the function returns. The string can contain embedded zeros.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="s">Pointer to the string data.</param>
        /// <param name="l">Length of the string.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushlstring(lua_State L, [NativeTypeName("const char *")] sbyte* s, size_t l);

        /// <summary>
        /// Pushes the zero-terminated string pointed to by s onto the stack.
        /// Lua makes (or reuses) an internal copy of the given string, so the memory at s can be freed or reused
        /// immediately after the function returns. The string cannot contain embedded zeros; it is assumed to end at the first zero.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="s">Pointer to the null-terminated string.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushstring(lua_State L, [NativeTypeName("const char *")] sbyte* s);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_pushvfstring(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, [NativeTypeName("va_list")] sbyte* argp);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* lua_pushfstring(lua_State L, [NativeTypeName("const char *")] sbyte* fmt, __arglist);

        /// <summary>
        /// Pushes a new C closure onto the stack. When a C function is created, it is possible to associate some values with it,
        /// thus creating a C closure (see §3.4 of Lua manual); these values are then accessible to the function whenever it is called.
        /// To associate values with a C function, first these values should be pushed onto the stack (when there are multiple values,
        /// the first value is pushed first). Then lua_pushcclosure is called to create and push the C function onto the stack,
        /// with the argument n telling how many values should be associated with the function. lua_pushcclosure also pops these values from the stack.
        /// The maximum value for n is 255.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="fn">The C function pointer.</param>
        /// <param name="n">The number of upvalues to associate with the function.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushcclosure(lua_State L, [NativeTypeName("lua_CFunction")] delegate* unmanaged[Cdecl]<lua_State, int> fn, int n);

        /// <summary>
        /// Pushes a boolean value with value b onto the stack.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="b">The boolean value (0 for false, non-zero for true).</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushboolean(lua_State L, int b);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_pushlightuserdata(lua_State L, void* p);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_pushthread(lua_State L);

        /// <summary>
        /// Pushes onto the stack the value t[k], where t is the value at the given valid index and k is the value at the top of the stack.
        /// This function pops the key from the stack (putting the resulting value in its place). As in Lua, this function may trigger
        /// a metamethod for the "index" event (see §2.8 of Lua manual).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_gettable(lua_State L, int idx);

        /// <summary>
        /// Pushes onto the stack the value t[k], where t is the value at the given valid index. As in Lua, this function may trigger
        /// a metamethod for the "index" event (see §2.8 of Lua manual).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        /// <param name="k">The field name (null-terminated string).</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_getfield(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* k);

        /// <summary>
        /// Similar to lua_gettable, but does a raw access (i.e., without metamethods).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawget(lua_State L, int idx);

        /// <summary>
        /// Pushes onto the stack the value t[n], where t is the value at the given valid index.
        /// The access is raw; that is, it does not invoke metamethods.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        /// <param name="n">The integer key.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_rawgeti(lua_State L, int idx, int n);

        /// <summary>
        /// Creates a new empty table and pushes it onto the stack. The new table has space pre-allocated for narr array elements
        /// and nrec non-array elements. This pre-allocation is useful when you know exactly how many elements the table will have.
        /// Otherwise you can use the function lua_newtable.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="narr">Number of pre-allocated array elements.</param>
        /// <param name="nrec">Number of pre-allocated non-array elements.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_createtable(lua_State L, int narr, int nrec);

        /// <summary>
        /// This function allocates a new block of memory with the given size, pushes onto the stack a new full userdata
        /// with the block address, and returns this address. Userdata represent C values in Lua. A full userdata represents
        /// a block of memory. It is an object (like a table): you must create it, it can have its own metatable,
        /// and you can detect when it is being collected. A full userdata is only equal to itself (under raw equality).
        /// When Lua collects a full userdata with a gc metamethod, Lua calls the metamethod and marks the userdata as finalized.
        /// When this userdata is collected again then Lua frees its corresponding memory.
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="sz">Size of the memory block to allocate.</param>
        /// <returns>Pointer to the allocated memory block.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_newuserdata(lua_State L, size_t sz);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_getmetatable(lua_State L, int objindex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_getfenv(lua_State L, int idx);

        /// <summary>
        /// Does the equivalent to t[k] = v, where t is the value at the given valid index, v is the value at the top of the stack,
        /// and k is the value just below the top. This function pops both the key and the value from the stack.
        /// As in Lua, this function may trigger a metamethod for the "newindex" event (see §2.8 of Lua manual).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_settable(lua_State L, int idx);

        /// <summary>
        /// Does the equivalent to t[k] = v, where t is the value at the given valid index and v is the value at the top of the stack.
        /// This function pops the value from the stack. As in Lua, this function may trigger a metamethod for the "newindex" event
        /// (see §2.8 of Lua manual).
        /// </summary>
        /// <param name="L">The Lua state.</param>
        /// <param name="idx">The table index.</param>
        /// <param name="k">The field name (null-terminated string).</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_setfield(lua_State L, int idx, [NativeTypeName("const char *")] sbyte* k);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawset(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_rawseti(lua_State L, int idx, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_setmetatable(lua_State L, int objindex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_setfenv(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_call(lua_State L, int nargs, int nresults);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_pcall(lua_State L, int nargs, int nresults, int errfunc);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_cpcall(lua_State L, lua_CFunction func, void* ud);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_load(lua_State L, [NativeTypeName("lua_Reader")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, [NativeTypeName("const char *")] sbyte* chunkname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_dump(lua_State L, [NativeTypeName("lua_Writer")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint, void*, int> writer, void* data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_yield(lua_State L, int nresults);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_resume(lua_State L, int narg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_status(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_gc(lua_State L, int what, int data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_error(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_next(lua_State L, int idx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_concat(lua_State L, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Alloc")]
        public static extern delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> lua_getallocf(lua_State L, void** ud);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_setallocf(lua_State L, [NativeTypeName("lua_Alloc")] delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*> f, void* ud);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_setlevel(lua_State from, lua_State to);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_getstack(lua_State L, int level, lua_Debug* ar);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_getinfo(lua_State L, [NativeTypeName("const char *")] sbyte* what, lua_Debug* ar);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getlocal")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_getlocal(lua_State L, [NativeTypeName("const lua_Debug *")] lua_Debug* ar, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setlocal")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_setlocal(lua_State L, [NativeTypeName("const lua_Debug *")] lua_Debug* ar, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getupvalue")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_getupvalue(lua_State L, int funcindex, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setupvalue")]
        [SuppressGCTransition]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* _lua_setupvalue(lua_State L, int funcindex, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_sethook(lua_State L, [NativeTypeName("lua_Hook")] delegate* unmanaged[Cdecl]<lua_State, lua_Debug*, void> func, int mask, int count);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        [return: NativeTypeName("lua_Hook")]
        public static extern delegate* unmanaged[Cdecl]<lua_State, lua_Debug*, void> lua_gethook(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gethookmask(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_gethookcount(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void* lua_upvalueid(lua_State L, int idx, int n);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_upvaluejoin(lua_State L, int idx1, int n1, int idx2, int n2);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_loadx(lua_State L, [NativeTypeName("lua_Reader")] delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, [NativeTypeName("const char *")] sbyte* chunkname, [NativeTypeName("const char *")] sbyte* mode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_version")]
        [SuppressGCTransition]
        public static extern lua_Number* _lua_version(lua_State L);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern void lua_copy(lua_State L, int fromidx, int toidx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Number lua_tonumberx(lua_State L, int idx, int* isnum);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern lua_Integer lua_tointegerx(lua_State L, int idx, int* isnum);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [SuppressGCTransition]
        public static extern int lua_isyieldable(lua_State L);
    }
}
