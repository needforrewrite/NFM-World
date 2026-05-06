using nfm_world.camera;

namespace nfm_world.gameobject;

public interface IImmediateRenderable
{
    void OnBeforeRender()
    {
    }

    void Render(Camera camera, Lighting? lighting);
}