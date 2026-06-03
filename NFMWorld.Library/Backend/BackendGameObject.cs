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
        get => Rotation.ToEuler();
        set => Rotation = FixedQuaternion.FromEuler(value);
    }

    public Matrix MatrixWorld => throw new NotImplementedException();
}