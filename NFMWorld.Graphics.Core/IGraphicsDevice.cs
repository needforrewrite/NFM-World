namespace NFMWorld.Graphics.Core;

/// <summary>
/// The main graphics device interface. Provides resource creation and command submission.
/// </summary>
public interface IGraphicsDevice : IDisposable
{
    // ── Device Properties ────────────────────────────────────────

    string Backend { get; }
    TextureFormat SupportedDepthFormat { get; }
    TextureFormat SupportedDepthStencilFormat { get; }

    // ── Command Buffer ───────────────────────────────────────────

    ICommandBuffer AcquireCommandBuffer();
    void Submit(ICommandBuffer commandBuffer);
    IFence SubmitAndAcquireFence(ICommandBuffer commandBuffer);
    void WaitForFence(IFence fence);
    void ReleaseFence(IFence fence);
    void Wait();

    // ── Resource Creation ────────────────────────────────────────

    ITexture CreateTexture2D(uint width, uint height, TextureFormat format, TextureUsageFlags usage, uint levelCount = 1);
    ITexture CreateTexture2D(string? name, uint width, uint height, TextureFormat format, TextureUsageFlags usage, uint levelCount = 1);

    IBuffer CreateBuffer<T>(BufferUsageFlags usage, uint elementCount) where T : unmanaged;
    IBuffer CreateBuffer<T>(string? name, BufferUsageFlags usage, uint elementCount) where T : unmanaged;

    ITransferBuffer CreateTransferBuffer<T>(uint elementCount) where T : unmanaged;
    ITransferBuffer CreateTransferBuffer<T>(string? name, uint elementCount) where T : unmanaged;

    ISampler CreateSampler(in SamplerCreateInfo createInfo);

    IGraphicsPipeline CreateGraphicsPipeline(in GraphicsPipelineCreateInfo createInfo);

    IResourceUploader CreateResourceUploader(uint initialSize = 0);

    // ── Shader Compilation ───────────────────────────────────────

    /// <summary>
    /// Creates a shader from HLSL source. The backend handles cross-compilation.
    /// </summary>
    IShader CreateShaderFromHLSL(
        string filePath,
        string entryPoint,
        ShaderStage stage,
        string? name = null,
        string? includeDir = null,
        ReadOnlySpan<ShaderDefine> defines = default);

    // ── Presentation ─────────────────────────────────────────────

    bool SetSwapchainParameters(IWindow window, SwapchainComposition composition, PresentMode presentMode);
}

/// <summary>
/// A shader preprocessor define (name=value pair).
/// </summary>
public readonly record struct ShaderDefine(string Name, string Value);
