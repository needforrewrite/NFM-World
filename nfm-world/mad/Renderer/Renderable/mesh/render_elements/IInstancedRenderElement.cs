using Microsoft.Xna.Framework.Graphics;
using nfm_world.camera;

namespace nfm_world;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount);
}