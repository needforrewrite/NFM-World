using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class PerspectiveCamera : Camera
{
    public float Fov { get; set; } = 58.715516388168026651329f;
    
    public override void OnBeforeRender()
    {
        ProjectionMatrix = Microsoft.Xna.Framework.Matrix.CreatePerspectiveFieldOfView(MathUtil.DegreesToRadians(Fov), Width / (float)Height, Near, Far);
        ViewMatrix = Microsoft.Xna.Framework.Matrix.CreateLookAt(Position.ToXna(), LookAt.ToXna(), Up.ToXna());
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}