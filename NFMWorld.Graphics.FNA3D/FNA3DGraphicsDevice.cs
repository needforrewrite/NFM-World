using NFMWorld.Graphics;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Placeholder for the FNA3D-backed <see cref="IGraphicsDevice"/> implementation.
/// Milestone 1 fills this in by extracting the FNA3D P/Invoke layer from
/// <c>FNA/src/Graphics/FNA3D.cs</c> into a standalone binding (this project) and implementing
/// device/buffer/texture/pipeline creation directly against FNA3D's native C ABI - not by
/// wrapping FNA's own <c>Microsoft.Xna.Framework.Graphics.GraphicsDevice</c>.
/// </summary>
public sealed class FNA3DGraphicsDevice : IGraphicsDevice
{
    public ISwapchain Swapchain => throw new NotImplementedException("Implemented in Milestone 1.");

    public ICommandBuffer AcquireCommandBuffer() => throw new NotImplementedException("Implemented in Milestone 1.");
    public void Submit(ICommandBuffer commandBuffer) => throw new NotImplementedException("Implemented in Milestone 1.");

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default) => throw new NotImplementedException("Implemented in Milestone 1.");
    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default) => throw new NotImplementedException("Implemented in Milestone 1.");
    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc) => throw new NotImplementedException("Implemented in Milestone 1.");
    public ISampler CreateSampler(SamplerDesc desc) => throw new NotImplementedException("Implemented in Milestone 1.");
    public IPipelineState CreatePipeline(PipelineDesc desc) => throw new NotImplementedException("Implemented in Milestone 1.");
}
