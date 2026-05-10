namespace NFMWorld;

public interface IImmediateRenderable
{
    void OnBeforeRender()
    {
    }

    void Render(Camera camera, Lighting? lighting);
}