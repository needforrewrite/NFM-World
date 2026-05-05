using nfm_world.compat;
using MoonWorks.Graphics;
using nfm_world.camera;

namespace nfm_world.stage;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount);
}