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
        get => Rotation.ToEuler();
        set => Rotation = FixedQuaternion.FromEuler(value);
    }

    f64Vector3 PositionWithoutInterpolation { set => Position = value; }
    f64Euler RotationWithoutInterpolation { set => EulerAngles = value; }

    ITransform? Parent { get; }
}