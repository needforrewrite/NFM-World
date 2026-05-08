using Maxine.Extensions;

namespace NFMWorldLibrary.Mad;

public class Control
{
    public bool Arrace;

    public int Chatup;
    public bool Down;
    public bool Enter;
    public bool Exit;

    public bool Handb;

    public int Multion;

    public bool Mutem;
    public bool Mutes;

    public bool Radar;

    public bool Right;
    public bool Up;
    public bool Left;
    public int Lookback;

    public int Wall = -1;

    /**
     * Inverts the ZY angle. It is true if the AI axis is flipped.
     */
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

    private int Py(int i, int i47, int i48, int i49)
    {
        return (i - i47) * (i - i47) + (i48 - i49) * (i48 - i49);
    }

    private int Pys(int i, int i50, int i51, int i52)
    {
        return (int) Math.Sqrt((i - i50) * (i - i50) + (i51 - i52) * (i51 - i52));
    }

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