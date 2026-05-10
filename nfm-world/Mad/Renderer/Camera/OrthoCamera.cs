namespace NFMWorld;

public class OrthoCamera : Camera
{
    public override void OnBeforeRender()
    {
        ProjectionMatrix = Matrix.CreateOrthographic(Width, Height, Near, Far);
        ViewMatrix = Matrix.CreateLookAt(Position, LookAt, Up);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }
}