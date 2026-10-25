using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Wraps a compiled D3D9 Effects Framework blob (.fxb, produced by fxc.exe - see
/// nfm-world/NFMWorld.csproj's BuildShaders target) as an <see cref="IShaderModule"/>. FNA3D's
/// Effect API (FNA3D_CreateEffect/FNA3D_ApplyEffect) has no way to submit a standalone vertex or
/// pixel shader - only a whole compiled Effect - so one <see cref="FNA3DShaderModule"/> represents
/// the ENTIRE effect (both stages combined). Per the resolved shader-abstraction design, callers
/// pass the SAME instance for both <see cref="PipelineDesc.VertexShader"/> and
/// <see cref="PipelineDesc.PixelShader"/> to signal this; <see cref="FNA3DGraphicsDevice.CreatePipeline"/>
/// rejects anything else.
/// </summary>
internal sealed class FNA3DShaderModule(ShaderStage stage, ReadOnlyMemory<byte> bytecode) : IShaderModule
{
    public ShaderStage Stage { get; } = stage;
    public ReadOnlyMemory<byte> Bytecode { get; } = bytecode;
}
