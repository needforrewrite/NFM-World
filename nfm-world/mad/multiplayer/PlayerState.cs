using System.Runtime.InteropServices;
using MessagePack;
using NFMWorld.Mad;
using SoftFloat;

namespace NFMWorld.Mad;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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
    [Key(29)] public required fix64 x;
    [Key(30)] public required fix64 y;
    [Key(31)] public required fix64 z;
    [Key(32)] public required fix64 xz;
    [Key(33)] public required fix64 xy;
    [Key(34)] public required fix64 zy;
    [Key(35)] public required fix64 speed;
    [Key(36)] public required fix64 power;
    [Key(37)] public required fix64 mxz;
    [Key(38)] public required fix64 pzy;
    [Key(39)] public required fix64 pxy;
    [Key(40)] public required fix64 txz;
    [Key(27)] public required int loop;
    [Key(28)] public required int wxz;
    [Key(41)] public required int pcleared;
    [Key(42)] public required int clear;
    [Key(43)] public required int nlaps;
    
    public static void ApplyTo(PlayerState state, InGameCar c)
    {
        c.Control.Left = state.Left;
        c.Control.Right = state.Right;
        c.Control.Up = state.Up;
        c.Control.Down = state.Down;
        c.Control.Handb = state.Handb;
        c.Mad.Newcar = state.Newcar;
        c.Mad.Mtouch = state.Mtouch;
        c.Mad.Wtouch = state.Wtouch;
        c.Mad.Pushed = state.Pushed;
        c.Mad.Gtouch = state.Gtouch;
        c.Mad.Pl = state.pl;
        c.Mad.Pr = state.pr;
        c.Mad.Pd = state.pd;
        c.Mad.Pu = state.pu;
        c.CarRef.Position = new Vector3((float)state.x, (float)state.y, (float)state.z);
        c.CarRef.Rotation = new Euler(AngleSingle.FromDegrees(state.xz), AngleSingle.FromDegrees(state.zy), AngleSingle.FromDegrees(state.xy));
        c.Mad.Speed = state.speed;
        c.Mad.Power = state.power;
        c.Mad.Mxz = state.mxz;
        c.Mad.Pzy = state.pzy;
        c.Mad.Pxy = state.pxy;
        c.Mad.Txz = state.txz;
        c.Mad.Loop = state.loop;
        c.CarRef.TurningWheelAngle = c.CarRef.TurningWheelAngle with { Xz = AngleSingle.FromDegrees(state.wxz) };
        c.Mad.Pcleared = state.pcleared;
        c.Mad.Clear = state.clear;
        c.Mad.Nlaps = state.nlaps;
    }
    
    public static PlayerState CreateFrom(InGameCar car)
    {
        return new PlayerState
        {
            Left = car.Control.Left,
            Right = car.Control.Right,
            Up = car.Control.Up,
            Down = car.Control.Down,
            Handb = car.Control.Handb,
            Newcar = car.Mad.Newcar,
            Mtouch = car.Mad.Mtouch,
            Wtouch = car.Mad.Wtouch,
            Pushed = car.Mad.Pushed,
            Gtouch = car.Mad.Gtouch,
            pl = car.Mad.Pl,
            pr = car.Mad.Pr,
            pd = car.Mad.Pd,
            pu = car.Mad.Pu,
            x = (fix64)car.CarRef.Position.X,
            y = (fix64)car.CarRef.Position.Y,
            z = (fix64)car.CarRef.Position.Z,
            xz = car.CarRef.Rotation.Xz.DegreesSFloat,
            xy = car.CarRef.Rotation.Xy.DegreesSFloat,
            zy = car.CarRef.Rotation.Zy.DegreesSFloat,
            speed = car.Mad.Speed,
            power = car.Mad.Power,
            mxz = car.Mad.Mxz,
            pzy = car.Mad.Pzy,
            pxy = car.Mad.Pxy,
            txz = car.Mad.Txz,
            loop = car.Mad.Loop,
            wxz = (int)car.CarRef.TurningWheelAngle.Xz.DegreesSFloat,
            pcleared = car.Mad.Pcleared,
            clear = car.Mad.Clear,
            nlaps = car.Mad.Nlaps,
            dest = false
        };
    }
}
