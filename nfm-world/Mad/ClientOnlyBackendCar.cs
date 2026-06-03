using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class ClientOnlyBackendCar(Rad3d rad) : ICar
{
    public Rad3d Rad { get; } = rad;
    public CarStats Stats { get; } = CarStats.ValidateStats(rad.Stats, rad.FileName);
    public int GroundAt { get; set; }
    public int MaxRadius { get; } = rad.MaxRadius;
    public f64Euler WheelAngle { get; set; }
    public f64Euler TurningWheelAngle { get; set; }
    public IReadOnlyList<Rad3dWheelDef> Wheels { get; } = rad.Wheels;

    public f64Vector3 Position { get; set; }
    public FixedQuaternion Rotation { get; set; }
    
    public f64Euler EulerAngles
    {
        get => Rotation.ToEuler();
        set => Rotation = FixedQuaternion.FromEuler(value);
    }

    IReadOnlyList<ITransform> ITransform.ChildTransforms => [];
    ITransform? ITransform.Parent => null;
}