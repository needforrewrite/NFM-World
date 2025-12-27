using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount);
}