using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public abstract class Camera
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public float Near { get; set; } = 50f;
    public float Far { get; set; } = 1_000_000f;
    
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 LookAt { get; set; } = Vector3.UnitZ;
    public Vector3 Up  { get; set; } = -Vector3.UnitY;

    public Microsoft.Xna.Framework.Matrix ViewMatrix { get; protected set; }

    public Microsoft.Xna.Framework.Matrix ProjectionMatrix { get; protected set; }

    public Microsoft.Xna.Framework.Matrix ViewProjectionMatrix { get; protected set; }

    public abstract void OnBeforeRender();
}