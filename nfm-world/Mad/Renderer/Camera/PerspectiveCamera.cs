using Maxine.Extensions.Mathematics;

namespace NFMWorld.Camera;

public class PerspectiveCamera : Camera
{
    public const float DefaultFov = 58.715516388168026651329f;
    public float Fov { get; set; } = DefaultFov;

    /// <summary>When true, uses an orthographic projection instead of perspective.</summary>
    public bool IsOrthographic { get; set; } = false;
    /// <summary>World units per pixel for orthographic projection.</summary>
    public float OrthoScale { get; set; } = 1f;
    
    public override void OnBeforeRender()
    {
        if (IsOrthographic)
        {
            float w = Width * OrthoScale;
            float h = Height * OrthoScale;
            ProjectionMatrix = Matrix.CreateOrthographic(w, h, Near, Far);
        }
        else
        {
            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathUtil.DegreesToRadians(Fov), Width / (float)Height, Near, Far);
        }
        ViewMatrix = Matrix.CreateLookAt(Position, LookAt, Up);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}