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

    // TODO(Milestone 5 Stage B follow-up): RenderQueue.Flush now needs a live ICommandBuffer,
    // which this method (an ad hoc, editor-only immediate-render path with no caller-supplied
    // command buffer) has no way to acquire on its own - IGraphicsDevice only allows one command
    // buffer live at a time (see IGraphicsDevice.AcquireCommandBuffer's doc comment), so acquiring
    // one here would conflict with whatever the real per-frame render pass is doing. Stubbed
    // (no-op) so the project compiles; not reached today since GameSparker.Load never runs (see
    // WorldGame.cs's Stage A TODOs).
    public void Render(Camera camera, Lighting? lighting)
    {
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        var boundingSphere = new BoundingSphere(new Vector3(0, 0, 0), MaxRadius);
        SubmitRenderables(queue, lighting, false, boundingSphere, RenderBucket.StagePieces, Matrix.Identity, 0, true, 1.0f, false, false);
    }
}