using Lua;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

public interface ICar : ITransform
{
    Rad3d Rad { get; }
    
    [LuaMember("stat")]
    CarStats Stats { get; }
    
    [LuaMember("grat")]
    int GroundAt { get; }
    
    [LuaMember("maxr")]
    int MaxRadius { get; }

    f64Euler WheelAngle { get; set; }
    f64Euler TurningWheelAngle { get; set; }
    IReadOnlyList<Rad3dWheelDef> Wheels { get; }
}