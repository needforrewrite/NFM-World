namespace NFMWorld.Graphics.Core;

/// <summary>
/// A command buffer that records rendering and upload commands for later submission.
/// </summary>
public interface ICommandBuffer
{
    /// <summary>
    /// Acquires the swapchain backbuffer texture for the given window.
    /// Returns null if the swapchain is unavailable this frame.
    /// </summary>
    ITexture? AcquireSwapchainTexture(IWindow window);

    // ── Uniform Data ─────────────────────────────────────────────

    void PushVertexUniformData<T>(in T uniforms, uint slot = 0) where T : unmanaged;
    void PushFragmentUniformData<T>(in T uniforms, uint slot = 0) where T : unmanaged;

    // ── Render Pass ──────────────────────────────────────────────

    IRenderPass BeginRenderPass(params ReadOnlySpan<ColorTargetInfo> colorTargetInfos);
    IRenderPass BeginRenderPass(in DepthStencilTargetInfo depthStencilTargetInfo, params ReadOnlySpan<ColorTargetInfo> colorTargetInfos);
    void EndRenderPass(IRenderPass renderPass);

    // ── Copy Pass ────────────────────────────────────────────────

    ICopyPass BeginCopyPass();
    void EndCopyPass(ICopyPass copyPass);
}
