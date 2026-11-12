namespace NFMWorld;

/// <summary>
/// Unified renderable interface. Objects submit their draws to a <see cref="RenderQueue"/>
/// during a single pass. Replaces <see cref="IImmediateRenderable"/> and the separate
/// <c>GetRenderData</c> collection pattern.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Called once per frame before <see cref="SubmitDraws"/> to interpolate transforms
    /// and perform any pre-render setup.
    /// </summary>
    void OnBeforeRender(float alpha)
    {
    }

    /// <summary>
    /// Submit all draws for this object to the queue. Called once per object per render pass
    /// (shadow passes + main pass). Use <c>pass.IsShadow</c> to skip effects that don't cast shadows.
    /// </summary>
    void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass);
}
