using NFMWorld.Interp;

namespace NFMWorld;

public class OrthoCamera : Camera
{
    /// <summary>World units per pixel for orthographic projection.</summary>
    public float OrthoScale { get; set; } = 1f;

    public override void OnBeforeRender(float alpha)
    {
        // Guard against uninitialised viewport: Width or Height of zero produces
        // infinite values in the orthographic projection matrix.
        var w = Math.Max(Width, 1);
        var h = Math.Max(Height, 1);

        ProjectionMatrix = Matrix.CreateOrthographic(w * OrthoScale, h * OrthoScale, Near, Far);
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