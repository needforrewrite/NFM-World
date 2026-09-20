using NFMWorld.Graphics;
using BoundingSphere = Maxine.Extensions.Mathematics.BoundingSphere;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

/// <summary>
/// DO NOT USE THIS EXCEPT FOR EDITOR STUFF!! IT IS SLOW AS FUCK!!!!!!!!!!
/// </summary>
public class ImmediateMesh : Mesh, IRenderable
{
    public ImmediateMesh(Rad3d rad) : base(rad)
    {
    }

    public ImmediateMesh(Mesh baseMesh) : base(baseMesh)
    {
    }

    /// <summary>
    /// Ad hoc immediate-render path for editor code: builds a throwaway render queue and flushes it
    /// straight into the caller's command buffer. The caller must own that buffer (this runs inside
    /// an enclosing render pass - see <see cref="IGraphicsDevice.AcquireCommandBuffer"/>'s "one
    /// buffer at a time" rule); it deliberately does not acquire one of its own.
    /// </summary>
    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting)
    {
        var renderQueue = new RenderQueue(GameSparker.NewGraphicsDevice);
        renderQueue.Begin(camera, lighting);
        SubmitDraws(renderQueue, camera, lighting, RenderPass.Main());
        renderQueue.Flush(cb);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        var boundingSphere = new BoundingSphere(new Vector3(0, 0, 0), MaxRadius);
        SubmitRenderables(queue, lighting, false, boundingSphere, RenderBucket.StagePieces, Matrix.Identity, 0, true, 1.0f, false, false);
    }
}