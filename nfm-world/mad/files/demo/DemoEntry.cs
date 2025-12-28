using MessagePack;
using NFMWorld.Mad;
using SoftFloat;

[MessagePackObject]
public class DemoEntry
{
    [Key(0)] public byte SerializedControl;
    [Key(1)] public (fix64 X, fix64 Y, fix64 Z) CarPosition;
    [Key(2)] public (fix64 Xz, fix64 Pxy, fix64 Pzy) CarRotation;
    [Key(3)] public (List<fix64> Scx, List<fix64> Scy, List<fix64> Scz) WheelVelocities = (new List<fix64>(), new List<fix64>(), new List<fix64>());
    [Key(4)] public fix64 Power;
    [Key(5)] public int Damage;
    [Key(6)] public (fix64 Ucomp, fix64 Dcomp, fix64 Lcomp, fix64 Rcomp) AngularVelocities;
    [Key(7)] public (int Lap, int CheckpointInlap) RacePosition;
    [Key(8)] public (int StuntType, fix64 Travxz, fix64 Travxy, fix64 Travzy, bool Surfer) StuntState;
    [Key(9)] public fix64 Powerup;
    [Key(10)] public bool BadLanding;
    [Key(11)] public bool Wasted;
    [Key(12)] public fix64 Speed;
    [Key(13)] public (bool Mtouch, bool Wtouch, bool Gtouch) Touch;
    [Key(14)] public (bool Pu, bool Pd, bool Pl, bool Pr) P;
    [Key(15)] public bool Pushed;
    [Key(16)] public bool Newcar;
    [Key(17)] public (fix64 Cxz, fix64 Mxz, fix64 Txz) XzReadings;

    public static DemoEntry Create(InGameCar car, int checkpointInLap, int lap)
    {
        DemoEntry entry = new DemoEntry();

        entry.SerializedControl = (byte)car.Control.Encode();
        entry.CarPosition.X = (fix64)car.CarRef.Position.X;
        entry.CarPosition.Y = (fix64)car.CarRef.Position.Y;
        entry.CarPosition.Z = (fix64)car.CarRef.Position.Z;
        entry.CarRotation.Xz = (fix64)car.CarRef.Rotation.Xz.Degrees;
        entry.CarRotation.Pxy = car.Mad.Pxy;
        entry.CarRotation.Pzy = car.Mad.Pzy;
        entry.WheelVelocities.Scx.AddRange(car.Mad.Scx);
        entry.WheelVelocities.Scy.AddRange(car.Mad.Scy);
        entry.WheelVelocities.Scz.AddRange(car.Mad.Scz);
        entry.Power = car.Mad.Power;
        entry.Damage = car.Mad.Hitmag;
        entry.AngularVelocities.Ucomp = car.Mad.Ucomp;
        entry.AngularVelocities.Dcomp = car.Mad.Dcomp;
        entry.AngularVelocities.Lcomp = car.Mad.Lcomp;
        entry.AngularVelocities.Rcomp = car.Mad.Rcomp;
        entry.RacePosition.CheckpointInlap = checkpointInLap;
        entry.RacePosition.Lap = lap;
        entry.StuntState.StuntType = car.Mad.Loop;
        entry.StuntState.Travxz = car.Mad.Travxz;
        entry.StuntState.Travxy = car.Mad.Travxy;
        entry.StuntState.Travzy = car.Mad.Travzy;
        entry.StuntState.Surfer = car.Mad.Surfer;
        entry.Powerup = car.Mad.Powerup;
        entry.BadLanding = car.Mad.BadLanding;
        entry.Wasted = car.Mad.Wasted;
        entry.Speed = car.Mad.Speed;
        entry.Touch.Mtouch = car.Mad.Mtouch;
        entry.Touch.Wtouch = car.Mad.Wtouch;
        entry.Touch.Gtouch = car.Mad.Gtouch;
        entry.P.Pu = car.Mad.Pu;
        entry.P.Pd = car.Mad.Pd;
        entry.P.Pl = car.Mad.Pl;
        entry.P.Pr = car.Mad.Pr;
        entry.Pushed = car.Mad.Pushed;
        entry.Newcar = car.Mad.Newcar;
        entry.XzReadings.Cxz = car.Mad.Cxz;
        entry.XzReadings.Mxz = car.Mad.Mxz;
        entry.XzReadings.Txz = car.Mad.Txz;

        return entry;
    }

    public void ApplyToCar(InGameCar car)
    {
        car.Control.Decode((byte)SerializedControl);

        Vector3 pos = new((float)CarPosition.X, (float)CarPosition.Y, (float)CarPosition.Z);
        car.CarRef.Position = pos;

        Euler rotation = new(AngleSingle.FromDegrees(CarRotation.Xz), AngleSingle.FromDegrees(CarRotation.Pxy), AngleSingle.FromDegrees(CarRotation.Pzy));
        car.CarRef.Rotation = rotation;

        car.Mad.Pxy = CarRotation.Pxy;
        car.Mad.Pzy = CarRotation.Pzy;

        for (int i = 0; i < WheelVelocities.Scx.Count; i++)
            car.Mad.Scx[i] = WheelVelocities.Scx[i];
        for (int i = 0; i < WheelVelocities.Scy.Count; i++)
            car.Mad.Scy[i] = WheelVelocities.Scy[i];
        for (int i = 0; i < WheelVelocities.Scz.Count; i++)
            car.Mad.Scz[i] = WheelVelocities.Scz[i];

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
        car.Mad.Surfer = StuntState.Surfer;

        car.Mad.Powerup = Powerup;
        car.Mad.BadLanding = BadLanding;
        car.Mad.Wasted = Wasted;
        car.Mad.Speed = Speed;
        car.Mad.Pushed = Pushed;
        car.Mad.Newcar = Newcar;

        car.Mad.Mtouch = Touch.Mtouch;
        car.Mad.Wtouch = Touch.Wtouch;
        car.Mad.Gtouch = Touch.Gtouch;

        car.Mad.Pu = P.Pu;
        car.Mad.Pd = P.Pd;
        car.Mad.Pl = P.Pl;
        car.Mad.Pr = P.Pr;

        car.Mad.Cxz = XzReadings.Cxz;
        car.Mad.Mxz = XzReadings.Mxz;
        car.Mad.Txz = XzReadings.Txz;
    }
}