using MoonWorks.Graphics;
using nfm_world.camera;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.stage;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, GpuBuffer instanceBuffer, int instanceCount);
}