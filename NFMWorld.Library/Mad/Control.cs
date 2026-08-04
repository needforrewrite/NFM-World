using Lua;
using Maxine.Extensions;
using nfm_world_library.Lua;

namespace NFMWorldLibrary;

[LuaVisible]
public partial class Control
{
    [LuaName("arrace")]
    public bool Arrace;

    [LuaName("chatup")]
    public int Chatup;
    
    [LuaName("down")]
    public bool Down;
    
    [LuaName("enter")]
    public bool Enter;
    
    [LuaName("exit")]
    public bool Exit;
    
    [LuaName("handb")]
    public bool Handb;
    
    [LuaName("multion")]
    public int Multion;
    
    [LuaName("mutem")]
    public bool Mutem;
    
    [LuaName("mutes")]
    public bool Mutes;
    
    [LuaName("radar")]
    public bool Radar;
    
    [LuaName("right")]
    public bool Right;
    
    [LuaName("up")]
    public bool Up;
    
    [LuaName("left")]
    public bool Left;
    
    [LuaName("lookback")]
    public int Lookback;
    
    [LuaName("wall")]
    public int Wall = -1;

    /// <summary>
    /// Inverts the ZY angle. It is true if the AI axis is flipped.
    /// </summary>
    [LuaName("zyinv")]
    public bool Zyinv = false;

    internal void Falseo(int i)
    {
        Left = false;
        Right = false;
        Up = false;
        Down = false;
        Handb = false;
        Lookback = 0;
        Enter = false;
        Exit = false;
        if (i == 1)
        {
            return;
        }

        Radar = false;
        Arrace = false;
        Chatup = 0;
        if (i != 2)
        {
            Multion = 0;
        }
        if (i == 3)
        {
            return;
        }

        Mutem = false;
        Mutes = false;
    }

    [LuaName("reset")]
    internal void Reset()
    {
        Left = false;
        Right = false;
        Up = false;
        Down = false;
        Handb = false;
        Lookback = 0;
        Arrace = false;
        Mutem = false;
        Mutes = false;
    }

    public Nibble<byte> Encode()
    {
        return new Nibble<byte>([Right, Left, Up, Down, Handb]);
    }

    public void Decode(Nibble<byte> enc)
    {
        Right = enc[0];
        Left = enc[1];
        Up = enc[2];
        Down = enc[3];
        Handb = enc[4];
    }

    public void Decode((bool Up, bool Down, bool Left, bool Right, bool Handb) enc)
    {
        Right = enc.Right;
        Left = enc.Left;
        Up = enc.Up;
        Down = enc.Down;
        Handb = enc.Handb;
    }
}