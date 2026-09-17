namespace NFMWorld.Graphics;

/// <summary>
/// Records (and, for an immediate-mode backend like FNA3D, simultaneously executes) a single
/// batch of draw work. Only one command buffer may be live at a time
/// (<see cref="IGraphicsDevice.AcquireCommandBuffer"/>/<see cref="IGraphicsDevice.Submit"/>),
/// which keeps this compatible with D3D11/OpenGL's single immediate-context model while leaving
/// room for a future Vulkan/Metal backend that actually defers/replays recorded commands.
/// </summary>
public interface ICommandBuffer
{
    void SetPipeline(IPipelineState pipeline);
    void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0);
    void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0);
    void SetShaderResource(int slot, ITexture texture, ISampler sampler);

    /// <summary>
    /// Sets a shader uniform value. The FNA3D backend implements this by writing into the
    /// wrapped Effect's parameters before applying it - FNA3D has no GPU-visible constant
    /// buffer object, so this is deliberately not modeled as an <see cref="IBuffer"/>.
    /// </summary>
    void SetUniform(int slot, ReadOnlySpan<byte> value);

    /// <summary>Null targets the swapchain's backbuffer.</summary>
    void SetRenderTarget(IRenderTarget? target);

    void SetViewport(Viewport viewport);

    /// <summary>
    /// Because this backend executes synchronously rather than deferring, the data is pinned
    /// and passed straight through to the native API - no extra CPU-side copy is made here.
    /// </summary>
    void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0);

    void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0);
    void Draw(int startVertex, int primitiveCount);
    void DrawIndexed(int baseVertex, int startIndex, int primitiveCount);
    void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount);
}
