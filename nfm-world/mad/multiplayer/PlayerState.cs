using System.Runtime.InteropServices;

namespace NFMWorld.Mad;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerState
{
    public required bool Left;
    public required bool Right;
    public required bool Up;
    public required bool Down;
    public required bool Handb;
    public required bool Newcar;
    public required bool Mtouch;
    public required bool Wtouch;
    public required bool Pushed;
    public required bool Gtouch;
    public required bool pl;
    public required bool pr;
    public required bool pd;
    public required bool pu;
    public required bool dest;
    public required int x, y, z, xz, xy, zy;
    public required float speed, power;
    public required float mxz, pzy, pxy, txz;
    public required int loop;
    public required int wxz;
    public required int pcleared, clear, nlaps;
}
