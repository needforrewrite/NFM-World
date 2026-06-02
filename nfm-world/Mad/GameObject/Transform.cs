using FixedMathSharp;
using NFMWorld.Interp;
using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorld;

public abstract class Transform : ITransform
{
    public readonly record struct TransformState(f64Vector3 Position, f64Euler Rotation);
    
    public TransformState PreviousState { get; private set; }
    
    public abstract IReadOnlyList<ITransform> ChildTransforms { get; }

    public f64Vector3 Position { get; set; } = f64Vector3.Zero;
    public FixedQuaternion Rotation { get; set; } = new();
    public Transform? Parent { get; set; }
    
    public f64Euler EulerAngles
    {
        get
        {
            var euler = Rotation.ToEulerAngles();
            return new f64Euler(f64AngleSingle.FromDegrees(euler.Y), f64AngleSingle.FromDegrees(euler.X), f64AngleSingle.FromDegrees(euler.Z));
        }
        set => Rotation = FixedQuaternion.FromEulerAnglesInDegrees(value.Yaw.Degrees, value.Pitch.Degrees, value.Roll.Degrees);
    }

    public f64Vector3 PositionWithoutInterpolation
    {
        set
        {
            PreviousState = PreviousState with { Position = value };
            Position = value;
        }
    }

    public f64Euler RotationWithoutInterpolation
    {
        set
        {
            PreviousState = PreviousState with { Rotation = value };
            EulerAngles = value;
        }
    }

    ITransform? ITransform.Parent => Parent;

    public Matrix MatrixWorld { get; private set; }

    public virtual void GameTick(IStage? stage = null)
    {
        PreviousState = new TransformState(Position, EulerAngles);
    }

    public virtual void OnBeforeRender(float alpha)
    {
        var interpolatedPosition = Interpolation.InterpolateCoord((Vector3)Position, (Vector3)PreviousState.Position, alpha);
        var interpolatedRotation = Interpolation.InterpolateEuler((Euler)EulerAngles, (Euler)PreviousState.Rotation, alpha);

        var ownMatrixWorld = Matrix.CreateFromEuler(interpolatedRotation) * Matrix.CreateTranslation(interpolatedPosition);
        if (Parent != null)
        {
            ownMatrixWorld = ownMatrixWorld * Parent.MatrixWorld;
        }
        MatrixWorld = ownMatrixWorld;
    }
}