using nfm_world.camera;

namespace nfm_world;

public interface IImmediateRenderable
{
    void OnBeforeRender()
    {
    }

    void Render(Camera camera, Lighting? lighting);
}