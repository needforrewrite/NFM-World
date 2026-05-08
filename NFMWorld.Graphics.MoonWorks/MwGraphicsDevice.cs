using MoonWorks.Storage;
using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

public sealed class MwGraphicsDevice : Core.IGraphicsDevice
{
    internal readonly MW.GraphicsDevice Inner;
    private readonly TitleStorage _storage;

    public MwGraphicsDevice(MW.GraphicsDevice device, TitleStorage storage)
    {
        Inner = device;
        _storage = storage;
    }

    // ── Device Properties ────────────────────────────────────────

    public string Backend => Inner.Backend;
    public Core.TextureFormat SupportedDepthFormat => Convert.ToCore(Inner.SupportedDepthFormat);
    public Core.TextureFormat SupportedDepthStencilFormat => Convert.ToCore(Inner.SupportedDepthStencilFormat);

    // ── Command Buffer ───────────────────────────────────────────

    public Core.ICommandBuffer AcquireCommandBuffer() =>
        new MwCommandBuffer(Inner.AcquireCommandBuffer());

    public void Submit(Core.ICommandBuffer commandBuffer) =>
        Inner.Submit(((MwCommandBuffer)commandBuffer).Inner);

    public Core.IFence SubmitAndAcquireFence(Core.ICommandBuffer commandBuffer) =>
        new MwFence(Inner.SubmitAndAcquireFence(((MwCommandBuffer)commandBuffer).Inner));

    public void WaitForFence(Core.IFence fence) =>
        Inner.WaitForFence(Convert.Unwrap(fence));

    public void ReleaseFence(Core.IFence fence) =>
        Inner.ReleaseFence(Convert.Unwrap(fence));

    public void Wait() => Inner.Wait();

    // ── Resource Creation ────────────────────────────────────────

    public Core.ITexture CreateTexture2D(uint width, uint height, Core.TextureFormat format, Core.TextureUsageFlags usage, uint levelCount = 1) =>
        new MwTexture(MW.Texture.Create2D(Inner, width, height, Convert.ToMW(format), Convert.ToMW(usage), levelCount));

    public Core.ITexture CreateTexture2D(string? name, uint width, uint height, Core.TextureFormat format, Core.TextureUsageFlags usage, uint levelCount = 1) =>
        new MwTexture(MW.Texture.Create2D(Inner, name!, width, height, Convert.ToMW(format), Convert.ToMW(usage), levelCount));

    public Core.IBuffer CreateBuffer<T>(Core.BufferUsageFlags usage, uint elementCount) where T : unmanaged =>
        new MwBuffer(MW.Buffer.Create<T>(Inner, Convert.ToMW(usage), elementCount));

    public Core.IBuffer CreateBuffer<T>(string? name, Core.BufferUsageFlags usage, uint elementCount) where T : unmanaged =>
        new MwBuffer(MW.Buffer.Create<T>(Inner, name!, Convert.ToMW(usage), elementCount));

    public Core.ITransferBuffer CreateTransferBuffer<T>(uint elementCount) where T : unmanaged =>
        new MwTransferBuffer(MW.TransferBuffer.Create<T>(Inner, MW.TransferBufferUsage.Upload, elementCount));

    public Core.ITransferBuffer CreateTransferBuffer<T>(string? name, uint elementCount) where T : unmanaged =>
        new MwTransferBuffer(MW.TransferBuffer.Create<T>(Inner, name!, MW.TransferBufferUsage.Upload, elementCount));

    public Core.ISampler CreateSampler(in Core.SamplerCreateInfo createInfo) =>
        new MwSampler(MW.Sampler.Create(Inner, Convert.ToMW(createInfo)));

    public Core.IGraphicsPipeline CreateGraphicsPipeline(in Core.GraphicsPipelineCreateInfo createInfo)
    {
        var colorTargets = new MW.ColorTargetDescription[createInfo.TargetInfo.ColorTargetDescriptions.Length];
        for (int i = 0; i < colorTargets.Length; i++)
        {
            colorTargets[i] = new MW.ColorTargetDescription
            {
                Format = Convert.ToMW(createInfo.TargetInfo.ColorTargetDescriptions[i].Format),
                BlendState = Convert.ToMW(createInfo.TargetInfo.ColorTargetDescriptions[i].BlendState),
            };
        }

        var vertexBufferDescs = new MW.VertexBufferDescription[createInfo.VertexInputState.VertexBufferDescriptions.Length];
        for (int i = 0; i < vertexBufferDescs.Length; i++)
        {
            ref var s = ref createInfo.VertexInputState.VertexBufferDescriptions[i];
            vertexBufferDescs[i] = new MW.VertexBufferDescription
            {
                Slot = s.Slot,
                Pitch = s.Pitch,
                InputRate = Convert.ToMW(s.InputRate),
                InstanceStepRate = s.InstanceStepRate,
            };
        }

        var vertexAttrs = new MW.VertexAttribute[createInfo.VertexInputState.VertexAttributes.Length];
        for (int i = 0; i < vertexAttrs.Length; i++)
        {
            ref var a = ref createInfo.VertexInputState.VertexAttributes[i];
            vertexAttrs[i] = new MW.VertexAttribute
            {
                Location = a.Location,
                BufferSlot = a.BufferSlot,
                Format = Convert.ToMW(a.Format),
                Offset = a.Offset,
            };
        }

        var mwCreateInfo = new MW.GraphicsPipelineCreateInfo
        {
            Name = createInfo.Name,
            VertexShader = Convert.Unwrap(createInfo.VertexShader),
            FragmentShader = Convert.Unwrap(createInfo.FragmentShader),
            VertexInputState = new MW.VertexInputState
            {
                VertexBufferDescriptions = vertexBufferDescs,
                VertexAttributes = vertexAttrs,
            },
            PrimitiveType = Convert.ToMW(createInfo.PrimitiveType),
            RasterizerState = Convert.ToMW(createInfo.RasterizerState),
            MultisampleState = Convert.ToMW(createInfo.MultisampleState),
            DepthStencilState = Convert.ToMW(createInfo.DepthStencilState),
            TargetInfo = new MW.GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargets,
                HasDepthStencilTarget = createInfo.TargetInfo.HasDepthStencilTarget,
                DepthStencilFormat = Convert.ToMW(createInfo.TargetInfo.DepthStencilFormat),
            },
        };

        return new MwGraphicsPipeline(MW.GraphicsPipeline.Create(Inner, mwCreateInfo));
    }

    public Core.IResourceUploader CreateResourceUploader(uint initialSize = 0) =>
        new MwResourceUploader(new MW.ResourceUploader(Inner, initialSize));

    // ── Shader Compilation ───────────────────────────────────────

    public Core.IShader CreateShaderFromHLSL(
        string filePath,
        string entryPoint,
        Core.ShaderStage stage,
        string? name = null,
        string? includeDir = null,
        ReadOnlySpan<Core.ShaderDefine> defines = default)
    {
        var mwDefines = new MW.ShaderCross.HLSLDefine[defines.Length];
        for (int i = 0; i < defines.Length; i++)
            mwDefines[i] = new MW.ShaderCross.HLSLDefine(defines[i].Name, defines[i].Value);

        var shader = MW.ShaderCross.Create(
            Inner,
            _storage,
            filePath,
            entryPoint,
            MW.ShaderCross.ShaderFormat.HLSL,
            Convert.ToMW(stage),
            name: name,
            includeDir: includeDir,
            defines: mwDefines);

        return new MwShader(shader);
    }

    // ── Presentation ─────────────────────────────────────────────

    public bool SetSwapchainParameters(Core.IWindow window, Core.SwapchainComposition composition, Core.PresentMode presentMode) =>
        Inner.SetSwapchainParameters(Convert.Unwrap(window), Convert.ToMW(composition), Convert.ToMW(presentMode));

    // ── Dispose ──────────────────────────────────────────────────

    public void Dispose() => Inner.Dispose();
}
