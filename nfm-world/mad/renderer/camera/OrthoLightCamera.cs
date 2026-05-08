namespace nfm_world.camera;

public class OrthoLightCamera : OrthoCamera
{
    /// <summary>
    /// Shadow map resolution used for texel snapping. Must match the actual
    /// shadow render target size (see WorldGame.LoadShadowTargets).
    /// </summary>
    private const int ShadowMapSize = 2048;

    public override void OnBeforeRender()
    {
        ProjectionMatrix = Matrix.CreateOrthographic(Width, Height, Near, Far);
        ViewMatrix = Matrix.CreateLookAt(Position, LookAt, Up);

        // Snap the light camera to shadow map texel boundaries to prevent
        // shadow "swimming" / shimmer when the main camera moves.
        // For an orthographic projection, each texel covers a fixed world-space size.
        float texelSizeX = (float)Width / ShadowMapSize;
        float texelSizeY = (float)Height / ShadowMapSize;

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