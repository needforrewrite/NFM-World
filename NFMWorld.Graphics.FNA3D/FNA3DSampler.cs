namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// FNA3D has no persistent sampler-state object in its native API - sampler state is applied
/// per-draw via FNA3D_VerifySampler, so this just carries the descriptor for the command buffer
/// to apply when a texture is bound (see FNA3DCommandBuffer.SetShaderResource).
/// </summary>
internal sealed class FNA3DSampler(SamplerDesc desc) : ISampler
{
    public SamplerDesc Desc { get; } = desc;

    public void Dispose() { }
}
