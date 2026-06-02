using FixedMathSharp;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

public interface ITransform
{
    IReadOnlyList<ITransform> ChildTransforms { get; }

    f64Vector3 Position { get; set; }
    FixedQuaternion Rotation { get; set; }

    f64Euler EulerAngles
    {
        get
        {
            var euler = Rotation.ToEulerAngles();
            return new f64Euler(f64AngleSingle.FromDegrees(euler.Y), f64AngleSingle.FromDegrees(euler.X), f64AngleSingle.FromDegrees(euler.Z));
        }
        set => Rotation = FixedQuaternion.FromEulerAnglesInDegrees(value.Yaw.Degrees, value.Pitch.Degrees, value.Roll.Degrees);
    }

    f64Vector3 PositionWithoutInterpolation { set => Position = value; }
    f64Euler RotationWithoutInterpolation { set => EulerAngles = value; }

    ITransform? Parent { get; }
}