using NFMWorld.Graphics.FNA3D.Native;

namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DPipelineState : IPipelineState
{
    public PipelineDesc Desc { get; }
    public FNA3D_BlendState BlendState { get; }
    public FNA3D_DepthStencilState DepthStencilState { get; }
    public FNA3D_RasterizerState RasterizerState { get; }

    /// <summary>FNA3D_Effect* - see IShaderModule doc comments on how VS/PS combine into one Effect blob (Milestone 3).</summary>
    public IntPtr EffectHandle { get; private set; }
    public IntPtr EffectDataHandle { get; private set; }
    private readonly IntPtr _device;

    public FNA3DPipelineState(IntPtr device, PipelineDesc desc, IntPtr effectHandle, IntPtr effectDataHandle)
    {
        _device = device;
        Desc = desc;
        EffectHandle = effectHandle;
        EffectDataHandle = effectDataHandle;
        BlendState = Mapping.ToNative(desc.BlendState);
        DepthStencilState = Mapping.ToNative(desc.DepthStencilState);
        RasterizerState = Mapping.ToNative(desc.RasterizerState);
    }

    public void Dispose()
    {
        if (EffectHandle == IntPtr.Zero) return;
        FNA3DNative.FNA3D_AddDisposeEffect(_device, EffectHandle);
        EffectHandle = IntPtr.Zero;
    }
}
