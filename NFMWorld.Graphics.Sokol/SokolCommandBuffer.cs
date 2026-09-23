using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// Records draw work against sokol_gfx. Like the FNA3D backend this executes immediately - sokol
/// is an immediate-mode API with no command-list object to replay - but the shape of the calls is
/// very different, and that difference is what this class exists to absorb.
///
/// sokol has three constraints the abstraction's call order does not respect:
/// <list type="number">
///   <item><c>sg_apply_pipeline</c>, <c>sg_apply_bindings</c>, <c>sg_apply_uniforms</c>,
///   <c>sg_apply_viewport</c> and <c>sg_apply_scissor_rect</c> all return early unless a pass is
///   already open (see <c>sg_apply_pipeline</c>: <c>if (!_sg.cur_pass.valid) return;</c>).</item>
///   <item>They must be issued in that order relative to each other - the pipeline's reflection is
///   what uniform and binding validation is checked against.</item>
///   <item>Bindings are a single struct applied all at once, not one resource at a time.</item>
/// </list>
///
/// So this class records everything into fields and defers every native call to
/// <see cref="Flush"/>, which runs immediately before a draw. That makes the whole class a small
/// state machine with one commit point, and it is why the abstraction's per-call ordering (setting
/// a render target, then a pipeline, then a texture, then a uniform) does not have to match
/// sokol's.
/// </summary>
internal sealed class SokolCommandBuffer(SokolGraphicsDevice device) : ICommandBuffer
{
    private SokolPipelineState? _pipeline;
    private SokolRenderTarget? _target;

    /// <summary>
    /// Resources written since the last flush. Uploads are deferred to <see cref="Flush"/> because
    /// sokol allows at most one update per resource per frame, and because a write must not be
    /// attempted at all on a buffer or image the shadowing passes have already bound.
    /// </summary>
    private readonly List<IPendingUpload> _pendingUploads = [];

    // Pending binding set. Sized to sokol's fixed limits, which the sg_bindings struct encodes
    // (8 vertex buffers, 32 views, 12 samplers).
    private readonly sg_buffer[] _vertexBuffers = new sg_buffer[8];
    private readonly int[] _vertexBufferOffsets = new int[8];
    private sg_buffer _indexBuffer;
    private int _indexBufferOffset;
    private readonly sg_view[] _views = new sg_view[32];
    private readonly sg_sampler[] _samplers = new sg_sampler[12];

    private sg_pass_action _passAction;
    private bool _inPass;
    private bool _pipelineDirty;

    private Viewport? _viewport;
    private ScissorRect? _scissor;

    /// <summary>
    /// The uniform block's contents, accumulated per draw.
    ///
    /// This is the sharpest divergence from the FNA3D backend, which writes each uniform straight
    /// into the Effect's parameter storage at a flat index. sokol has no per-uniform entry point:
    /// <c>sg_apply_uniforms</c> takes a uniform-<em>block</em> slot and asserts
    /// <c>data-&gt;size == shd-&gt;cmn.uniform_blocks[ub_slot].size</c> (sokol_gfx.h:25365), so a
    /// caller's individual writes have to be accumulated into a block-sized buffer and uploaded as
    /// one range at draw time.
    ///
    /// The slot callers pass is a byte offset into that block - it comes from
    /// <c>UniformParam.Offset</c> in the shader compiler's emitted reflection, which is the merged
    /// <c>_Global</c> cbuffer's <c>packoffset</c> layout - so a write lands exactly where the
    /// compiled shader reads it.
    /// </summary>
    private byte[] _uniformBlock = [];
    private int _uniformBlockSize;

    public void SetPipeline(IPipelineState pipeline)
    {
        var sokolPipeline = (SokolPipelineState)pipeline;
        if (ReferenceEquals(_pipeline, sokolPipeline)) return;

        _pipeline = sokolPipeline;
        _pipelineDirty = true;

        // The block's layout is a property of the pipeline's shader, so changing pipeline
        // invalidates anything accumulated for the previous one.
        _uniformBlock = new byte[sokolPipeline.UniformBlockSize];
        Array.Clear(_uniformBlock);
        _uniformBlockSize = sokolPipeline.UniformBlockSize;

        // Likewise for the binding tables: the sokol slots they name are this pipeline's, and a
        // view left over from the previous one would otherwise satisfy a slot the caller never
        // set (the previous pipeline may have declared many more views than this one).
        Array.Clear(_views);
        Array.Clear(_samplers);
    }

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0)
    {
        // The stride is deliberately ignored: sokol reads it from the pipeline's vertex layout,
        // not from the binding, and the two are validated against each other by sokol itself.
        _vertexBuffers[slot] = ((SokolBuffer)buffer).Handle;
        _vertexBufferOffsets[slot] = offsetBytes;
    }

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0)
    {
        _indexBuffer = ((SokolBuffer)buffer).Handle;
        _indexBufferOffset = offsetBytes;
    }

    public void SetShaderResource(int slot, ITexture texture, ISampler sampler)
    {
        var view = ((SokolTexture)texture).View;

        // sokol keeps views and samplers in separate namespaces, unlike FNA3D where
        // FNA3D_VerifySampler binds a texture and its state together. The abstraction still hands
        // over one slot, which maps onto one sokol view slot per stage and one sampler slot per
        // stage - see ShaderBindings for why every binding is declared in both stages.
        var viewSlots = _pipeline?.ViewSlotMap[slot];
        if (viewSlots is not null)
            foreach (var sokolSlot in viewSlots) _views[sokolSlot] = view;

        if (sampler is not SokolSampler sokolSampler) return;
        var samplerSlots = _pipeline?.SamplerSlotMap[slot];
        if (samplerSlots is not null)
            foreach (var sokolSlot in samplerSlots) _samplers[sokolSlot] = sokolSampler.Handle;
    }

    public void SetUniform(int slot, ReadOnlySpan<byte> value)
    {
        if (_pipeline is null)
            throw new InvalidOperationException($"{nameof(SetUniform)} requires a pipeline to be bound first.");

        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "A negative slot means the shader compiler optimized the uniform out; callers must skip it.");

        if (slot + value.Length > _uniformBlockSize)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"{value.Length} bytes at offset {slot} overruns the {_uniformBlockSize}-byte uniform block.");

        value.CopyTo(_uniformBlock.AsSpan(slot));
    }

    public void SetRenderTarget(IRenderTarget? target)
    {
        // sokol has no "retarget" call - a pass is begun and ended explicitly, so switching target
        // closes whatever pass was open. The next draw opens a new one.
        EndPass();
        _target = (SokolRenderTarget?)target;

        // The clear action is consumed by the next sg_begin_pass. Default (all-zero) is
        // SG_LOADACTION_CLEAR with a black/1.0/0 clear, which is the defined-surface default a
        // caller who never calls Clear should get.
        _passAction = new sg_pass_action
        {
            depth = new sg_depth_attachment_action { clear_value = 1f },
        };
    }

    public void SetViewport(Viewport viewport) => _viewport = viewport;

    public void SetScissorRect(ScissorRect rect) => _scissor = rect;

    public unsafe void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        // sokol expresses clears as load actions on the pass rather than as a draw-time call, so
        // a Clear only has any effect if it is recorded before the pass it applies to begins.
        if (options.HasFlag(ClearOptions.Color))
        {
            ref var action = ref _passAction.colors[0];
            action.load_action = sg_load_action.SG_LOADACTION_CLEAR;
            action.store_action = sg_store_action.SG_STOREACTION_STORE;
            action.clear_value = new sg_color { r = color.R, g = color.G, b = color.B, a = color.A };
        }
        if (options.HasFlag(ClearOptions.Depth))
        {
            _passAction.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
            _passAction.depth.clear_value = depth;
        }
        if (options.HasFlag(ClearOptions.Stencil))
        {
            _passAction.stencil.load_action = sg_load_action.SG_LOADACTION_CLEAR;
            _passAction.stencil.clear_value = (byte)stencil;
        }

        // If draws have already opened the pass, the action has been read; reopening it is the
        // only way to apply the new clear. That discards those draws, which is what "clear the
        // target" means - the abstraction's Clear is expected before the draws it precedes.
        if (_inPass) { EndPass(); BeginPass(); }
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0)
    {
        var sokol = (SokolBuffer)buffer;

        if (sokol.Usage == BufferUsage.Immutable)
            throw new NotSupportedException(
                "An immutable buffer cannot be updated after creation - sokol's sg_update_buffer " +
                "rejects one, and its contents were taken from the descriptor at creation time.");

        if (offsetBytes < 0 || offsetBytes + data.Length > sokol.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(data),
                $"{data.Length} bytes at offset {offsetBytes} overruns the {sokol.SizeInBytes}-byte buffer.");

        // sg_update_buffer replaces a whole buffer and there is no offset argument (nor any
        // readback to preserve the rest with), so every write lands in the CPU mirror and the
        // single upload happens once, at draw time. That is what lets a caller update the same
        // buffer twice in a frame - sokol allows one sg_update_buffer per buffer per frame.
        sokol.Mirror ??= new byte[sokol.SizeInBytes];
        data.CopyTo(sokol.Mirror.AsSpan(offsetBytes));
        sokol.MarkDirty();
        Track(sokol);
    }

    public void UpdateTexture(ITexture texture, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        var sokol = (SokolTexture)texture;
        var bytesPerPixel = sokol.BytesPerPixel;
        if (bytesPerPixel == 0)
            throw new ArgumentOutOfRangeException(nameof(texture), sokol.Format, "Block-compressed textures cannot be updated by sub-rectangle.");

        if (!sokol.Updatable)
            throw new NotSupportedException(
                "This texture was created immutable, so it cannot be updated. A texture is only updatable " +
                "when it is single-level (MipMapped: false) and its format has a CPU mirror - see CreateTexture.");

        var rowBytes = width * bytesPerPixel;
        if (data.Length < rowBytes * height)
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, $"Expected at least {rowBytes * height} bytes for a {width}x{height} update.");

        // sg_update_image replaces a whole level, not a sub-rectangle, and sokol has no readback to
        // reconstruct the rest with - so the write lands in the mirror, which already holds every
        // byte outside the updated region. That is what makes a sub-rectangle update
        // non-destructive; a fresh staging buffer would silently zero the untouched pixels.
        var shadow = sokol.CpuShadow;
        if (shadow is null || shadow.Length != sokol.Width * sokol.Height * bytesPerPixel)
            throw new InvalidOperationException("An updatable texture must have a mirror sized to its surface; see CreateTexture.");

        for (var row = 0; row < height; row++)
            data.Slice(row * rowBytes, rowBytes).CopyTo(shadow.AsSpan(((y + row) * sokol.Width + x) * bytesPerPixel));

        sokol.MarkDirty();
        Track(sokol);
    }

    /// <summary>
    /// Remembers <paramref name="resource"/> so its mirror reaches the GPU before the next draw.
    /// Adding it twice is harmless: the resource is only flushed once per frame, and only while it
    /// still has pending changes.
    /// </summary>
    private void Track(IPendingUpload resource)
    {
        if (!_pendingUploads.Contains(resource)) _pendingUploads.Add(resource);
    }

    private void FlushUploads()
    {
        if (_pendingUploads.Count == 0) return;

        var frame = device.FrameIndex;
        foreach (var resource in _pendingUploads)
            if (resource.UploadPending) resource.Upload(frame);
        _pendingUploads.Clear();
    }

    public void Draw(int startVertex, int primitiveCount)
    {
        var pipeline = RequirePipeline();
        Flush();
        Gfx.draw(startVertex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: false), 1);
    }

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount)
    {
        var pipeline = RequirePipeline();
        Flush();
        Gfx.draw_ex(startIndex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: true), 1, baseVertex, 0);
    }

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount)
    {
        var pipeline = RequirePipeline();
        Flush();
        Gfx.draw_ex(startIndex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: true), instanceCount, baseVertex, 0);
    }

    private SokolPipelineState RequirePipeline() =>
        _pipeline ?? throw new InvalidOperationException($"{nameof(SetPipeline)} must be called before issuing a draw call.");

    /// <summary>
    /// Converts the abstraction's primitive count (matching FNA3D's <c>DrawPrimitives</c>) into
    /// sokol's element count. A strip consumes one extra element for its first primitive
    /// (<c>n</c> triangles need <c>n+2</c> vertices), so it cannot use the list multiplier.
    /// </summary>
    private static int ElementCount(PrimitiveTopology topology, int primitiveCount, bool indexed) => topology switch
    {
        PrimitiveTopology.TriangleList => primitiveCount * 3,
        PrimitiveTopology.LineList => primitiveCount * 2,
        PrimitiveTopology.PointList => primitiveCount,
        // A strip's n primitives always consume n+2 elements, indexed or not - only the
        // list topologies scale by their primitive size.
        PrimitiveTopology.TriangleStrip => primitiveCount + 2,
        PrimitiveTopology.LineStrip => primitiveCount + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    /// <summary>
    /// Issues every deferred native call, in sokol's required order. This is the one place a pass
    /// is opened and the pipeline/bindings/uniforms are applied, so the invariants sokol validates
    /// (pass open, pipeline applied, uniforms sized to the block, all declared views bound) hold
    /// by construction rather than by the caller's call order.
    /// </summary>
    private unsafe void Flush()
    {
        var pipeline = RequirePipeline();

        // Uploads go before BeginPass: sg_update_buffer/sg_update_image are not pass-recording
        // calls, and sokol asserts they happen outside a pass. Both are also once-per-resource-per-
        // frame, which FlushUploads collapses by flushing each resource at most once.
        FlushUploads();
        BeginPass();

        if (_pipelineDirty)
        {
            Gfx.apply_pipeline(pipeline.Handle);
            _pipelineDirty = false;
        }

        // Viewport and scissor default to the whole drawable, matching FNA3D's device defaults,
        // but they must be (re)applied whenever a new pass opens or a new pipeline binds.
        var viewport = _viewport ?? new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height);
        Gfx.apply_viewportf(viewport.X, viewport.Y, viewport.Width, viewport.Height, SokolNative.Bool(true));

        if (_scissor is { } scissor)
            Gfx.apply_scissor_rect(scissor.X, scissor.Y, scissor.Width, scissor.Height, SokolNative.Bool(true));

        // Uniforms first: sg_apply_uniforms asserts the data matches the applied pipeline's block.
        // A program keeps its merged _Global cbuffer at register(b0) in BOTH stages, so the same
        // accumulated bytes go to each stage's block slot.
        if (_uniformBlockSize > 0)
        {
            foreach (var slot in pipeline.UniformBlockSlots)
            {
                fixed (byte* pointer = _uniformBlock)
                {
                    var range = SokolNative.Range(pointer, _uniformBlockSize);
                    Gfx.apply_uniforms(slot, &range);
                }
            }
        }

        ApplyBindings();
    }

    /// <summary>
    /// Starts a pass if one is open. sokol requires a draw to happen inside a pass, and a pass to
    /// name either the swapchain or explicit attachment views - which is why the target is read
    /// here rather than at <see cref="SetRenderTarget"/>.
    /// </summary>
    private unsafe void BeginPass()
    {
        if (_inPass) return;

        var pass = new sg_pass { action = _passAction };

        if (_target is null)
        {
            // A swapchain pass: sokol acquires the current backbuffer itself. The swapchain is only
            // valid between frames, so it is re-read per pass rather than cached.
            pass.swapchain = Glue.swapchain();
        }
        else
        {
            pass.attachments.colors[0] = _target.ColorAttachmentView;
            pass.attachments.depth_stencil = _target.DepthStencilAttachmentView;
        }

        Gfx.begin_pass(&pass);
        _inPass = true;

        // A fresh pass resets the applied pipeline, so the next draw must re-issue it.
        _pipelineDirty = true;
    }

    private void EndPass()
    {
        if (!_inPass) return;
        Gfx.end_pass();
        _inPass = false;
    }

    private unsafe void ApplyBindings()
    {
        var bindings = new sg_bindings();
        for (var i = 0; i < _vertexBuffers.Length; i++)
        {
            bindings.vertex_buffers[i] = _vertexBuffers[i];
            bindings.vertex_buffer_offsets[i] = _vertexBufferOffsets[i];
        }
        bindings.index_buffer = _indexBuffer;
        bindings.index_buffer_offset = _indexBufferOffset;
        for (var i = 0; i < _views.Length; i++) bindings.views[i] = _views[i];
        for (var i = 0; i < _samplers.Length; i++) bindings.samplers[i] = _samplers[i];

        Gfx.apply_bindings(&bindings);
    }

    /// <summary>
    /// Closes the frame: ends any open pass and commits. Called by
    /// <see cref="SokolGraphicsDevice.Submit"/> rather than by the individual command calls,
    /// because sokol presents as part of <c>sg_commit</c> and Submit is the frame boundary.
    /// </summary>
    internal void Commit()
    {
        EndPass();
        Gfx.commit();
    }
}
