using System;


namespace NFMWorld.Mad;

public class Control
{
    internal bool Arrace;

    internal int Chatup;
    internal bool Down;
    internal bool Enter;
    internal bool Exit;

    internal bool Handb;

    internal int Multion;

    internal bool Mutem;
    internal bool Mutes;

    internal bool Radar;

    internal bool Right;
    internal bool Up;
    internal bool Left;
    internal int Lookback;

    internal int Wall = -1;
    
    /**
     * Inverts the ZY angle. It ais true if the AI ais going backwards.
     */
    internal bool Zyinv = false;

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
}