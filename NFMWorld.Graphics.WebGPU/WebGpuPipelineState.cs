// LLM maintained.
//
// The pipeline state, and the cache in front of it.
//
// WebGPU bakes more into a pipeline than any other backend here: the colour and depth formats, the
// sample count, the blend state as concrete factors, and the whole bind group layout. The
// abstraction, though, creates a pipeline before it knows what it will be drawn into - Poly draws
// into both the swapchain and a shadow cascade, which are different formats and different sample
// counts. So a pipeline here is a *description* that builds a concrete wgpu pipeline on first use
// per variant, keyed on (colour format, depth format, sample count, filterability).
//
// Bind groups are cached beside the variants for the same reason: a bind group names the texture
// views and the sampler it was built from, and those change together in long runs (every piece of
// one mesh, every path of one NanoVG frame).

using NFMWorld.Shaders;
using Wg = Maxine.WebGPU;

namespace NFMWorld.Graphics.WebGPU;

/// <summary>
/// Everything a concrete wgpu pipeline depends on that the abstraction does not state at creation.
/// </summary>
/// <param name="Unfilterable">
/// Whether the device lacks <c>float32-filterable</c>. A filtering sampler cannot be paired with an
/// unfilterable float format, and the layout - which is per pipeline, not per draw - has to commit
/// to one or the other before any texture is bound. Without the feature every binding is laid out
/// unfilterable, which costs filtered texture sampling (the MSDF text atlas) but keeps the shadow
/// maps bindable; with it, which is what every desktop adapter reports, filtering is the layout.
/// </param>
internal readonly record struct WebGpuPipelineKey(
    Wg.TextureFormat ColorFormat,
    Wg.TextureFormat DepthFormat,
    int SampleCount,
    bool Unfilterable
);

internal sealed unsafe class WebGpuPipelineState : IPipelineState
{
    /// <summary>
    /// How many texture and sampler bindings one pipeline may declare. Seven is what the widest
    /// shader in the tree uses (apos-shapes' glyph atlases, arc table, blue noise and ramp), the same
    /// bound the D3D11 backend picked for the same reason.
    /// </summary>
    internal const int ResourceSlots = 7;

    private readonly WebGpuGraphicsDevice _device;
    private readonly WebGpuShaderModule _vertex;
    private readonly WebGpuShaderModule? _pixel;

    private readonly List<WebGpuPipelineVariant> _variants = [];
    private readonly Dictionary<WebGpuPipelineKey, WebGpuPipelineVariant> _byKey = [];

    private WebGpuPipelineKey? _lastKey;
    private WebGpuPipelineVariant? _lastVariant;

    public PipelineDesc Desc { get; }

    public ShaderReflection Reflection => _vertex.Reflection;

    /// <summary>The merged uniform block's size in bytes, rounded to WebGPU's 16-byte struct rule.</summary>
    internal int UniformBlockSize { get; }

    internal bool HasUniformBlock => UniformBlockSize > 0;

    internal WebGpuShaderModule Vertex => _vertex;
    internal WebGpuGraphicsDevice Device => _device;

    internal WebGpuPipelineState(
        WebGpuGraphicsDevice device,
        WebGpuShaderModule vertex,
        WebGpuShaderModule? pixel,
        PipelineDesc desc
    )
    {
        _device = device;
        _vertex = vertex;
        _pixel = pixel;
        Desc = desc;

        var end = 0;
        foreach (var uniform in Reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);
        UniformBlockSize = (end + 15) & ~15;

        if (Reflection.Textures.Count > ResourceSlots || Reflection.Samplers.Count > ResourceSlots)
        {
            throw new InvalidOperationException(
                $"This shader declares {Reflection.Textures.Count} texture(s) and "
                + $"{Reflection.Samplers.Count} sampler(s); the WebGPU backend lays out {ResourceSlots} "
                + $"of each. Raise {nameof(ResourceSlots)}."
            );
        }
    }

    /// <summary>
    /// The variant for the attachment it is about to be drawn into. WebGPU has no "pipeline without a
    /// fragment target", so a pass with no depth attachment needs a pipeline whose depth state is
    /// absent rather than merely disabled.
    /// </summary>
    internal WebGpuPipelineVariant GetVariant(in WebGpuPipelineKey key)
    {
        if (_lastKey == key && _lastVariant is not null)
            return _lastVariant;

        if (!_byKey.TryGetValue(key, out var variant))
        {
            variant = Build(key);
            _byKey[key] = variant;
            _variants.Add(variant);
        }

        _lastKey = key;
        _lastVariant = variant;
        return variant;
    }

    /// <summary>
    /// The vertex shader location a layout attribute feeds, or -1 when the shader does not declare
    /// it. A matrix input is one reflected entry spanning several locations, so a layout that lists
    /// it as four <c>TEXCOORD</c> registers maps each register onto its own location.
    /// </summary>
    internal int LocationOf(string semantic, int index)
    {
        foreach (var input in Reflection.VertexInputs)
        {
            if (!string.Equals(input.Semantic, semantic, StringComparison.OrdinalIgnoreCase))
                continue;

            if (index >= input.SemanticIndex && index < input.SemanticIndex + input.Registers)
                return input.Location + (index - input.SemanticIndex);
        }

        return -1;
    }

    private WebGpuPipelineVariant Build(in WebGpuPipelineKey key)
    {
        var layout = BuildBindGroupLayout(key);
        var pipeline = BuildPipeline(key, layout);
        return new WebGpuPipelineVariant(this, key, layout, pipeline);
    }

    private Wg.BindGroupLayout* BuildBindGroupLayout(in WebGpuPipelineKey key)
    {
        var visible = Wg.ShaderStage.Vertex | Wg.ShaderStage.Fragment;
        var entries = stackalloc Wg.BindGroupLayoutEntry[1 + ResourceSlots * 2];
        var count = 0;

        if (HasUniformBlock)
        {
            entries[count++] = new Wg.BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = visible,
                Buffer = new Wg.BufferBindingLayout
                {
                    Type = Wg.BufferBindingType.Uniform,
                    // One region of the per-frame uniform ring per draw, addressed by the dynamic
                    // offset the draw supplies - which is what lets the bind group itself be cached
                    // across draws that only changed their uniform values.
                    HasDynamicOffset = 1,
                    MinBindingSize = (ulong)UniformBlockSize,
                },
            };
        }

        // Fixed by the shader compiler's own assignment (ShaderBuild.BindingsFor): the block is 0,
        // textures start at 1 whether or not there is a block, then samplers.
        const int textureBase = 1;
        for (var i = 0; i < Reflection.Textures.Count; i++)
        {
            entries[count++] = new Wg.BindGroupLayoutEntry
            {
                Binding = (uint)(textureBase + i),
                Visibility = visible,
                Texture = new Wg.TextureBindingLayout
                {
                    SampleType = key.Unfilterable
                        ? Wg.TextureSampleType.UnfilterableFloat
                        : Wg.TextureSampleType.Float,
                    ViewDimension = Wg.TextureViewDimension.Dimension2D,
                    Multisampled = 0,
                },
            };
        }

        var samplerBase = textureBase + Reflection.Textures.Count;
        for (var i = 0; i < Reflection.Samplers.Count; i++)
        {
            entries[count++] = new Wg.BindGroupLayoutEntry
            {
                Binding = (uint)(samplerBase + i),
                Visibility = visible,
                Sampler = new Wg.SamplerBindingLayout
                {
                    Type = key.Unfilterable
                        ? Wg.SamplerBindingType.NonFiltering
                        : Wg.SamplerBindingType.Filtering,
                },
            };
        }

        var descriptor = new Wg.BindGroupLayoutDescriptor { EntryCount = (nuint)count, Entries = entries };
        var layout = _device.Api.DeviceCreateBindGroupLayout(_device.Device, &descriptor);
        _device.CheckCreated(layout is not null, "bind group layout");
        return layout;
    }

    private Wg.RenderPipeline* BuildPipeline(in WebGpuPipelineKey key, Wg.BindGroupLayout* bindGroupLayout)
    {
        var layouts = stackalloc Wg.BindGroupLayout*[1];
        layouts[0] = bindGroupLayout;

        var pipelineLayoutDescriptor = new Wg.PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = 1,
            BindGroupLayouts = layouts,
        };

        var pipelineLayout = _device.Api.DeviceCreatePipelineLayout(_device.Device, &pipelineLayoutDescriptor);
        _device.CheckCreated(pipelineLayout is not null, "pipeline layout");

        var buffers = stackalloc Wg.VertexBufferLayout[Desc.VertexLayouts.Count];
        var attributes = stackalloc Wg.VertexAttribute[32];
        var attributeIndex = 0;

        for (var slot = 0; slot < Desc.VertexLayouts.Count; slot++)
        {
            var layout = Desc.VertexLayouts[slot];
            var first = attributeIndex;

            foreach (var attribute in layout.Attributes)
            {
                var location = LocationOf(attribute.Semantic, attribute.Slot);
                if (location < 0)
                    continue;

                attributes[attributeIndex++] = new Wg.VertexAttribute
                {
                    Format = attribute.Format.ToVertexFormat(),
                    Offset = (ulong)attribute.OffsetInBytes,
                    ShaderLocation = (uint)location,
                };
            }

            buffers[slot] = new Wg.VertexBufferLayout
            {
                ArrayStride = (ulong)layout.StrideInBytes,
                StepMode = layout.InstanceStepRate > 0 ? Wg.VertexStepMode.Instance : Wg.VertexStepMode.Vertex,
                AttributeCount = (nuint)(attributeIndex - first),
                Attributes = attributes + first,
            };
        }

        using var vertexEntry = new WebGpuInterop.Utf8(_vertex.VertexEntry);
        var vertexState = new Wg.VertexState
        {
            Module = _vertex.VertexModule,
            EntryPoint = vertexEntry.View,
            BufferCount = (nuint)Desc.VertexLayouts.Count,
            Buffers = buffers,
        };

        var primitive = new Wg.PrimitiveState
        {
            Topology = Desc.Topology.ToTopology(),
            // A strip pipeline must declare its index format up front; the app's strips are 16-bit.
            StripIndexFormat = Desc.Topology is PrimitiveTopology.TriangleStrip or PrimitiveTopology.LineStrip
                ? Wg.IndexFormat.Uint16
                : Wg.IndexFormat.Undefined,
            FrontFace = Wg.FrontFace.Ccw,
            CullMode = Desc.RasterizerState.CullMode.ToCullMode(),
        };

        var multisample = new Wg.MultisampleState { Count = (uint)key.SampleCount, Mask = uint.MaxValue };

        var blend = Desc.BlendState.ToBlendState();
        var target = new Wg.ColorTargetState
        {
            Format = key.ColorFormat,
            Blend = Desc.BlendState.Enabled ? &blend : null,
            WriteMask = Desc.BlendState.ColorWriteMask.ToColorWriteMask(),
        };

        var pixelModule = _pixel ?? _vertex;
        using var pixelEntry = new WebGpuInterop.Utf8(pixelModule.PixelEntry);
        var fragment = new Wg.FragmentState
        {
            Module = pixelModule.PixelModule,
            EntryPoint = pixelEntry.View,
            TargetCount = 1,
            Targets = &target,
        };

        // Only when the pass has a depth attachment: a pipeline that names a format the pass does
        // not have is rejected, and a pass with depth does accept a pipeline that ignores it.
        var depthState = key.DepthFormat == Wg.TextureFormat.Undefined
            ? default
            : BuildDepthStencil(key.DepthFormat);

        var descriptor = new Wg.RenderPipelineDescriptor
        {
            Layout = pipelineLayout,
            Vertex = vertexState,
            Primitive = primitive,
            DepthStencil = key.DepthFormat == Wg.TextureFormat.Undefined ? null : &depthState,
            Multisample = multisample,
            Fragment = &fragment,
        };

        var pipeline = _device.Api.DeviceCreateRenderPipeline(_device.Device, &descriptor);
        _device.CheckCreated(pipeline is not null, $"render pipeline ({Desc.TechniqueName ?? "default"})");

        // The pipeline keeps its own reference to the layout it was created from.
        _device.Api.PipelineLayoutRelease(pipelineLayout);
        return pipeline;
    }

    private Wg.DepthStencilState BuildDepthStencil(Wg.TextureFormat format)
    {
        var state = Desc.DepthStencilState;
        var depthCompare = state.DepthTestEnabled ? state.DepthCompare.ToCompareFunction() : Wg.CompareFunction.Always;
        var stencilCompare = state.StencilTestEnabled
            ? state.StencilFunction.ToCompareFunction()
            : Wg.CompareFunction.Always;

        var front = new Wg.StencilFaceState
        {
            Compare = stencilCompare,
            FailOp = state.StencilFail.ToStencilOperation(),
            DepthFailOp = state.StencilDepthFail.ToStencilOperation(),
            PassOp = state.StencilPass.ToStencilOperation(),
        };

        var back = state.TwoSidedStencil
            ? new Wg.StencilFaceState
            {
                Compare = state.StencilTestEnabled
                    ? state.CcwStencilFunction.ToCompareFunction()
                    : Wg.CompareFunction.Always,
                FailOp = state.CcwStencilFail.ToStencilOperation(),
                DepthFailOp = state.CcwStencilDepthFail.ToStencilOperation(),
                PassOp = state.CcwStencilPass.ToStencilOperation(),
            }
            : front;

        return new Wg.DepthStencilState
        {
            Format = format,
            DepthWriteEnabled = state.DepthWriteEnabled ? Wg.OptionalBool.True : Wg.OptionalBool.False,
            DepthCompare = depthCompare,
            StencilFront = front,
            StencilBack = back,
            StencilReadMask = (uint)state.StencilReadMask,
            StencilWriteMask = (uint)state.StencilWriteMask,
        };
    }

    public void Dispose()
    {
        foreach (var variant in _variants)
            variant.Dispose();

        _variants.Clear();
        _byKey.Clear();
        _lastVariant = null;
        _lastKey = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// One concrete wgpu pipeline, its bind group layout, and the bind group most recently built against
/// it.
///
/// The cached bind group is the common case rather than an optimisation of the rare one: a mesh
/// binds its three textures once and then draws dozens of pieces with the same ones, and NanoVG
/// rebinds only when its atlas changes.
/// </summary>
internal sealed unsafe class WebGpuPipelineVariant : IDisposable
{
    private readonly WebGpuPipelineState _state;
    private readonly WebGpuGraphicsDevice _device;

    internal WebGpuPipelineKey Key { get; }
    internal Wg.BindGroupLayout* BindGroupLayout { get; }
    internal Wg.RenderPipeline* Pipeline { get; }

    private readonly nint[] _cachedTextures = new nint[WebGpuPipelineState.ResourceSlots];
    private readonly nint[] _cachedSamplers = new nint[WebGpuPipelineState.ResourceSlots];
    private int _cachedTextureCount = -1;
    private int _cachedSamplerCount;
    private int _cachedGeneration = -1;
    private Wg.BindGroup* _cachedBindGroup;

    internal WebGpuPipelineVariant(
        WebGpuPipelineState state,
        in WebGpuPipelineKey key,
        Wg.BindGroupLayout* bindGroupLayout,
        Wg.RenderPipeline* pipeline
    )
    {
        _state = state;
        _device = state.Device;
        Key = key;
        BindGroupLayout = bindGroupLayout;
        Pipeline = pipeline;
    }

    internal int UniformBlockSize => _state.UniformBlockSize;

    /// <summary>
    /// The bind group for the given resources, built once per distinct set. Everything the shader
    /// declares has to be present even when nothing was bound to it, so unset slots fall back to the
    /// device's dummy texture and sampler - which is what a shader that reads a slot the app never
    /// filled would otherwise bind as garbage.
    /// </summary>
    /// <param name="textures">The bound views, indexed by the pipeline's own texture slot.</param>
    /// <param name="samplers">The bound samplers, indexed the same way.</param>
    internal Wg.BindGroup* GetBindGroup(ReadOnlySpan<nint> textures, ReadOnlySpan<nint> samplers)
    {
        var generation = _device.Uniforms.Generation;
        if (_cachedBindGroup is not null
            && _cachedGeneration == generation
            && _cachedTextureCount == textures.Length
            && _cachedSamplerCount == samplers.Length
            && _cachedTextures.AsSpan(0, textures.Length).SequenceEqual(textures)
            && _cachedSamplers.AsSpan(0, samplers.Length).SequenceEqual(samplers))
        {
            return _cachedBindGroup;
        }

        var entries = stackalloc Wg.BindGroupEntry[1 + WebGpuPipelineState.ResourceSlots * 2];
        var count = 0;

        if (_state.HasUniformBlock)
        {
            entries[count++] = new Wg.BindGroupEntry
            {
                Binding = 0,
                Buffer = _device.Uniforms.Handle,
                // Offset 0 with the whole block as the size: the per-draw region is supplied as the
                // dynamic offset at bind time, which is what makes this group reusable.
                Offset = 0,
                Size = (ulong)_state.UniformBlockSize,
            };
        }

        const int textureBase = 1;
        for (var i = 0; i < _state.Reflection.Textures.Count; i++)
        {
            var view = i < textures.Length && textures[i] != 0 ? textures[i] : (nint)_device.DummyTexture.View;
            entries[count++] = new Wg.BindGroupEntry
            {
                Binding = (uint)(textureBase + i),
                TextureView = (Wg.TextureView*)view,
            };
        }

        var samplerBase = textureBase + _state.Reflection.Textures.Count;
        for (var i = 0; i < _state.Reflection.Samplers.Count; i++)
        {
            var sampler = i < samplers.Length && samplers[i] != 0 ? samplers[i] : (nint)_device.DummySampler.Handle;
            entries[count++] = new Wg.BindGroupEntry
            {
                Binding = (uint)(samplerBase + i),
                Sampler = (Wg.Sampler*)sampler,
            };
        }

        var descriptor = new Wg.BindGroupDescriptor
        {
            Layout = BindGroupLayout,
            EntryCount = (nuint)count,
            Entries = entries,
        };

        var group = _device.Api.DeviceCreateBindGroup(_device.Device, &descriptor);
        _device.CheckCreated(group is not null, "bind group");

        ReleaseCachedBindGroup();

        textures.CopyTo(_cachedTextures);
        samplers.CopyTo(_cachedSamplers);
        _cachedTextureCount = textures.Length;
        _cachedSamplerCount = samplers.Length;
        _cachedGeneration = generation;
        _cachedBindGroup = group;
        return group;
    }

    /// <summary>Drops the cached group, which names a uniform ring region that has since been replaced.</summary>
    internal void InvalidateBindGroup() => ReleaseCachedBindGroup();

    private void ReleaseCachedBindGroup()
    {
        if (_cachedBindGroup is null)
            return;

        // Deferred: a render pass stores the group's id and resolves it when the pass ends, so
        // releasing a group the open pass already recorded makes wgpu-core panic on a vacant id.
        var group = _cachedBindGroup;
        _cachedBindGroup = null;
        _device.DeferDestroy(() => _device.Api.BindGroupRelease(group));
        _cachedTextureCount = -1;
        _cachedGeneration = -1;
    }

    public void Dispose()
    {
        ReleaseCachedBindGroup();
        // The pass holds an id for the pipeline too, resolved at the same point as a bind group's.
        var pipeline = Pipeline;
        _device.DeferDestroy(() => _device.Api.RenderPipelineRelease(pipeline));
        _device.Api.BindGroupLayoutRelease(BindGroupLayout);
    }
}
