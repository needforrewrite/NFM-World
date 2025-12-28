using Maxine.Extensions;
using MessagePack;
using NFMWorld.Mad;
using SoftFloat;

[MessagePackObject]
public class DemoEntry
{
    [Key(0)] public Nibble<byte> SerializedControl;
    [Key(1)] public (fix64 X, fix64 Y, fix64 Z) CarPosition;
    [Key(2)] public (fix64 Xz, fix64 Pxy, fix64 Pzy) CarRotation;
    [Key(3)] public (List<fix64> Scx, List<fix64> Scy, List<fix64> Scz) WheelVelocities;
    [Key(4)] public fix64 Power;
    [Key(5)] public fix64 Damage;
    [Key(6)] public (fix64 Ucomp, fix64 Dcomp, fix64 Lcomp, fix64 Rcomp) AngularVelocities;
    [Key(7)] public (int Lap, int CheckpointInlap) RacePosition;
    [Key(8)] public (int StuntType, fix64 Travxz, fix64 Travxy, fix64 Travzy, bool Surfer) StuntState; /* StuntType = Loop in Mad.cs */
    [Key(9)] public fix64 Powerup;
    [Key(10)] public bool BadLanding;
    [Key(11)] public bool Wasted;
    [Key(12)] public fix64 ResultantSpeed; /* Speed in Mad.cs */
    [Key(13)] public (bool Mtouch, bool Wtouch, bool Gtouch) Touch;
    [Key(14)] public (bool Pu, bool Pd, bool Pl, bool Pr) P;
    [Key(15)] public bool Pushed;
    [Key(16)] public bool Newcar;

    public DemoEntry(Mad mad, Control control, Car car)
    {
        SerializedControl = control.Encode();
        
    }
}