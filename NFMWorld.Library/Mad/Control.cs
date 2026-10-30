using Maxine.Extensions;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

[PluginVisible]
public partial class Control
{
    [PluginVisible]
    public bool Arrace;

    [PluginVisible]
    public int Chatup;
    
    [PluginVisible]
    public bool Down;
    
    [PluginVisible]
    public bool Enter;
    
    [PluginVisible]
    public bool Exit;
    
    [PluginVisible]
    public bool Handb;
    
    [PluginVisible]
    public int Multion;
    
    [PluginVisible]
    public bool Mutem;
    
    [PluginVisible]
    public bool Mutes;
    
    [PluginVisible]
    public bool Radar;
    
    [PluginVisible]
    public bool Right;
    
    [PluginVisible]
    public bool Up;
    
    [PluginVisible]
    public bool Left;
    
    [PluginVisible]
    public int Lookback;
    
    [PluginVisible]
    public int Wall = -1;

    /// <summary>
    /// Inverts the ZY angle. It is true if the AI axis is flipped.
    /// </summary>
    [PluginVisible]
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

    [PluginVisible]
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

    [PluginVisible]
    public void Decode((bool Up, bool Down, bool Left, bool Right, bool Handb) enc)
    {
        Right = enc.Right;
        Left = enc.Left;
        Up = enc.Up;
        Down = enc.Down;
        Handb = enc.Handb;
    }
}