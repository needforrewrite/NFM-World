// LLM maintained.
//
// The command buffer: records one render pass, submits it, and presents.
//
// WebGPU has no "set the target and keep drawing" model. A render pass is a descriptor listing its
// attachments with load/store operations, and every draw recorded belongs to exactly one - so
// "which attachments am I drawing into" is a property of the pass, re-opened whenever the caller
// changes target. That also moves Clear into pass opening: WebGPU clears by loading, so the clear
// the caller asked for becomes part of whatever pass is opened next, colour and depth independent.
//
// Pipeline state is the other asymmetry. A pipeline names its colour format, depth format and sample
// count, so the pipeline a draw needs is chosen at the draw, from the attachment it is in - see
// WebGpuPipelineState.GetVariant.

using System.Numerics;
using System.Runtime.InteropServices;
using Wg = Silk.NET.WebGPU;

namespace NFMWorld.Graphics.WebGPU;

internal sealed unsafe class WebGpuCommandBuffer : ICommandBuffer
{
    private const int MaxVertexStreams = 4;

    private readonly WebGpuGraphicsDevice _device;
    private readonly WebGpuSwapchain _swapchain;

    private WebGpuPipelineState? _pipeline;
    private WebGpuPipelineVariant? _variant;
    private WebGpuPipelineKey _variantKey;

    /// <summary>The caller's render target, or null for the swapchain.</summary>
    private WebGpuRenderTarget? _target;

    /// <summary>
    /// The uniform block's bytes for the draw being assembled. WebGPU has no writable constant
    /// buffer, so the bytes are kept on the CPU and copied into a fresh region of the uniform ring
    /// immediately before each draw - which is why a draw is also the point the block is uploaded.
    /// </summary>
    private byte[] _uniforms = [];

    private readonly VertexStream[] _vertexStreams = new VertexStream[MaxVertexStreams];
    private WebGpuBuffer? _indexBuffer;
    private int _indexOffsetBytes;

    private readonly nint[] _textures = new nint[WebGpuPipelineState.ResourceSlots];
    private readonly nint[] _samplers = new nint[WebGpuPipelineState.ResourceSlots];

    private Viewport _viewport;
    private ScissorRect _scissor;
    private bool _hasViewport;
    private bool _hasScissor;

    /// <summary>An outstanding clear, applied when the next pass opens.</summary>
    private ClearOptions _pendingClear;
    private ColorRgba _clearColor;
    private float _clearDepth = 1f;
    private int _clearStencil;

    private Wg.CommandEncoder* _encoder;
    private Wg.RenderPassEncoder* _pass;

    internal WebGpuCommandBuffer(WebGpuGraphicsDevice device)
    {
        _device = device;
        _swapchain = device.SwapchainInternal;
        _viewport = new Viewport(0, 0, _swapchain.Width, _swapchain.Height);
        _scissor = new ScissorRect(0, 0, _swapchain.Width, _swapchain.Height);

        var descriptor = default(Wg.CommandEncoderDescriptor);
        _encoder = _device.Api.DeviceCreateCommandEncoder(_device.Device, &descriptor);
        _device.CheckCreated(_encoder is not null, "command encoder");
    }

    public void SetPipeline(IPipelineState pipeline)
    {
        var webGpu = (WebGpuPipelineState)pipeline;

        var size = webGpu.UniformBlockSize;
        if (_uniforms.Length != size)
            _uniforms = new byte[size];

        if (ReferenceEquals(_pipeline, webGpu))
            return;

        _pipeline = webGpu;
        _variant = null;
        Array.Clear(_textures);
        Array.Clear(_samplers);
    }

    /// <summary>
    /// Writes a uniform into the CPU copy of the block.
    ///
    /// A matrix4x4 is transposed here, exactly as the D3D11 backend does and for the same reason:
    /// the abstraction's callers hold row-major <see cref="System.Numerics.Matrix4x4"/> values, the
    /// SPIR-V (and therefore the WGSL tint emits from it) is column-major, and a value written
    /// straight through would be read as its own transpose - a translation landing in the fourth
    /// column where the multiply reads w instead.
    /// </summary>
    public void SetUniform(int slot, ReadOnlySpan<byte> value)
    {
        var pipeline = RequirePipeline();

        // A negative slot is a uniform the compiler dropped; the generated wrappers no-op on it.
        if (slot < 0)
            return;

        if (slot + value.Length > _uniforms.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                $"Uniform write of {value.Length} byte(s) at offset {slot} does not fit the "
                + $"{_uniforms.Length}-byte block this pipeline declares."
            );
        }

        if (IsMatrix4x4At(pipeline, slot))
        {
            var matrix = MemoryMarshal.Read<Matrix4x4>(value);
            var transposed = Matrix4x4.Transpose(matrix);
            MemoryMarshal.Write(_uniforms.AsSpan(slot), in transposed);
            return;
        }

        value.CopyTo(_uniforms.AsSpan(slot));
    }

    private static bool IsMatrix4x4At(WebGpuPipelineState pipeline, int offset)
    {
        foreach (var uniform in pipeline.Reflection.Uniforms)
        {
            if (uniform.Offset == offset)
                return uniform.Type == Shaders.UniformType.Matrix4x4;
        }

        return false;
    }

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0)
    {
        if ((uint)slot >= MaxVertexStreams)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                $"Vertex stream slot {slot} exceeds the {MaxVertexStreams} this backend tracks."
            );
        }

        _vertexStreams[slot] = new VertexStream((WebGpuBuffer)buffer, strideBytes, offsetBytes);
    }

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0)
    {
        _indexBuffer = (WebGpuBuffer)buffer;
        _indexOffsetBytes = offsetBytes;
    }

    /// <summary>
    /// Records a texture and its sampler against a slot. Nothing reaches the GPU here: a bind group
    /// is built once per distinct resource set at draw time, because WebGPU has no per-slot bind.
    /// </summary>
    public void SetShaderResource(int slot, ITexture texture, ISampler sampler)
    {
        RequirePipeline();

        if ((uint)slot >= WebGpuPipelineState.ResourceSlots)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                $"Texture slot {slot} exceeds the {WebGpuPipelineState.ResourceSlots} this backend binds "
                + "per draw. The shader's bind point is what the slot is, so nothing else needs changing."
            );
        }

        // A multisampled texture has no sampleable view - the abstraction can hand the swapchain's
        // own colour buffer here, which is unresolvable. Binding the dummy keeps the group complete.
        _textures[slot] = (nint)((WebGpuTexture)texture).View;
        _samplers[slot] = sampler is WebGpuSampler s ? (nint)s.Handle : 0;
    }

    public void SetRenderTarget(IRenderTarget? target)
    {
        var webGpu = (WebGpuRenderTarget?)target;
        if (ReferenceEquals(_target, webGpu))
            return;

        // A clear belongs to the attachments it was issued against, so it has to be consumed before
        // they change - otherwise it would land on the next target's pass, clearing a cascade that
        // was never asked to be cleared. It is also the only point an empty scene gets its clear at
        // all: with no draw to open a pass, the pending clear would otherwise be dropped.
        FlushPendingClear();

        // The pass has to close: its attachment list is fixed at open. Whatever it recorded is
        // already ordered in the encoder, so the next draw opens a pass on the new attachments.
        EndPass();
        _target = webGpu;
    }

    public void SetViewport(Viewport viewport)
    {
        _viewport = viewport;
        _hasViewport = true;
        if (_pass is not null)
            ApplyViewportAndScissor();
    }

    public void SetScissorRect(ScissorRect rect)
    {
        _scissor = rect;
        _hasScissor = true;
        ApplyScissor();
    }

    public void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        if (options == ClearOptions.None)
            return;

        // WebGPU expresses a clear as the load operation of a pass, so it is remembered and folded
        // into the next pass opened against these attachments. A second clear before any draw
        // replaces the first rather than queueing two, which is what the other backends' immediate
        // clears would have collapsed to anyway.
        _pendingClear |= options;
        if (options.HasFlag(ClearOptions.Color))
            _clearColor = color;

        if (options.HasFlag(ClearOptions.Depth))
            _clearDepth = depth;

        if (options.HasFlag(ClearOptions.Stencil))
            _clearStencil = stencil;
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0)
    {
        if (data.IsEmpty)
            return;

        // Through the staging ring rather than a queue write: this lands mid-frame and has to stay
        // ordered against the draws recorded before and after it, which a queue write does not
        // guarantee - it is ordered against submissions, not against recorded commands.
        //
        // The copy length is rounded up to WebGPU's 4-byte copy unit; WebGpuBuffer.GpuSize leaves
        // room for that tail, and Staging.Write pads the staged bytes to the same length.
        var staging = _device.Staging;
        var offset = staging.Allocate(data.Length);
        staging.Write(offset, data);

        var destination = (WebGpuBuffer)buffer;
        EndPass();

        _device.Api.CommandEncoderCopyBufferToBuffer(
            _encoder,
            staging.Handle,
            offset,
            destination.Handle,
            (ulong)offsetBytes,
            (ulong)WebGpuInterop.Align(data.Length, 4)
        );
    }

    public void UpdateTexture(ITexture texture, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        var webGpu = (WebGpuTexture)texture;
        var rowBytes = webGpu.Format.RowBytes(width);
        var rows = webGpu.Format.RowCount(height);
        var required = rowBytes * rows;

        if (data.Length < required)
        {
            throw new ArgumentException(
                $"A {width}x{height} {webGpu.Format} update needs {required} byte(s); {data.Length} were given.",
                nameof(data)
            );
        }

        // WebGPU requires every row of a multi-row copy to start on a 256-byte boundary, so a
        // tightly packed source is repacked into the staging region at that pitch.
        var pitch = WebGpuInterop.Align(rowBytes, 256);
        var size = pitch * rows;

        var staging = _device.Staging;
        var offset = staging.Allocate(size);
        if (pitch == rowBytes)
        {
            staging.Write(offset, data[..required]);
        }
        else
        {
            var padded = new byte[size];
            for (var row = 0; row < rows; row++)
                data.Slice(row * rowBytes, rowBytes).CopyTo(padded.AsSpan(row * pitch, rowBytes));
            staging.Write(offset, padded);
        }

        var destination = new Wg.ImageCopyTexture
        {
            Texture = webGpu.Handle,
            MipLevel = 0,
            Origin = new Wg.Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
            Aspect = Wg.TextureAspect.All,
        };

        var layout = new Wg.TextureDataLayout
        {
            Offset = offset,
            BytesPerRow = (uint)pitch,
            RowsPerImage = (uint)rows,
        };

        var source = new Wg.ImageCopyBuffer { Buffer = staging.Handle, Layout = layout };
        var extent = new Wg.Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
        EndPass();
        _device.Api.CommandEncoderCopyBufferToTexture(_encoder, &source, &destination, &extent);
    }

    public void Draw(int startVertex, int primitiveCount) =>
        DrawInternal(baseVertex: 0, firstIndex: startVertex, primitiveCount, instanceCount: 1, indexed: false);

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount) =>
        DrawInternal(baseVertex, firstIndex: startIndex, primitiveCount, instanceCount: 1, indexed: true);

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount) =>
        DrawInternal(baseVertex, firstIndex: startIndex, primitiveCount, instanceCount, indexed: true);

    private void DrawInternal(int baseVertex, int firstIndex, int primitiveCount, int instanceCount, bool indexed)
    {
        var pipeline = RequirePipeline();
        EnsurePass();

        var variant = _variant;
        if (variant is null)
        {
            variant = pipeline.GetVariant(_variantKey);
            _variant = variant;
            _device.Api.RenderPassEncoderSetPipeline(_pass, variant.Pipeline);
        }

        // The uniform region for this draw. Every draw gets its own: a later draw that writes
        // different values must not change what an earlier one reads.
        if (pipeline.HasUniformBlock)
        {
            var offset = _device.Uniforms.Allocate(_uniforms.Length);
            _device.Uniforms.Write(offset, _uniforms);

            var bindGroup = variant.GetBindGroup(_textures, _samplers);
            var dynamicOffset = (uint)offset;
            _device.Api.RenderPassEncoderSetBindGroup(_pass, 0, bindGroup, 1, &dynamicOffset);
        }
        else
        {
            var bindGroup = variant.GetBindGroup(_textures, _samplers);
            _device.Api.RenderPassEncoderSetBindGroup(_pass, 0, bindGroup, 0, null);
        }

        BindVertexStreams(pipeline);

        var count = ElementCount(pipeline.Desc.Topology, primitiveCount);

        // (vertexCount, instanceCount, firstVertex, firstInstance): the non-instanced form is one
        // instance starting at the caller's first vertex, and the first vertex is not the instance
        // count - passing it there draws nothing whenever a draw starts at vertex 0.
        if (!indexed)
        {
            _device.Api.RenderPassEncoderDraw(_pass, (uint)count, (uint)instanceCount, (uint)firstIndex, 0);
            return;
        }

        var indexBuffer = _indexBuffer
                          ?? throw new InvalidOperationException(
                              $"DrawIndexed requires {nameof(SetIndexBuffer)} to have been called."
                          );

        // The format is a property of the index buffer rather than the pipeline (only a strip
        // pipeline bakes one, and every strip in this tree is 16-bit).
        var indexOffset = _indexOffsetBytes + firstIndex * indexBuffer.IndexSize;
        _device.Api.RenderPassEncoderSetIndexBuffer(
            _pass,
            indexBuffer.Handle,
            indexBuffer.IndexFormat == IndexFormat.UInt32 ? Wg.IndexFormat.Uint32 : Wg.IndexFormat.Uint16,
            (ulong)indexOffset,
            (ulong)(indexBuffer.SizeInBytes - indexOffset)
        );

        _device.Api.RenderPassEncoderDrawIndexed(_pass, (uint)count, (uint)instanceCount, 0, baseVertex, 0);
    }

    private void BindVertexStreams(WebGpuPipelineState pipeline)
    {
        var slotCount = Math.Max(1, pipeline.Desc.VertexLayouts.Count);
        for (var slot = 0; slot < slotCount; slot++)
        {
            var stream = _vertexStreams[slot];
            var buffer = stream.Buffer
                         ?? throw new InvalidOperationException(
                             $"The pipeline declares vertex layout slot {slot} but {nameof(SetVertexBuffer)} "
                             + "was never called for it."
                         );

            // Whole-buffer binding with the caller's byte offset: WebGPU has no stride here (the
            // stride lives in the pipeline's layout), so the three arguments are buffer, offset and
            // size, and the size is what remains of the buffer past the offset.
            _device.Api.RenderPassEncoderSetVertexBuffer(
                _pass,
                (uint)slot,
                buffer.Handle,
                (ulong)stream.OffsetBytes,
                (ulong)(buffer.SizeInBytes - stream.OffsetBytes)
            );
        }
    }

    /// <summary>
    /// Opens a pass on the current target if none is open, computing the pipeline variant key from
    /// the attachments it lands on.
    /// </summary>
    private void EnsurePass()
    {
        if (_pass is not null)
            return;

        var colorFormat = Wg.TextureFormat.Undefined;
        var depthFormat = Wg.TextureFormat.Undefined;
        var sampleCount = 1;
        Wg.TextureView* colorView;
        Wg.TextureView* resolveView = null;
        Wg.TextureView* depthView;

        if (_target is null)
        {
            _device.SwapchainInternal.BeginFrame();
            colorView = _swapchain.ColorView;
            resolveView = _swapchain.ResolveView;
            depthView = _swapchain.DepthView;
            colorFormat = _swapchain.ColorFormat;
            sampleCount = _swapchain.MultiSampleCount;
            if (depthView is not null)
                depthFormat = _swapchain.DepthFormat;
        }
        else
        {
            colorView = _target.Color.View;
            depthView = _target.Depth?.View;
            colorFormat = _target.Color.GpuFormat;
            if (depthView is not null)
                depthFormat = _target.Depth!.GpuFormat;
        }

        if (colorView is null)
        {
            throw new InvalidOperationException(
                "No render target is bound and the swapchain has no back buffer to fall back to."
            );
        }

        var wantsColorClear = _pendingClear.HasFlag(ClearOptions.Color);
        var wantsDepth = depthView is not null;
        var wantsDepthClear = wantsDepth && _pendingClear.HasFlag(ClearOptions.Depth);
        var wantsStencilClear = wantsDepth && _pendingClear.HasFlag(ClearOptions.Stencil);

        var colorAttachment = new Wg.RenderPassColorAttachment
        {
            View = colorView,
            ResolveTarget = resolveView,
            LoadOp = wantsColorClear ? Wg.LoadOp.Clear : Wg.LoadOp.Load,
            StoreOp = Wg.StoreOp.Store,
            ClearValue = new Wg.Color
            {
                R = _clearColor.R,
                G = _clearColor.G,
                B = _clearColor.B,
                A = _clearColor.A,
            },
        };

        var depthAttachment = default(Wg.RenderPassDepthStencilAttachment);
        if (wantsDepth)
        {
            depthAttachment = new Wg.RenderPassDepthStencilAttachment
            {
                View = depthView,
                DepthLoadOp = wantsDepthClear ? Wg.LoadOp.Clear : Wg.LoadOp.Load,
                DepthStoreOp = Wg.StoreOp.Store,
                DepthClearValue = _clearDepth,
                StencilLoadOp = wantsStencilClear ? Wg.LoadOp.Clear : Wg.LoadOp.Load,
                StencilStoreOp = Wg.StoreOp.Store,
                StencilClearValue = (uint)_clearStencil,
            };
        }

        // wgpu-native caps how many draws one pass may record, and the cap is a chained struct
        // rather than a descriptor field. A pass here holds a whole scene's draws, so the bound is
        // generous rather than counted - the draw count is not known before recording them.
        var maxDrawCount = new Wg.RenderPassDescriptorMaxDrawCount
        {
            Chain = new Wg.ChainedStruct { SType = Wg.SType.RenderPassDescriptorMaxDrawCount },
            MaxDrawCount = 1_000_000,
        };

        var descriptor = new Wg.RenderPassDescriptor
        {
            NextInChain = (Wg.ChainedStruct*)&maxDrawCount,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorAttachment,
            DepthStencilAttachment = wantsDepth ? &depthAttachment : null,
        };

        _pass = _device.Api.CommandEncoderBeginRenderPass(_encoder, &descriptor);
        _device.CheckCreated(_pass is not null, "render pass encoder");

        _pendingClear = ClearOptions.None;
        _variant = null;

        _variantKey = new WebGpuPipelineKey(
            colorFormat,
            depthFormat,
            sampleCount,
            Unfilterable: !_device.Float32Filterable
        );

        // A pass starts with the whole attachment as its viewport and scissor, so whatever the
        // caller asked for before it opened has to be re-applied - the same reason D3D11 re-applies
        // both when a target binds.
        ApplyViewportAndScissor();
    }

    /// <summary>
    /// Applies an outstanding clear by opening and immediately closing a pass on the current target.
    /// WebGPU clears by loading, so a clear with no draws after it has no pass to belong to; calling
    /// this at every boundary (target change, submit) keeps the other backends' immediate semantics.
    /// </summary>
    private void FlushPendingClear()
    {
        if (_pendingClear == ClearOptions.None)
            return;

        EnsurePass();
        EndPass();
    }

    private void EndPass()
    {
        if (_pass is null)
            return;

        _device.Api.RenderPassEncoderEnd(_pass);
        _device.Api.RenderPassEncoderRelease(_pass);
        _pass = null;
        _variant = null;
    }

    /// <summary>
    /// Re-applies the viewport and scissor to the open pass, falling back to the whole attachment
    /// when the caller set neither - which is the state a pass opens in and what a caller that never
    /// calls either expects.
    /// </summary>
    private void ApplyViewportAndScissor()
    {
        if (_pass is null)
            return;

        var width = _target?.Color.Width ?? _swapchain.Width;
        var height = _target?.Color.Height ?? _swapchain.Height;

        var viewport = _hasViewport ? _viewport : new Viewport(0, 0, width, height);

        // WebGPU's framebuffer origin is the top left, so no flip; the depth range is the
        // abstraction's [0, 1].
        _device.Api.RenderPassEncoderSetViewport(
            _pass,
            viewport.X,
            viewport.Y,
            viewport.Width,
            viewport.Height,
            viewport.MinDepth,
            viewport.MaxDepth
        );

        ApplyScissor();
    }

    private void ApplyScissor()
    {
        if (_pass is null)
            return;

        var width = _target?.Color.Width ?? _swapchain.Width;
        var height = _target?.Color.Height ?? _swapchain.Height;
        var scissor = _hasScissor ? _scissor : new ScissorRect(0, 0, width, height);

        _device.Api.RenderPassEncoderSetScissorRect(
            _pass,
            (uint)Math.Clamp(scissor.X, 0, width),
            (uint)Math.Clamp(scissor.Y, 0, height),
            (uint)Math.Clamp(scissor.Width, 0, width),
            (uint)Math.Clamp(scissor.Height, 0, height)
        );
    }

    private static int ElementCount(PrimitiveTopology topology, int primitiveCount) => topology switch
    {
        PrimitiveTopology.TriangleList => primitiveCount * 3,
        PrimitiveTopology.LineList => primitiveCount * 2,
        PrimitiveTopology.PointList => primitiveCount,
        PrimitiveTopology.TriangleStrip => primitiveCount + 2,
        PrimitiveTopology.LineStrip => primitiveCount + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    private WebGpuPipelineState RequirePipeline() =>
        _pipeline ?? throw new InvalidOperationException($"{nameof(SetPipeline)} must be called before issuing draw work.");

    /// <summary>
    /// Finishes the encoder and submits, then presents. Called once by the device.
    /// </summary>
    internal void Submit()
    {
        FlushPendingClear();
        EndPass();

        var descriptor = default(Wg.CommandBufferDescriptor);
        var commands = _device.Api.CommandEncoderFinish(_encoder, &descriptor);
        _device.Api.CommandEncoderRelease(_encoder);
        _encoder = null;

        if (commands is not null)
        {
            _device.Api.QueueSubmit(_device.QueueInternal, 1, &commands);
            _device.Api.CommandBufferRelease(commands);
        }

        // Present is the host's: WorldGame.Submit(cb) then Swapchain.Present(), as on every other
        // backend. Presenting here too acquires the surface twice and wgpu aborts the process.

        // The whole frame is done, so the ring's regions are no longer needed by anything recorded.
        _device.Uniforms.Reset();
        _device.Staging.Reset();
    }

    internal void Dispose()
    {
        EndPass();

        if (_encoder is not null)
        {
            _device.Api.CommandEncoderRelease(_encoder);
            _encoder = null;
        }

        if (_variant is not null)
        {
            _variant.InvalidateBindGroup();
            _variant = null;
        }
    }

    private readonly record struct VertexStream(WebGpuBuffer Buffer, int StrideBytes, int OffsetBytes);
}
