using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Maxine.Extensions;
using TightPack;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[TightPackable]
[TightPackAllowSerialize]
[PluginVisible]
public partial struct CarFrame
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [TightPackable]
    [TightPackAllowSerialize]
    [PluginVisible]
    public partial struct BitFlags
    {
        public Nibble<uint> Values;

        [PluginVisible]
        public bool Right
        {
            readonly get => Values[0];
            set => Values[0] = value;
        }

        [PluginVisible]
        public bool Left
        {
            readonly get => Values[1];
            set => Values[1] = value;
        }

        [PluginVisible]
        public bool Up
        {
            readonly get => Values[2];
            set => Values[2] = value;
        }

        [PluginVisible]
        public bool Down
        {
            readonly get => Values[3];
            set => Values[3] = value;
        }

        [PluginVisible]
        public bool Handb
        {
            readonly get => Values[4];
            set => Values[4] = value;
        }

        [PluginVisible]
        public bool Mtouch
        {
            readonly get => Values[5];
            set => Values[5] = value;
        }

        [PluginVisible]
        public bool Wtouch
        {
            readonly get => Values[6];
            set => Values[6] = value;
        }

        [PluginVisible]
        public bool Gtouch
        {
            readonly get => Values[7];
            set => Values[7] = value;
        }

        [PluginVisible]
        public bool Pu
        {
            readonly get => Values[8];
            set => Values[8] = value;
        }

        [PluginVisible]
        public bool Pd
        {
            readonly get => Values[9];
            set => Values[9] = value;
        }

        [PluginVisible]
        public bool Pl
        {
            readonly get => Values[10];
            set => Values[10] = value;
        }

        [PluginVisible]
        public bool Pr
        {
            readonly get => Values[11];
            set => Values[11] = value;
        }

        [PluginVisible]
        public bool Pushed
        {
            readonly get => Values[12];
            set => Values[12] = value;
        }

        [PluginVisible]
        public bool Newcar
        {
            readonly get => Values[13];
            set => Values[13] = value;
        }

        [PluginVisible]
        public bool BadLanding
        {
            readonly get => Values[14];
            set => Values[14] = value;
        }

        [PluginVisible]
        public bool Wasted
        {
            readonly get => Values[15];
            set => Values[15] = value;
        }

        [PluginVisible]
        public bool Surfer
        {
            readonly get => Values[16];
            set => Values[16] = value;
        }
    }

    [PluginVisible] public (fix64 X, fix64 Y, fix64 Z) CarPosition;
    [PluginVisible] public (fix64 Xz, fix64 Pxy, fix64 Pzy) CarRotation;
    [PluginVisible] public (InlineArray4<fix64> Scx, InlineArray4<fix64> Scy, InlineArray4<fix64> Scz) WheelVelocities;
    [PluginVisible] public fix64 Power;
    [PluginVisible] public int Damage;
    [PluginVisible] public (fix64 Ucomp, fix64 Dcomp, fix64 Lcomp, fix64 Rcomp) AngularVelocities;
    [PluginVisible] public (byte Lap, ushort CheckpointInlap) RacePosition;
    [PluginVisible] public (sbyte StuntType, fix64 Travxz, fix64 Travxy, fix64 Travzy) StuntState;
    [PluginVisible] public fix64 Powerup;
    [PluginVisible] public fix64 Speed;
    [PluginVisible] public (fix64 Mxz, fix64 Txz) XzReadings;
    [PluginVisible] public BitFlags TheBitFlags;

    /// <summary>Camera headings, so a replay can drive the follow and static cameras.</summary>
    [PluginVisible] public (fix64 Cxz, fix64 StaticXz) CameraAngles;

    [PluginVisible]
    public static CarFrame Create(BackendCar car)
    {
        CarFrame entry = new CarFrame();

        entry.TheBitFlags.Up = car.Control.Up;
        entry.TheBitFlags.Down = car.Control.Down;
        entry.TheBitFlags.Left = car.Control.Left;
        entry.TheBitFlags.Right = car.Control.Right;
        entry.TheBitFlags.Handb = car.Control.Handb;
        entry.CarPosition.X = car.Position.X;
        entry.CarPosition.Y = car.Position.Y;
        entry.CarPosition.Z = car.Position.Z;
        entry.CarRotation.Xz = car.Rotation.Xz.Degrees;
        entry.CarRotation.Pxy = car.CarPhysics.Pxy;
        entry.CarRotation.Pzy = car.CarPhysics.Pzy;
        for (var i = 0; i < 4; i++)
        {
            entry.WheelVelocities.Scx[i] = car.CarPhysics.Scx[i];
            entry.WheelVelocities.Scy[i] = car.CarPhysics.Scy[i];
            entry.WheelVelocities.Scz[i] = car.CarPhysics.Scz[i];
        }

        entry.Power = car.CarPhysics.Power;
        entry.Damage = car.CarPhysics.DamagePoints;
        entry.AngularVelocities.Ucomp = car.CarPhysics.UpComponent;
        entry.AngularVelocities.Dcomp = car.CarPhysics.DownComponent;
        entry.AngularVelocities.Lcomp = car.CarPhysics.LeftComponent;
        entry.AngularVelocities.Rcomp = car.CarPhysics.RightComponent;
        entry.RacePosition.CheckpointInlap = car.CurrentCheckpoint;
        entry.RacePosition.Lap = car.CurrentLap;
        entry.StuntState.StuntType = car.CarPhysics.StuntState;
        entry.StuntState.Travxz = car.CarPhysics.TotalStuntXz;
        entry.StuntState.Travxy = car.CarPhysics.TotalStuntXy;
        entry.StuntState.Travzy = car.CarPhysics.TotalStuntZy;
        entry.TheBitFlags.Surfer = car.CarPhysics.Surfing;
        entry.Powerup = car.CarPhysics.Powerup;
        entry.TheBitFlags.BadLanding = car.CarPhysics.BadLanding;
        entry.TheBitFlags.Wasted = car.CarPhysics.Wasted;
        entry.Speed = car.CarPhysics.Speed;
        entry.TheBitFlags.Mtouch = car.CarPhysics.Mtouch;
        entry.TheBitFlags.Wtouch = car.CarPhysics.Wtouch;
        entry.TheBitFlags.Gtouch = car.CarPhysics.Gtouch;
        entry.TheBitFlags.Pu = car.CarPhysics.PressUp;
        entry.TheBitFlags.Pd = car.CarPhysics.PressDown;
        entry.TheBitFlags.Pl = car.CarPhysics.PressLeft;
        entry.TheBitFlags.Pr = car.CarPhysics.PressRight;
        entry.TheBitFlags.Pushed = car.CarPhysics.Pushed;
        entry.TheBitFlags.Newcar = car.CarPhysics.Newcar;
        entry.XzReadings.Mxz = car.CarPhysics.Mxz;
        entry.XzReadings.Txz = car.CarPhysics.Txz;
        entry.CameraAngles.Cxz = car.CarPhysics.Cxz;
        entry.CameraAngles.StaticXz = car.CarPhysics.StaticCameraXz;

        return entry;
    }

    [PluginVisible]
    public readonly void ApplyToCar(BackendCar car)
    {
        car.Control.Up = TheBitFlags.Up;
        car.Control.Down = TheBitFlags.Down;
        car.Control.Left = TheBitFlags.Left;
        car.Control.Right = TheBitFlags.Right;
        car.Control.Handb = TheBitFlags.Handb;

        f64Vector3 pos = new(CarPosition.X, CarPosition.Y, CarPosition.Z);
        car.Position = pos;

        f64Euler rotation = new(
            f64AngleSingle.FromDegrees(CarRotation.Xz),
            f64AngleSingle.FromDegrees(CarRotation.Pzy),
            f64AngleSingle.FromDegrees(CarRotation.Pxy)
        );
        car.Rotation = rotation;

        car.CarPhysics.Pxy = CarRotation.Pxy;
        car.CarPhysics.Pzy = CarRotation.Pzy;

        for (int i = 0; i < 4; i++)
        {
            car.CarPhysics.Scx[i] = WheelVelocities.Scx[i];
            car.CarPhysics.Scy[i] = WheelVelocities.Scy[i];
            car.CarPhysics.Scz[i] = WheelVelocities.Scz[i];
        }

        car.CarPhysics.Power = Power;
        car.CarPhysics.DamagePoints = Damage;
        car.CarPhysics.UpComponent = AngularVelocities.Ucomp;
        car.CarPhysics.DownComponent = AngularVelocities.Dcomp;
        car.CarPhysics.LeftComponent = AngularVelocities.Lcomp;
        car.CarPhysics.RightComponent = AngularVelocities.Rcomp;

        car.CarPhysics.StuntState = StuntState.StuntType;
        car.CarPhysics.TotalStuntXz = StuntState.Travxz;
        car.CarPhysics.TotalStuntXy = StuntState.Travxy;
        car.CarPhysics.TotalStuntZy = StuntState.Travzy;
        car.CarPhysics.Surfing = TheBitFlags.Surfer;

        car.CarPhysics.Powerup = Powerup;
        car.CarPhysics.BadLanding = TheBitFlags.BadLanding;
        car.CarPhysics.Wasted = TheBitFlags.Wasted;
        car.CarPhysics.Speed = Speed;
        car.CarPhysics.Pushed = TheBitFlags.Pushed;
        car.CarPhysics.Newcar = TheBitFlags.Newcar;

        car.CarPhysics.Mtouch = TheBitFlags.Mtouch;
        car.CarPhysics.Wtouch = TheBitFlags.Wtouch;
        car.CarPhysics.Gtouch = TheBitFlags.Gtouch;

        car.CarPhysics.PressUp = TheBitFlags.Pu;
        car.CarPhysics.PressDown = TheBitFlags.Pd;
        car.CarPhysics.PressLeft = TheBitFlags.Pl;
        car.CarPhysics.PressRight = TheBitFlags.Pr;

        car.CarPhysics.Mxz = XzReadings.Mxz;
        car.CarPhysics.Txz = XzReadings.Txz;
        car.CarPhysics.Cxz = CameraAngles.Cxz;
        car.CarPhysics.StaticCameraXz = CameraAngles.StaticXz;
    }
}
