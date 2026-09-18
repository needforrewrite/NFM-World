using System.Runtime.InteropServices;
using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Bundles a parsed FNA3D Effect (technique + reflected parameter pointers) with the baked
/// blend/depth/rasterizer state and per-slot vertex declarations that used to be set piecemeal
/// against FNA's GraphicsDevice - see PipelineDesc's doc comments. One FNA3D_VertexDeclaration is
/// built and pinned per <see cref="PipelineDesc.VertexLayouts"/> slot for the pipeline's lifetime,
/// since FNA3D_VertexBufferBinding.vertexDeclaration.elements is a raw pointer that must stay
/// valid for every draw call, not just the call that built it.
/// </summary>
internal sealed class FNA3DPipelineState : IPipelineState
{
    public PipelineDesc Desc { get; }
    public FNA3D_BlendState BlendState { get; }
    public FNA3D_DepthStencilState DepthStencilState { get; }
    public FNA3D_RasterizerState RasterizerState { get; }

    /// <summary>FNA3D_Effect* - both PipelineDesc.VertexShader and PixelShader must be the same FNA3DShaderModule (see its doc comments); the whole compiled Effect blob backs this one pipeline.</summary>
    public IntPtr EffectHandle { get; private set; }

    /// <summary>MOJOSHADER_effectTechnique* selected for this pipeline (the first technique, since none of these shaders currently define more than one).</summary>
    public IntPtr TechniquePointer { get; }
    public int PassCount { get; }

    /// <summary>Raw MOJOSHADER_effectValue.values pointers, indexed the same as Desc's shader Reflection.Uniforms - see MojoShaderEffectReflection.</summary>
    public IReadOnlyList<IntPtr> UniformValuePointers { get; }

    public ShaderReflection Reflection { get; }

    public FNA3D_VertexDeclaration[] VertexDeclarations { get; }
    public int[] InstanceFrequencies { get; }

    private readonly IntPtr _device;
    private readonly GCHandle[] _pinnedElementArrays;

    public FNA3DPipelineState(IntPtr device, PipelineDesc desc, IntPtr effectHandle, IntPtr effectData, string? techniqueName = null)
    {
        _device = device;
        Desc = desc;
        EffectHandle = effectHandle;
        BlendState = Mapping.ToNative(desc.BlendState);
        DepthStencilState = Mapping.ToNative(desc.DepthStencilState);
        RasterizerState = Mapping.ToNative(desc.RasterizerState);

        TechniquePointer = MojoShaderEffectReflection.FindTechnique(effectData, techniqueName, out var passCount);
        PassCount = passCount;

        var built = MojoShaderEffectReflection.Build(effectData);
        Reflection = built.Reflection;
        UniformValuePointers = built.ValuePointers;

        VertexDeclarations = new FNA3D_VertexDeclaration[desc.VertexLayouts.Count];
        InstanceFrequencies = new int[desc.VertexLayouts.Count];
        _pinnedElementArrays = new GCHandle[desc.VertexLayouts.Count];
        for (var slot = 0; slot < desc.VertexLayouts.Count; slot++)
        {
            var layout = desc.VertexLayouts[slot];
            var elements = new FNA3D_VertexElement[layout.Attributes.Count];
            for (var i = 0; i < layout.Attributes.Count; i++)
            {
                var attr = layout.Attributes[i];
                elements[i] = new FNA3D_VertexElement
                {
                    Offset = attr.OffsetInBytes,
                    VertexElementFormat = Mapping.ToNative(attr.Format),
                    VertexElementUsage = Mapping.ToNativeVertexUsage(attr.Semantic),
                    UsageIndex = attr.Slot,
                };
            }

            var handle = GCHandle.Alloc(elements, GCHandleType.Pinned);
            _pinnedElementArrays[slot] = handle;
            VertexDeclarations[slot] = new FNA3D_VertexDeclaration
            {
                vertexStride = layout.StrideInBytes,
                elementCount = elements.Length,
                elements = handle.AddrOfPinnedObject(),
            };
            InstanceFrequencies[slot] = layout.InstanceStepRate;
        }
    }

    public void Dispose()
    {
        foreach (var handle in _pinnedElementArrays)
        {
            if (handle.IsAllocated) handle.Free();
        }

        if (EffectHandle == IntPtr.Zero) return;
        FNA3D_AddDisposeEffect(_device, EffectHandle);
        EffectHandle = IntPtr.Zero;
    }
}
