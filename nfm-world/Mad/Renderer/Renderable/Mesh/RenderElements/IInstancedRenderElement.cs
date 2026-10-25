using NFMWorld.Graphics;

namespace NFMWorld;

public interface IInstancedRenderElement
{
    void Render(ICommandBuffer cb, Camera camera, Lighting? lighting, IBuffer instanceBuffer, int instanceCount);
}
