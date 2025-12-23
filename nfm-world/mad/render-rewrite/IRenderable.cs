namespace NFMWorld.Mad;

public interface IRenderable
{
    void OnBeforeRender()
    {
    }

    void Render(Camera camera, Lighting? lighting);
}