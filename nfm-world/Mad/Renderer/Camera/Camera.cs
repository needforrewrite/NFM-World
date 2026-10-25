
using Maxine.Extensions.Mathematics;

namespace NFMWorld;

public abstract class Camera
{
    public readonly record struct CameraState(Vector3 Position, Vector3 LookAt, Vector3 Up);
    
    public CameraState PreviousState { get; private set; }

    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public float Near { get; set; } = 50f;
    public float Far { get; set; } = 1_000_000f;
    
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 LookAt { get; set; } = Vector3.UnitZ;
    public Vector3 Up { get; set; } = -Vector3.UnitY;

    public Vector3 PositionWithoutInterpolation
    {
        set 
        {
            PreviousState = PreviousState with { Position = value };
            Position = value;
        }
    }
    
    public Vector3 LookAtWithoutInterpolation
    {
        set
        {
            PreviousState = PreviousState with { LookAt = value };
            LookAt = value;
        }
    }
    
    public Vector3 UpWithoutInterpolation 
    {
        set 
        {
            PreviousState = PreviousState with { Up = value };
            Up = value;
        }
    }
    
    public Matrix ViewMatrix { get; protected set; }

    public Matrix ProjectionMatrix { get; protected set; }

    public Matrix ViewProjectionMatrix
    {
        get;
        protected set
        {
            field = value;
            Frustum = new BoundingFrustum(value);
        }
    }

    public BoundingFrustum Frustum { get; private set; }

    public virtual void OnBeforeGameTick()
    {
        PreviousState = new CameraState(Position, LookAt, Up);
    }
    
    public abstract void OnBeforeRender(float alpha);
}