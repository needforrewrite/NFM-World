using System.Runtime.InteropServices;
using MessagePack;

namespace NFMWorld.Mad;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[MessagePackObject]
public struct PlayerState
{
    [Key(0)] public required bool Left;
    [Key(1)] public required bool Right;
    [Key(2)] public required bool Up;
    [Key(3)] public required bool Down;
    [Key(4)] public required bool Handb;
    [Key(5)] public required bool Newcar;
    [Key(6)] public required bool Mtouch;
    [Key(7)] public required bool Wtouch;
    [Key(8)] public required bool Pushed;
    [Key(9)] public required bool Gtouch;
    [Key(10)] public required bool pl;
    [Key(11)] public required bool pr;
    [Key(12)] public required bool pd;
    [Key(13)] public required bool pu;
    [Key(14)] public required bool dest;
    [Key(29)] public required int x;
    [Key(30)] public required int y;
    [Key(31)] public required int z;
    [Key(32)] public required int xz;
    [Key(33)] public required int xy;
    [Key(34)] public required int zy;
    [Key(35)] public required float speed;
    [Key(36)] public required float power;
    [Key(37)] public required float mxz;
    [Key(38)] public required float pzy;
    [Key(39)] public required float pxy;
    [Key(40)] public required float txz;
    [Key(27)] public required int loop;
    [Key(28)] public required int wxz;
    [Key(41)] public required int pcleared;
    [Key(42)] public required int clear;
    [Key(43)] public required int nlaps;
}
