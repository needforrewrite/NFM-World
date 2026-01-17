namespace LuaJIT;

public static unsafe partial class Methods
{
    public const string LUAJIT_VERSION = "LuaJIT 2.1.0-beta3";
    public const int LUAJIT_VERSION_NUM = 20100;
    public const string LUAJIT_VERSION_SYM = "luaJIT_version_2_1_0_beta3";
    public const string LUAJIT_COPYRIGHT = "Copyright (C) 2005-2022 Mike Pall";
    public const string LUAJIT_URL = "https://luajit.org/";
	
    public const int LUAJIT_MODE_MASK = 0x00FF;
	
    public const int LUAJIT_MODE_MODE_MAX = 0x11;
	
    public const int LUAJIT_MODE_OFF = 0x0000;
    public const int LUAJIT_MODE_ON = 0x0100;
    public const int LUAJIT_MODE_FLUSH = 0x0200;

    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;
    
	public const string LUA_LDIR = "!\\lua\\";
	public const string LUA_CDIR = "!\\";
	
	public const string LUA_PATH_DEFAULT = ".\\?.lua;" + LUA_LDIR + "?.lua;" + LUA_LDIR + "?\\init.lua;";
	public const string LUA_CPATH_DEFAULT = ".\\?.dll;" + LUA_CDIR + "?.dll;" + LUA_CDIR + "loadall.dll";
	
	public const string LUA_PATH = "LUA_PATH";
	public const string LUA_CPATH = "LUA_CPATH";
	public const string LUA_INIT = "LUA_INIT";
	
	public const string LUA_DIRSEP = "\\";
	public const string LUA_PATHSEP = ";";
	public const string LUA_PATH_MARK = "?";
	public const string LUA_EXECDIR = "!";
	public const string LUA_IGMARK = "-";
	public const string LUA_PATH_CONFIG = LUA_DIRSEP + "\n" + LUA_PATHSEP + "\n" + LUA_PATH_MARK + "\n" + LUA_EXECDIR + "\n" + LUA_IGMARK + "\n";
	
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
	
	public const string LUA_VERSION = "Lua 5.1";
	public const string LUA_RELEASE = "Lua 5.1.4";
	public const int LUA_VERSION_NUM = 501;
	public const string LUA_COPYRIGHT = "Copyright (C) 1994-2008 Lua.org, PUC-Rio";
	public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo, W. Celes";
	
	public const string LUA_SIGNATURE = "\x1bLua";
	
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