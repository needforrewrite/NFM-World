using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace LuaJIT;

public static unsafe partial class Methods
{
    #region luaL_* Library Functions

    public static void luaL_openlib(lua_State L, string? libname, luaL_Reg* l, int nup)
    {
        var libnamePtr = libname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(libname);
        try
        {
            luaL_openlib(L, libnamePtr, l, nup);
        }
        finally
        {
            if (libnamePtr != null) Marshal.FreeHGlobal((nint)libnamePtr);
        }
    }

    public static void luaL_openlib(lua_State L, ReadOnlySpan<byte> libname, luaL_Reg* l, int nup)
    {
        fixed (byte* libnamePtr = libname)
        {
            luaL_openlib(L, (sbyte*)libnamePtr, l, nup);
        }
    }

    public static void luaL_register(lua_State L, string? libname, luaL_Reg* l)
    {
        var libnamePtr = libname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(libname);
        try
        {
            luaL_register(L, libnamePtr, l);
        }
        finally
        {
            if (libnamePtr != null) Marshal.FreeHGlobal((nint)libnamePtr);
        }
    }

    public static void luaL_register(lua_State L, ReadOnlySpan<byte> libname, luaL_Reg* l)
    {
        fixed (byte* libnamePtr = libname)
        {
            luaL_register(L, (sbyte*)libnamePtr, l);
        }
    }

    public static int luaL_getmetafield(lua_State L, int obj, string e)
    {
        var ePtr = e == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(e);
        try
        {
            return luaL_getmetafield(L, obj, ePtr);
        }
        finally
        {
            if (ePtr != null) Marshal.FreeHGlobal((nint)ePtr);
        }
    }

    public static int luaL_getmetafield(lua_State L, int obj, ReadOnlySpan<byte> e)
    {
        fixed (byte* ePtr = e)
        {
            return luaL_getmetafield(L, obj, (sbyte*)ePtr);
        }
    }

    public static int luaL_callmeta(lua_State L, int obj, string e)
    {
        var ePtr = e == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(e);
        try
        {
            return luaL_callmeta(L, obj, ePtr);
        }
        finally
        {
            if (ePtr != null) Marshal.FreeHGlobal((nint)ePtr);
        }
    }

    public static int luaL_callmeta(lua_State L, int obj, ReadOnlySpan<byte> e)
    {
        fixed (byte* ePtr = e)
        {
            return luaL_callmeta(L, obj, (sbyte*)ePtr);
        }
    }

    public static int luaL_typerror(lua_State L, int narg, string tname)
    {
        var tnamePtr = tname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(tname);
        try
        {
            return luaL_typerror(L, narg, tnamePtr);
        }
        finally
        {
            if (tnamePtr != null) Marshal.FreeHGlobal((nint)tnamePtr);
        }
    }

    public static int luaL_typerror(lua_State L, int narg, ReadOnlySpan<byte> tname)
    {
        fixed (byte* tnamePtr = tname)
        {
            return luaL_typerror(L, narg, (sbyte*)tnamePtr);
        }
    }

    public static int luaL_argerror(lua_State L, int numarg, string extramsg)
    {
        var extramsgPtr = extramsg == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(extramsg);
        try
        {
            return luaL_argerror(L, numarg, extramsgPtr);
        }
        finally
        {
            if (extramsgPtr != null) Marshal.FreeHGlobal((nint)extramsgPtr);
        }
    }

    public static int luaL_argerror(lua_State L, int numarg, ReadOnlySpan<byte> extramsg)
    {
        fixed (byte* extramsgPtr = extramsg)
        {
            return luaL_argerror(L, numarg, (sbyte*)extramsgPtr);
        }
    }

    public static string? luaL_checklstring(lua_State L, int numArg, out nuint length)
    {
        nuint l;
        var result = luaL_checklstring(L, numArg, &l);
        length = l;
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? luaL_checklstring(lua_State L, int numArg)
    {
        return luaL_checklstring(L, numArg, out _);
    }

    public static string? luaL_optlstring(lua_State L, int numArg, string? def, out nuint length)
    {
        var defPtr = def == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(def);
        try
        {
            nuint l;
            var result = luaL_optlstring(L, numArg, defPtr, &l);
            length = l;
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
        finally
        {
            if (defPtr != null) Marshal.FreeHGlobal((nint)defPtr);
        }
    }

    public static string? luaL_optlstring(lua_State L, int numArg, ReadOnlySpan<byte> def, out nuint length)
    {
        fixed (byte* defPtr = def)
        {
            nuint l;
            var result = luaL_optlstring(L, numArg, (sbyte*)defPtr, &l);
            length = l;
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
    }

    public static void luaL_checkstack(lua_State L, int sz, string msg)
    {
        var msgPtr = msg == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(msg);
        try
        {
            luaL_checkstack(L, sz, msgPtr);
        }
        finally
        {
            if (msgPtr != null) Marshal.FreeHGlobal((nint)msgPtr);
        }
    }

    public static void luaL_checkstack(lua_State L, int sz, ReadOnlySpan<byte> msg)
    {
        fixed (byte* msgPtr = msg)
        {
            luaL_checkstack(L, sz, (sbyte*)msgPtr);
        }
    }

    public static int luaL_newmetatable(lua_State L, string tname)
    {
        var tnamePtr = tname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(tname);
        try
        {
            return luaL_newmetatable(L, tnamePtr);
        }
        finally
        {
            if (tnamePtr != null) Marshal.FreeHGlobal((nint)tnamePtr);
        }
    }

    public static int luaL_newmetatable(lua_State L, ReadOnlySpan<byte> tname)
    {
        fixed (byte* tnamePtr = tname)
        {
            return luaL_newmetatable(L, (sbyte*)tnamePtr);
        }
    }

    public static void* luaL_checkudata(lua_State L, int ud, string tname)
    {
        var tnamePtr = tname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(tname);
        try
        {
            return luaL_checkudata(L, ud, tnamePtr);
        }
        finally
        {
            if (tnamePtr != null) Marshal.FreeHGlobal((nint)tnamePtr);
        }
    }

    public static void* luaL_checkudata(lua_State L, int ud, ReadOnlySpan<byte> tname)
    {
        fixed (byte* tnamePtr = tname)
        {
            return luaL_checkudata(L, ud, (sbyte*)tnamePtr);
        }
    }

    public static int luaL_error(lua_State L, string fmt)
    {
        var fmtPtr = fmt == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(fmt);
        try
        {
            return luaL_error(L, fmtPtr, __arglist());
        }
        finally
        {
            if (fmtPtr != null) Marshal.FreeHGlobal((nint)fmtPtr);
        }
    }

    public static int luaL_error(lua_State L, ReadOnlySpan<byte> fmt)
    {
        fixed (byte* fmtPtr = fmt)
        {
            return luaL_error(L, (sbyte*)fmtPtr, __arglist());
        }
    }

    public static int luaL_loadfile(lua_State L, string filename)
    {
        var filenamePtr = filename == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(filename);
        try
        {
            return luaL_loadfile(L, filenamePtr);
        }
        finally
        {
            if (filenamePtr != null) Marshal.FreeHGlobal((nint)filenamePtr);
        }
    }

    public static int luaL_loadfile(lua_State L, ReadOnlySpan<byte> filename)
    {
        fixed (byte* filenamePtr = filename)
        {
            return luaL_loadfile(L, (sbyte*)filenamePtr);
        }
    }

    public static int luaL_loadbuffer(lua_State L, string buff, string name)
    {
        var buffPtr = buff == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(buff);
        var namePtr = name == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(name);
        try
        {
            var sz = buff != null ? (nuint)Encoding.ASCII.GetByteCount(buff) : 0;
            return luaL_loadbuffer(L, buffPtr, sz, namePtr);
        }
        finally
        {
            if (buffPtr != null) Marshal.FreeHGlobal((nint)buffPtr);
            if (namePtr != null) Marshal.FreeHGlobal((nint)namePtr);
        }
    }

    public static int luaL_loadbuffer(lua_State L, ReadOnlySpan<byte> buff, ReadOnlySpan<byte> name)
    {
        fixed (byte* buffPtr = buff)
        fixed (byte* namePtr = name)
        {
            return luaL_loadbuffer(L, (sbyte*)buffPtr, (nuint)buff.Length, (sbyte*)namePtr);
        }
    }

    public static int luaL_loadstring(lua_State L, string s)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        try
        {
            return luaL_loadstring(L, sPtr);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
        }
    }

    public static int luaL_loadstring(lua_State L, ReadOnlySpan<byte> s)
    {
        fixed (byte* sPtr = s)
        {
            return luaL_loadstring(L, (sbyte*)sPtr);
        }
    }

    public static string? luaL_gsub(lua_State L, string s, string p, string r)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        var pPtr = p == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(p);
        var rPtr = r == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(r);
        try
        {
            var result = luaL_gsub(L, sPtr, pPtr, rPtr);
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
            if (pPtr != null) Marshal.FreeHGlobal((nint)pPtr);
            if (rPtr != null) Marshal.FreeHGlobal((nint)rPtr);
        }
    }

    public static string? luaL_gsub(lua_State L, ReadOnlySpan<byte> s, ReadOnlySpan<byte> p, ReadOnlySpan<byte> r)
    {
        fixed (byte* sPtr = s)
        fixed (byte* pPtr = p)
        fixed (byte* rPtr = r)
        {
            var result = luaL_gsub(L, (sbyte*)sPtr, (sbyte*)pPtr, (sbyte*)rPtr);
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
    }

    public static string? luaL_findtable(lua_State L, int idx, string fname, int szhint)
    {
        var fnamePtr = fname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(fname);
        try
        {
            var result = luaL_findtable(L, idx, fnamePtr, szhint);
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
        finally
        {
            if (fnamePtr != null) Marshal.FreeHGlobal((nint)fnamePtr);
        }
    }

    public static string? luaL_findtable(lua_State L, int idx, ReadOnlySpan<byte> fname, int szhint)
    {
        fixed (byte* fnamePtr = fname)
        {
            var result = luaL_findtable(L, idx, (sbyte*)fnamePtr, szhint);
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
    }

    public static int luaL_fileresult(lua_State L, int stat, string? fname)
    {
        var fnamePtr = fname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(fname);
        try
        {
            return luaL_fileresult(L, stat, fnamePtr);
        }
        finally
        {
            if (fnamePtr != null) Marshal.FreeHGlobal((nint)fnamePtr);
        }
    }

    public static int luaL_fileresult(lua_State L, int stat, ReadOnlySpan<byte> fname)
    {
        fixed (byte* fnamePtr = fname)
        {
            return luaL_fileresult(L, stat, (sbyte*)fnamePtr);
        }
    }

    public static int luaL_loadfilex(lua_State L, string filename, string? mode)
    {
        var filenamePtr = filename == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(filename);
        var modePtr = mode == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(mode);
        try
        {
            return luaL_loadfilex(L, filenamePtr, modePtr);
        }
        finally
        {
            if (filenamePtr != null) Marshal.FreeHGlobal((nint)filenamePtr);
            if (modePtr != null) Marshal.FreeHGlobal((nint)modePtr);
        }
    }

    public static int luaL_loadfilex(lua_State L, ReadOnlySpan<byte> filename, ReadOnlySpan<byte> mode)
    {
        fixed (byte* filenamePtr = filename)
        fixed (byte* modePtr = mode)
        {
            return luaL_loadfilex(L, (sbyte*)filenamePtr, (sbyte*)modePtr);
        }
    }

    public static int luaL_loadbufferx(lua_State L, string buff, string name, string? mode)
    {
        var buffPtr = buff == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(buff);
        var namePtr = name == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(name);
        var modePtr = mode == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(mode);
        try
        {
            var sz = buff != null ? (nuint)Encoding.ASCII.GetByteCount(buff) : 0;
            return luaL_loadbufferx(L, buffPtr, sz, namePtr, modePtr);
        }
        finally
        {
            if (buffPtr != null) Marshal.FreeHGlobal((nint)buffPtr);
            if (namePtr != null) Marshal.FreeHGlobal((nint)namePtr);
            if (modePtr != null) Marshal.FreeHGlobal((nint)modePtr);
        }
    }

    public static int luaL_loadbufferx(lua_State L, ReadOnlySpan<byte> buff, ReadOnlySpan<byte> name, ReadOnlySpan<byte> mode)
    {
        fixed (byte* buffPtr = buff)
        fixed (byte* namePtr = name)
        fixed (byte* modePtr = mode)
        {
            return luaL_loadbufferx(L, (sbyte*)buffPtr, (nuint)buff.Length, (sbyte*)namePtr, (sbyte*)modePtr);
        }
    }

    public static void luaL_traceback(lua_State L, lua_State L1, string? msg, int level)
    {
        var msgPtr = msg == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(msg);
        try
        {
            luaL_traceback(L, L1, msgPtr, level);
        }
        finally
        {
            if (msgPtr != null) Marshal.FreeHGlobal((nint)msgPtr);
        }
    }

    public static void luaL_traceback(lua_State L, lua_State L1, ReadOnlySpan<byte> msg, int level)
    {
        fixed (byte* msgPtr = msg)
        {
            luaL_traceback(L, L1, (sbyte*)msgPtr, level);
        }
    }

    public static void luaL_pushmodule(lua_State L, string modname, int sizehint)
    {
        var modnamePtr = modname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(modname);
        try
        {
            luaL_pushmodule(L, modnamePtr, sizehint);
        }
        finally
        {
            if (modnamePtr != null) Marshal.FreeHGlobal((nint)modnamePtr);
        }
    }

    public static void luaL_pushmodule(lua_State L, ReadOnlySpan<byte> modname, int sizehint)
    {
        fixed (byte* modnamePtr = modname)
        {
            luaL_pushmodule(L, (sbyte*)modnamePtr, sizehint);
        }
    }

    public static void* luaL_testudata(lua_State L, int ud, string tname)
    {
        var tnamePtr = tname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(tname);
        try
        {
            return luaL_testudata(L, ud, tnamePtr);
        }
        finally
        {
            if (tnamePtr != null) Marshal.FreeHGlobal((nint)tnamePtr);
        }
    }

    public static void* luaL_testudata(lua_State L, int ud, ReadOnlySpan<byte> tname)
    {
        fixed (byte* tnamePtr = tname)
        {
            return luaL_testudata(L, ud, (sbyte*)tnamePtr);
        }
    }

    public static void luaL_setmetatable(lua_State L, string tname)
    {
        var tnamePtr = tname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(tname);
        try
        {
            luaL_setmetatable(L, tnamePtr);
        }
        finally
        {
            if (tnamePtr != null) Marshal.FreeHGlobal((nint)tnamePtr);
        }
    }

    public static void luaL_setmetatable(lua_State L, ReadOnlySpan<byte> tname)
    {
        fixed (byte* tnamePtr = tname)
        {
            luaL_setmetatable(L, (sbyte*)tnamePtr);
        }
    }

    public static void luaL_addlstring(luaL_Buffer* B, string s)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        try
        {
            var len = s != null ? (nuint)Encoding.ASCII.GetByteCount(s) : 0;
            luaL_addlstring(B, sPtr, len);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
        }
    }

    public static void luaL_addlstring(luaL_Buffer* B, ReadOnlySpan<byte> s)
    {
        fixed (byte* sPtr = s)
        {
            luaL_addlstring(B, (sbyte*)sPtr, (nuint)s.Length);
        }
    }

    public static void luaL_addstring(luaL_Buffer* B, string s)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        try
        {
            luaL_addstring(B, sPtr);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
        }
    }

    public static void luaL_addstring(luaL_Buffer* B, ReadOnlySpan<byte> s)
    {
        fixed (byte* sPtr = s)
        {
            luaL_addstring(B, (sbyte*)sPtr);
        }
    }

    #endregion

    #region lua_* Core Functions

    public static string? lua_typename(lua_State L, int tp)
    {
        var result = _lua_typename(L, tp);
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? lua_tolstring(lua_State L, int idx, out nuint len)
    {
        nuint length;
        var result = lua_tolstring(L, idx, &length);
        len = length;
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? lua_tolstring(lua_State L, int idx)
    {
        return lua_tolstring(L, idx, out _);
    }

    /// <summary>
    /// Convenience method equivalent to lua_tolstring with length ignored.
    /// </summary>
    public static string? lua_tostring(lua_State L, int idx)
    {
        return lua_tolstring(L, idx, out _);
    }
    
    public static int lua_tostringintobuffer(lua_State L, int idx, Span<byte> buffer)
    {
        nuint len;
        var strPtr = lua_tolstring(L, idx, &len);
        if (strPtr == null) return 0;

        var bytesToCopy = (int)Math.Min(len, (nuint)buffer.Length);
        for (int i = 0; i < bytesToCopy; i++)
        {
            buffer[i] = (byte)strPtr[i];
        }
        return bytesToCopy;
    }

    public static void lua_pushlstring(lua_State L, string s)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        try
        {
            var len = s != null ? (nuint)Encoding.ASCII.GetByteCount(s) : 0;
            lua_pushlstring(L, sPtr, len);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
        }
    }

    public static void lua_pushlstring(lua_State L, ReadOnlySpan<byte> s)
    {
        fixed (byte* sPtr = s)
        {
            lua_pushlstring(L, (sbyte*)sPtr, (nuint)s.Length);
        }
    }

    public static void lua_pushstring(lua_State L, string s)
    {
        var sPtr = s == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(s);
        try
        {
            lua_pushstring(L, sPtr);
        }
        finally
        {
            if (sPtr != null) Marshal.FreeHGlobal((nint)sPtr);
        }
    }

    public static void lua_pushstring(lua_State L, ReadOnlySpan<byte> s)
    {
        fixed (byte* sPtr = s)
        {
            lua_pushstring(L, (sbyte*)sPtr);
        }
    }

    public static void lua_getfield(lua_State L, int idx, string k)
    {
        var kPtr = k == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(k);
        try
        {
            lua_getfield(L, idx, kPtr);
        }
        finally
        {
            if (kPtr != null) Marshal.FreeHGlobal((nint)kPtr);
        }
    }

    public static void lua_getfield(lua_State L, int idx, ReadOnlySpan<byte> k)
    {
        fixed (byte* kPtr = k)
        {
            lua_getfield(L, idx, (sbyte*)kPtr);
        }
    }

    public static void lua_setfield(lua_State L, int idx, string k)
    {
        var kPtr = k == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(k);
        try
        {
            lua_setfield(L, idx, kPtr);
        }
        finally
        {
            if (kPtr != null) Marshal.FreeHGlobal((nint)kPtr);
        }
    }

    public static void lua_setfield(lua_State L, int idx, ReadOnlySpan<byte> k)
    {
        fixed (byte* kPtr = k)
        {
            lua_setfield(L, idx, (sbyte*)kPtr);
        }
    }

    public static int lua_load(lua_State L, delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, string chunkname)
    {
        var chunknamePtr = chunkname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(chunkname);
        try
        {
            return lua_load(L, reader, dt, chunknamePtr);
        }
        finally
        {
            if (chunknamePtr != null) Marshal.FreeHGlobal((nint)chunknamePtr);
        }
    }

    public static int lua_load(lua_State L, delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, ReadOnlySpan<byte> chunkname)
    {
        fixed (byte* chunknamePtr = chunkname)
        {
            return lua_load(L, reader, dt, (sbyte*)chunknamePtr);
        }
    }

    public static string? lua_getlocal(lua_State L, lua_Debug* ar, int n)
    {
        var result = _lua_getlocal(L, ar, n);
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? lua_setlocal(lua_State L, lua_Debug* ar, int n)
    {
        var result = _lua_setlocal(L, ar, n);
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? lua_getupvalue(lua_State L, int funcindex, int n)
    {
        var result = _lua_getupvalue(L, funcindex, n);
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static string? lua_setupvalue(lua_State L, int funcindex, int n)
    {
        var result = _lua_setupvalue(L, funcindex, n);
        return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
    }

    public static int lua_loadx(lua_State L, delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, string chunkname, string? mode)
    {
        var chunknamePtr = chunkname == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(chunkname);
        var modePtr = mode == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(mode);
        try
        {
            return lua_loadx(L, reader, dt, chunknamePtr, modePtr);
        }
        finally
        {
            if (chunknamePtr != null) Marshal.FreeHGlobal((nint)chunknamePtr);
            if (modePtr != null) Marshal.FreeHGlobal((nint)modePtr);
        }
    }

    public static int lua_loadx(lua_State L, delegate* unmanaged[Cdecl]<lua_State, void*, nuint*, sbyte*> reader, void* dt, ReadOnlySpan<byte> chunkname, ReadOnlySpan<byte> mode)
    {
        fixed (byte* chunknamePtr = chunkname)
        fixed (byte* modePtr = mode)
        {
            return lua_loadx(L, reader, dt, (sbyte*)chunknamePtr, (sbyte*)modePtr);
        }
    }

    #endregion

    #region LuaJIT Profiling Functions

    public static void luaJIT_profile_start(lua_State L, string mode, delegate* unmanaged[Cdecl]<void*, lua_State, int, int, void> cb, void* data)
    {
        var modePtr = mode == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(mode);
        try
        {
            luaJIT_profile_start(L, modePtr, cb, data);
        }
        finally
        {
            if (modePtr != null) Marshal.FreeHGlobal((nint)modePtr);
        }
    }

    public static void luaJIT_profile_start(lua_State L, ReadOnlySpan<byte> mode, delegate* unmanaged[Cdecl]<void*, lua_State, int, int, void> cb, void* data)
    {
        fixed (byte* modePtr = mode)
        {
            luaJIT_profile_start(L, (sbyte*)modePtr, cb, data);
        }
    }

    public static string? luaJIT_profile_dumpstack(lua_State L, string fmt, int depth, out nuint len)
    {
        var fmtPtr = fmt == null ? null : (sbyte*)Marshal.StringToHGlobalAnsi(fmt);
        try
        {
            nuint length;
            var result = luaJIT_profile_dumpstack(L, fmtPtr, depth, &length);
            len = length;
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
        finally
        {
            if (fmtPtr != null) Marshal.FreeHGlobal((nint)fmtPtr);
        }
    }

    public static string? luaJIT_profile_dumpstack(lua_State L, ReadOnlySpan<byte> fmt, int depth, out nuint len)
    {
        fixed (byte* fmtPtr = fmt)
        {
            nuint length;
            var result = luaJIT_profile_dumpstack(L, (sbyte*)fmtPtr, depth, &length);
            len = length;
            return result == null ? null : Marshal.PtrToStringAnsi((nint)result);
        }
    }

    #endregion
}