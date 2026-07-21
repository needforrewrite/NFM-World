using Maxine.Extensions.Mathematics;
using NFMWorld.Interp;

namespace NFMWorld;

public class PerspectiveCamera : Camera
{
    public const float DefaultFov = 58.715516388168026651329f;
    public float Fov { get; set; } = DefaultFov;
    
    public override void OnBeforeRender(float alpha)
    {
        // Guard against uninitialised viewport: Width or Height of zero produces
        // a NaN aspect ratio which propagates through the projection matrix.
        var w = Math.Max(Width, 1);
        var h = Math.Max(Height, 1);

        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathUtil.DegreesToRadians(Fov), w / (float)h, Near, Far);
        var interpolatedPosition = Interpolation.InterpolateCoord(Position, PreviousState.Position, alpha);
        var interpolatedLookAt = Interpolation.InterpolateCoord(LookAt, PreviousState.LookAt, alpha);
        var interpolatedUp = Interpolation.InterpolateCoord(Up, PreviousState.Up, alpha);

        // If position == lookAt, CreateLookAt will normalise a zero vector → NaN.
        // Guard by falling back to a unit forward vector.
        if (interpolatedPosition == interpolatedLookAt)
        {
            interpolatedLookAt = interpolatedPosition + Vector3.UnitZ;
        }

        ViewMatrix = Matrix.CreateLookAt(interpolatedPosition, interpolatedLookAt, interpolatedUp);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}