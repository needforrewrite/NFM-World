using Lua;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

[LuaVisible]
public partial interface ITransform
{
    IReadOnlyList<ITransform> ChildTransforms { get; }

    f64Vector3 Position { get; set; }
    f64Euler Rotation { get; set; }
    
    f64Vector3 PositionWithoutInterpolation { set => Position = value; }
    f64Euler RotationWithoutInterpolation { set => Rotation = value; }

    ITransform? Parent { get; }
}