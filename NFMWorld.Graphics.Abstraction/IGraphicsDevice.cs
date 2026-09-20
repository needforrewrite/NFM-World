namespace NFMWorld.Graphics;

/// <summary>
/// Backend-agnostic entry point for resource creation and command submission. Concrete
/// implementations: <c>NFMWorld.Graphics.FNA3D</c> (first backend, proves the abstraction) and,
/// later, <c>NFMWorld.Graphics.Sokol</c>.
/// </summary>
public interface IGraphicsDevice
{
    ISwapchain Swapchain { get; }

    /// <summary>
    /// Only one command buffer may be live at a time; implementations must guard against a
    /// second acquisition before the previous one is submitted.
    /// </summary>
    ICommandBuffer AcquireCommandBuffer();

    /// <summary>Executes the command buffer's recorded work (immediately, for an immediate-mode backend).</summary>
    void Submit(ICommandBuffer commandBuffer);

    IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default);
    ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default);
    IRenderTarget CreateRenderTarget(RenderTargetDesc desc);
    ISampler CreateSampler(SamplerDesc desc);
    IPipelineState CreatePipeline(PipelineDesc desc);

    /// <summary>
    /// Synchronous CPU readback of a texture's pixels into <paramref name="destination"/>, which
    /// must hold <c>width * height * bytesPerPixel</c> tightly packed rows (no padding), in the
    /// same top-down order the texture was uploaded in.
    /// </summary>
    /// <remarks>
    /// This blocks on a GPU stall by design - it exists for editor, export and debug paths, never
    /// for per-frame gameplay work. The texture must not be currently bound as a render target:
    /// callers reading an off-screen <see cref="IRenderTarget"/> must bind the swapchain (or
    /// another target) first. Depth-stencil textures have no readable storage in the backends and
    /// are rejected; read the target's <see cref="IRenderTarget.ColorTexture"/> instead.
    /// </remarks>
    void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0);
}
