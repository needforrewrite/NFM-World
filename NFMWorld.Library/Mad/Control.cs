using Lua;
using Maxine.Extensions;

namespace NFMWorldLibrary;

[LuaObject]
public partial class Control
{
    [LuaMember("arrace")]
    public bool Arrace;

    [LuaMember("chatup")]
    public int Chatup;
    
    [LuaMember("down")]
    public bool Down;
    
    [LuaMember("enter")]
    public bool Enter;
    
    [LuaMember("exit")]
    public bool Exit;
    
    [LuaMember("handb")]
    public bool Handb;
    
    [LuaMember("multion")]
    public int Multion;
    
    [LuaMember("mutem")]
    public bool Mutem;
    
    [LuaMember("mutes")]
    public bool Mutes;
    
    [LuaMember("radar")]
    public bool Radar;
    
    [LuaMember("right")]
    public bool Right;
    
    [LuaMember("up")]
    public bool Up;
    
    [LuaMember("left")]
    public bool Left;
    
    [LuaMember("lookback")]
    public int Lookback;
    
    [LuaMember("wall")]
    public int Wall = -1;

    /// <summary>
    /// Inverts the ZY angle. It is true if the AI axis is flipped.
    /// </summary>
    [LuaMember("zyinv")]
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

    [LuaMember("reset")]
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