using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class PerspectiveCamera : Camera
{
    public const float DefaultFov = 58.715516388168026651329f;
    public float Fov { get; set; } = DefaultFov;
    
    public override void OnBeforeRender()
    {
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathUtil.DegreesToRadians(Fov), Width / (float)Height, Near, Far);
        ViewMatrix = Matrix.CreateLookAt(Position, LookAt, Up);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}