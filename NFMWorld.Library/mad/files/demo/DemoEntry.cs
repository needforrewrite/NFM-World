using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Maxine.Extensions;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Mad.Files.Demo;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DemoEntry
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BitFlags
    {
        public Nibble<uint> Values;
        public bool Right { readonly get => Values[0]; set => Values[0] = value; }
        public bool Left { readonly get => Values[1]; set => Values[1] = value; }
        public bool Up { readonly get => Values[2]; set => Values[2] = value; }
        public bool Down { readonly get => Values[3]; set => Values[3] = value; }
        public bool Handb { readonly get => Values[4]; set => Values[4] = value; }
        public bool Mtouch { readonly get => Values[5]; set => Values[5] = value; }
        public bool Wtouch { readonly get => Values[6]; set => Values[6] = value; }
        public bool Gtouch { readonly get => Values[7]; set => Values[7] = value; }
        public bool Pu { readonly get => Values[8]; set => Values[8] = value; }
        public bool Pd { readonly get => Values[9]; set => Values[9] = value; }
        public bool Pl { readonly get => Values[10]; set => Values[10] = value; }
        public bool Pr { readonly get => Values[11]; set => Values[11] = value; }
        public bool Pushed { readonly get => Values[12]; set => Values[12] = value; }
        public bool Newcar { readonly get => Values[13]; set => Values[13] = value; }
        public bool BadLanding { readonly get => Values[14]; set => Values[14] = value; }
        public bool Wasted { readonly get => Values[15]; set => Values[15] = value; }
        public bool Surfer { readonly get => Values[16]; set => Values[16] = value; }
    }
    
    public (fix64 X, fix64 Y, fix64 Z) CarPosition;
    public (fix64 Xz, fix64 Pxy, fix64 Pzy) CarRotation;
    public (InlineArray4<fix64> Scx, InlineArray4<fix64> Scy, InlineArray4<fix64> Scz) WheelVelocities;
    public fix64 Power;
    public int Damage;
    public (fix64 Ucomp, fix64 Dcomp, fix64 Lcomp, fix64 Rcomp) AngularVelocities;
    public (byte Lap, ushort CheckpointInlap) RacePosition;
    public (sbyte StuntType, fix64 Travxz, fix64 Travxy, fix64 Travzy) StuntState;
    public fix64 Powerup;
    public fix64 Speed;
    public (fix64 Mxz, fix64 Txz) XzReadings;
    public BitFlags TheBitFlags;

    public static DemoEntry Create(IInGameCar car)
    {
        DemoEntry entry = new DemoEntry();

        entry.TheBitFlags.Up = car.Control.Up;
        entry.TheBitFlags.Down = car.Control.Down;
        entry.TheBitFlags.Left = car.Control.Left;
        entry.TheBitFlags.Right = car.Control.Right;
        entry.TheBitFlags.Handb = car.Control.Handb;
        entry.CarPosition.X = car.Position.X;
        entry.CarPosition.Y = car.Position.Y;
        entry.CarPosition.Z = car.Position.Z;
        entry.CarRotation.Xz = car.Rotation.Xz.Degrees;
        entry.CarRotation.Pxy = car.Mad.Pxy;
        entry.CarRotation.Pzy = car.Mad.Pzy;
        for (var i = 0; i < 4; i++)
        {
            entry.WheelVelocities.Scx[i] = car.Mad.Scx[i];
            entry.WheelVelocities.Scy[i] = car.Mad.Scy[i];
            entry.WheelVelocities.Scz[i] = car.Mad.Scz[i];
        }
        entry.Power = car.Mad.Power;
        entry.Damage = car.Mad.Hitmag;
        entry.AngularVelocities.Ucomp = car.Mad.Ucomp;
        entry.AngularVelocities.Dcomp = car.Mad.Dcomp;
        entry.AngularVelocities.Lcomp = car.Mad.Lcomp;
        entry.AngularVelocities.Rcomp = car.Mad.Rcomp;
        entry.RacePosition.CheckpointInlap = car.currentCheckpoint;
        entry.RacePosition.Lap = car.currentLap;
        entry.StuntState.StuntType = car.Mad.Loop;
        entry.StuntState.Travxz = car.Mad.Travxz;
        entry.StuntState.Travxy = car.Mad.Travxy;
        entry.StuntState.Travzy = car.Mad.Travzy;
        entry.TheBitFlags.Surfer = car.Mad.Surfer;
        entry.Powerup = car.Mad.Powerup;
        entry.TheBitFlags.BadLanding = car.Mad.BadLanding;
        entry.TheBitFlags.Wasted = car.Mad.Wasted;
        entry.Speed = car.Mad.Speed;
        entry.TheBitFlags.Mtouch = car.Mad.Mtouch;
        entry.TheBitFlags.Wtouch = car.Mad.Wtouch;
        entry.TheBitFlags.Gtouch = car.Mad.Gtouch;
        entry.TheBitFlags.Pu = car.Mad.Pu;
        entry.TheBitFlags.Pd = car.Mad.Pd;
        entry.TheBitFlags.Pl = car.Mad.Pl;
        entry.TheBitFlags.Pr = car.Mad.Pr;
        entry.TheBitFlags.Pushed = car.Mad.Pushed;
        entry.TheBitFlags.Newcar = car.Mad.Newcar;
        entry.XzReadings.Mxz = car.Mad.Mxz;
        entry.XzReadings.Txz = car.Mad.Txz;

        return entry;
    }

    public void ApplyToCar(IInGameCar car)
    {
        car.Control.Up = TheBitFlags.Up;
        car.Control.Down = TheBitFlags.Down;
        car.Control.Left = TheBitFlags.Left;
        car.Control.Right = TheBitFlags.Right;
        car.Control.Handb = TheBitFlags.Handb;

        f64Vector3 pos = new(CarPosition.X, CarPosition.Y, CarPosition.Z);
        car.Position = pos;

        f64Euler rotation = new(f64AngleSingle.FromDegrees(CarRotation.Xz), f64AngleSingle.FromDegrees(CarRotation.Pxy), f64AngleSingle.FromDegrees(CarRotation.Pzy));
        car.Rotation = rotation;

        car.Mad.Pxy = CarRotation.Pxy;
        car.Mad.Pzy = CarRotation.Pzy;

        for (int i = 0; i < 4; i++)
        {
            car.Mad.Scx[i] = WheelVelocities.Scx[i];
            car.Mad.Scy[i] = WheelVelocities.Scy[i];
            car.Mad.Scz[i] = WheelVelocities.Scz[i];
        }

        car.Mad.Power = Power;
        car.Mad.Hitmag = Damage;
        car.Mad.Ucomp = AngularVelocities.Ucomp;
        car.Mad.Dcomp = AngularVelocities.Dcomp;
        car.Mad.Lcomp = AngularVelocities.Lcomp;
        car.Mad.Rcomp = AngularVelocities.Rcomp;

        car.Mad.Loop = StuntState.StuntType;
        car.Mad.Travxz = StuntState.Travxz;
        car.Mad.Travxy = StuntState.Travxy;
        car.Mad.Travzy = StuntState.Travzy;
        car.Mad.Surfer = TheBitFlags.Surfer;

        car.Mad.Powerup = Powerup;
        car.Mad.BadLanding = TheBitFlags.BadLanding;
        car.Mad.Wasted = TheBitFlags.Wasted;
        car.Mad.Speed = Speed;
        car.Mad.Pushed = TheBitFlags.Pushed;
        car.Mad.Newcar = TheBitFlags.Newcar;

        car.Mad.Mtouch = TheBitFlags.Mtouch;
        car.Mad.Wtouch = TheBitFlags.Wtouch;
        car.Mad.Gtouch = TheBitFlags.Gtouch;

        car.Mad.Pu = TheBitFlags.Pu;
        car.Mad.Pd = TheBitFlags.Pd;
        car.Mad.Pl = TheBitFlags.Pl;
        car.Mad.Pr = TheBitFlags.Pr;

        car.Mad.Mxz = XzReadings.Mxz;
        car.Mad.Txz = XzReadings.Txz;
    }
}