using NFMWorld.Graphics;

namespace NFMWorld;

public interface IImmediateRenderElement
{
    void Render(ICommandBuffer cb, Camera camera, Lighting? lighting);
}
