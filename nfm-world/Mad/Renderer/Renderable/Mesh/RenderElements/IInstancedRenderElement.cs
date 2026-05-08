using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld;

public interface IInstancedRenderElement
{
    void Render(Camera.Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount);
}