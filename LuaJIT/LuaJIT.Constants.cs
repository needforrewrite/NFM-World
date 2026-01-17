namespace LuaJIT;

// ReSharper disable InconsistentNaming
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

public static unsafe partial class Methods
{
    public static ReadOnlySpan<byte> LUAJIT_VERSION => "LuaJIT 2.1.0-beta3"u8;
    public const int LUAJIT_VERSION_NUM = 20100;
    public static ReadOnlySpan<byte> LUAJIT_VERSION_SYM => "luaJIT_version_2_1_0_beta3"u8;
    public static ReadOnlySpan<byte> LUAJIT_COPYRIGHT => "Copyright (C) 2005-2022 Mike Pall"u8;
    public static ReadOnlySpan<byte> LUAJIT_URL => "https://luajit.org/"u8;
	
    public const int LUAJIT_MODE_MASK = 0x00FF;
	
    public const int LUAJIT_MODE_MODE_MAX = 0x11;
	
    public const int LUAJIT_MODE_OFF = 0x0000;
    public const int LUAJIT_MODE_ON = 0x0100;
    public const int LUAJIT_MODE_FLUSH = 0x0200;

    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;
    
	public static ReadOnlySpan<byte> LUA_LDIR => "!\\lua\\"u8;
	public static ReadOnlySpan<byte> LUA_CDIR => "!\\"u8;
	
	public static ReadOnlySpan<byte> LUA_PATH_DEFAULT => ".\\?.lua;!\\lua\\?.lua;!\\lua\\?\\init.lua;"u8;
	public static ReadOnlySpan<byte> LUA_CPATH_DEFAULT => ".\\?.dll;!\\?.dll;!\\loadall.dll"u8;
	
	public static ReadOnlySpan<byte> LUA_PATH => "LUA_PATH"u8;
	public static ReadOnlySpan<byte> LUA_CPATH => "LUA_CPATH"u8;
	public static ReadOnlySpan<byte> LUA_INIT => "LUA_INIT"u8;
	
	public static ReadOnlySpan<byte> LUA_DIRSEP => "\\"u8;
	public static ReadOnlySpan<byte> LUA_PATHSEP => ";"u8;
	public static ReadOnlySpan<byte> LUA_PATH_MARK => "?"u8;
	public static ReadOnlySpan<byte> LUA_EXECDIR => "!"u8;
	public static ReadOnlySpan<byte> LUA_IGMARK => "-"u8;
	public static ReadOnlySpan<byte> LUA_PATH_CONFIG => "\\\n;\n?\n!\n-\n"u8;
	
	public static string LUA_QL(string x)
	{
		return "'" + x + "'";
	}
	
	public const string LUA_QS = "'%s'";
	
	public const int LUAI_MAXSTACK = 65500;
	public const int LUAI_MAXCSTACK = 8000;
	public const int LUAI_GCPAUSE = 200;
	public const int LUAI_GCMUL = 200;
	public const int LUA_MAXCAPTURES = 32;
	
	public const int LUA_IDSIZE = 60;
	
	public const int LUAL_BUFFERSIZE = 512 > 16384 ? 8182 : 512;
	
	public static ReadOnlySpan<byte> LUA_VERSION => "Lua 5.1"u8;
	public static ReadOnlySpan<byte> LUA_RELEASE => "Lua 5.1.4"u8;
	public const int LUA_VERSION_NUM = 501;
	public static ReadOnlySpan<byte> LUA_COPYRIGHT => "Copyright (C) 1994-2008 Lua.org, PUC-Rio"u8;
	public static ReadOnlySpan<byte> LUA_AUTHORS => "R. Ierusalimschy, L. H. de Figueiredo, W. Celes"u8;
	
	public static ReadOnlySpan<byte> LUA_SIGNATURE => "\x1bLua"u8;
	
	public const int LUA_MULTRET = -1;
	
	public const int LUA_REGISTRYINDEX = -10000;
	public const int LUA_ENVIRONINDEX = -10001;
	public const int LUA_GLOBALSINDEX = -10002;
	
	public static int lua_upvalueindex(int i)
	{
		return LUA_GLOBALSINDEX - i;
	}
	
	public const int LUA_OK = 0;
	public const int LUA_YIELD = 1;
	public const int LUA_ERRRUN = 2;
	public const int LUA_ERRSYNTAX = 3;
	public const int LUA_ERRMEM = 4;
	public const int LUA_ERRERR = 5;
	
	public const int LUA_TNONE = -1;
	public const int LUA_TNIL = 0;
	public const int LUA_TBOOLEAN = 1;
	public const int LUA_TLIGHTUSERDATA = 2;
	public const int LUA_TNUMBER = 3;
	public const int LUA_TSTRING = 4;
	public const int LUA_TTABLE = 5;
	public const int LUA_TFUNCTION = 6;
	public const int LUA_TUSERDATA = 7;
	public const int LUA_TTHREAD = 8;
	
	public const int LUA_MINSTACK = 20;
	
	public const int LUA_GCSTOP = 0;
	public const int LUA_GCRESTART = 1;
	public const int LUA_GCCOLLECT = 2;
	public const int LUA_GCCOUNT = 3;
	public const int LUA_GCCOUNTB = 4;
	public const int LUA_GCSTEP = 5;
	public const int LUA_GCSETPAUSE = 6;
	public const int LUA_GCSETSTEPMUL = 7;
	public const int LUA_GCISRUNNING = 9;

}