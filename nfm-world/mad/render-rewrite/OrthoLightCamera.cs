namespace NFMWorld.Mad;

public class OrthoLightCamera : OrthoCamera
{
    public override void OnBeforeRender()
    {
        ProjectionMatrix = Matrix.CreateOrthographic(Width, Height, Near, Far);
        ViewMatrix = Matrix.CreateLookAt(Position, LookAt, Up);
        
        // Calculate world units per texel
        float shadowMapSize = 2048f;
        float texelSize = Width / shadowMapSize;

        // Get light's world position in light view space
        Matrix lightView = ViewMatrix;
        Vector3 shadowOrigin = Vector3.Transform(Vector3.Zero, lightView);

        // Snap to texel grid in light view space
        shadowOrigin.X = MathF.Round(shadowOrigin.X / texelSize) * texelSize;
        shadowOrigin.Y = MathF.Round(shadowOrigin.Y / texelSize) * texelSize;

        // Calculate rounding offset
        Vector3 roundOffset = shadowOrigin - Vector3.Transform(Vector3.Zero, lightView);

        // Apply offset by adjusting the view matrix translation
        var viewMatrix = ViewMatrix;
        viewMatrix.M41 += roundOffset.X;
        viewMatrix.M42 += roundOffset.Y;
        viewMatrix.M43 += roundOffset.Z;
        ViewMatrix = viewMatrix;

        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}