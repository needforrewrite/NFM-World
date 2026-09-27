using NFMWorld.Interp;

namespace NFMWorld;

public class OrthoLightCamera : OrthoCamera
{
    public override void OnBeforeRender(float alpha)
    {
        ProjectionMatrix = Matrix.CreateOrthographic(Width, Height, Near, Far);
        var interpolatedPosition = Interpolation.InterpolateCoord(Position, PreviousState.Position, alpha);
        var interpolatedLookAt = Interpolation.InterpolateCoord(LookAt, PreviousState.LookAt, alpha);
        var interpolatedUp = Interpolation.InterpolateCoord(Up, PreviousState.Up, alpha);
        ViewMatrix = Matrix.CreateLookAt(interpolatedPosition, interpolatedLookAt, interpolatedUp);
        
        // Snap the light camera to shadow map texel boundaries to prevent
        // shadow "swimming" / shimmer when the main camera moves.
        // For an orthographic projection, each texel covers a fixed world-space size.
        float texelSizeX = (float)Width / GameSparker.ShadowResolution;
        float texelSizeY = (float)Height / GameSparker.ShadowResolution;

        // Transform the origin into light view space to find the current sub-texel offset
        Vector3 originInView = Vector3.Transform(Vector3.Zero, ViewMatrix);

        // Round to texel boundaries
        float snappedX = MathF.Floor(originInView.X / texelSizeX) * texelSizeX;
        float snappedY = MathF.Floor(originInView.Y / texelSizeY) * texelSizeY;
        float offsetX = snappedX - originInView.X;
        float offsetY = snappedY - originInView.Y;

        // Apply the rounding as a translation in view space (before projection)
        ViewMatrix = ViewMatrix * Matrix.CreateTranslation(offsetX, offsetY, 0);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}