using FixedMathSharp;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Backend;

public class BackendGameObject : ITransform
{
    public List<BackendGameObject> Children { get; }
    IReadOnlyList<ITransform> ITransform.ChildTransforms => Children;

    public BackendGameObject? Parent { get; set; }
    ITransform? ITransform.Parent => Parent;

    public f64Vector3 Position { get; set; }
    public FixedQuaternion Rotation { get; set; }

    public f64Euler EulerAngles
    {
        get
        {
            var euler = Rotation.ToEulerAngles();
            return new f64Euler(f64AngleSingle.FromDegrees(euler.Y), f64AngleSingle.FromDegrees(euler.X), f64AngleSingle.FromDegrees(euler.Z));
        }
        set => Rotation = FixedQuaternion.FromEulerAnglesInDegrees(value.Yaw.Degrees, value.Pitch.Degrees, value.Roll.Degrees);
    }

    public Matrix MatrixWorld => throw new NotImplementedException();
}