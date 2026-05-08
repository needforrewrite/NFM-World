namespace NFMWorld;

public interface IImmediateRenderable
{
    void OnBeforeRender()
    {
    }

    void Render(Camera.Camera camera, Lighting? lighting);
}