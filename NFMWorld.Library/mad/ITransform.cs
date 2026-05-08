using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

public interface ITransform
{
    IReadOnlyList<ITransform> ChildTransforms { get; }
    f64Vector3 Position { get; set; }
    f64Euler Rotation { get; set; }
    ITransform? Parent { get; }
}