using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

/// <summary>
/// DO NOT USE THIS EXCEPT FOR EDITOR STUFF!! IT IS SLOW AS FUCK!!!!!!!!!!
/// </summary>
public class ImmediateMesh : Mesh, IRenderable
{
    public ImmediateMesh(GraphicsDevice graphicsDevice, Rad3d rad) : base(graphicsDevice, rad)
    {
    }

    public ImmediateMesh(Mesh baseMesh) : base(baseMesh)
    {
    }

    public void Render(Camera camera, Lighting? lighting)
    {
        var renderQueue = new RenderQueue(GraphicsDevice);
        renderQueue.Begin(camera, lighting);
        SubmitDraws(renderQueue, camera, lighting, RenderPass.Main());
        renderQueue.Flush();
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        var boundingSphere = new BoundingSphere(new Vector3(0, 0, 0), MaxRadius);
        SubmitRenderables(queue, lighting, false, boundingSphere, RenderBucket.StagePieces, Matrix.Identity, 0, true, 1.0f, false, false);
    }
}