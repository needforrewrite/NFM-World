using nfm_world.camera;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.renderable.mesh.render_elements;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, GpuBuffer instanceBuffer, int instanceCount);
}