using NFMWorld.Graphics;
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

    /// <summary>
    /// The bound index buffer's format, or <c>NONE</c> when no index buffer is bound - which is the
    /// non-indexed case, and the value the pipeline variant must declare to match.
    /// </summary>
    private sg_index_type _indexFormat = sg_index_type.SG_INDEXTYPE_NONE;
    private readonly sg_view[] _views = new sg_view[32];
    private readonly sg_sampler[] _samplers = new sg_sampler[12];

    private sg_pass_action _passAction;
    private bool _inPass;
    private bool _pipelineDirty;

    /// <summary>
    /// Whether anything that needs the pass's attachments as already loaded has been recorded since
    /// the pass opened. False means the pass can be closed and reopened freely, because nothing has
    /// been drawn that discarding would lose.
    ///
    /// Only true once a draw has actually reached the GPU, deliberately: a bind that never drew
    /// leaves no output behind, so <see cref="Clear"/> can still restart the pass around it and skip
    /// a full re-issue. That is the common case in this app - <c>SetRenderTarget</c>, <c>Clear</c>,
    /// <c>SetPipeline</c> then draws - where the clear lands between the binds and the draws.
    /// </summary>
    private bool _drawnSinceBeginPass;

    /// <summary>
    /// The index format of the pipeline variant currently applied, so <see cref="Flush"/> can tell
    /// when a different one is needed. It is not implied by <see cref="_pipelineDirty"/>: one
    /// pipeline stays set across draws that differ only in indexing, because the abstraction binds
    /// an index buffer and then draws both indexed and not - so <see cref="_pipelineDirty"/> is
    /// already false while the variant still has to change.
    /// </summary>
    private sg_index_type _appliedIndexType = sg_index_type.SG_INDEXTYPE_NONE;

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
        var sokol = (SokolBuffer)buffer;
        _indexBuffer = sokol.Handle;
        _indexBufferOffset = offsetBytes;
        _indexFormat = sokol.IndexFormat.ToNative();
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
        var pipeline = RequirePipeline();

        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "A negative slot means the shader compiler optimized the uniform out; callers must skip it.");

        if (slot + value.Length > _uniformBlockSize)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"{value.Length} bytes at offset {slot} overruns the {_uniformBlockSize}-byte uniform block.");

        // Matrix4x4 values are transposed on the way in, exactly as the GL and FNA3D backends do.
        // This is the abstraction's contract rather than a quirk of any one backend: callers hold
        // row-major XNA/System.Numerics matrices, and every shader the compiler emits reads its
        // matrices the other way round - the HLSL it produces declares them `column_major` in the
        // cbuffer and multiplies row-vector style, `mul(float4(p, 1), M)` (see PolyBasic's emitted
        // `column_major float4x4 _360_View : packoffset(c14)` beside `mul(_656, _360_View)`).
        // Handed a row-major matrix unconverted, HLSL reads the transpose instead.
        //
        // The symptom is not a subtle skew, which is why this is worth a comment: a translation
        // lives in M41/M42/M43, which the shaders' row-vector convention needs in the GPU's fourth
        // *column*, so written straight through it lands in the fourth *row* and the multiply reads
        // it as the w component - a perspective divide instead of a translation. Geometry collapses
        // to a sliver or explodes rather than moving, while identity matrices (every unrotated,
        // untranslated screen-space quad) stay symmetric and so keep looking correct. That is
        // precisely the shape of the bug this fixes (an almost-empty blue screen with a thin
        // horizontal line across the middle, and occasional vertex explosions).
        //
        // The reflection decides rather than the byte count: a 64-byte write is only a matrix
        // because the bundle says so, and transposing something the shader reads as four vec4s
        // would corrupt it. Matching on the offset is what makes the lookup correct, because the
        // caller's slot *is* a byte offset (SlotOf in the generated bundles returns
        // `uniform.Offset`) while the uniform list is ordered by name - the two are not the same
        // numbering, so indexing the list with the slot would read a different parameter's type.
        if (IsMatrix4x4At(pipeline, slot))
        {
            Transpose4x4Into(value, _uniformBlock.AsSpan(slot));
                return;
        }

        value.CopyTo(_uniformBlock.AsSpan(slot));
    }

    /// <summary>
    /// Whether the reflected uniform at this byte offset is a <c>float4x4</c> - see the transpose
    /// discussion in <see cref="SetUniform"/> for why the offset is the key and not the slot index.
    /// Mirrors the identical check in <c>GlCommandBuffer.IsMatrix4x4At</c>; two uniforms cannot share
    /// an offset in a block whose size was computed from those same offsets, so the first match is
    /// the only match.
    /// </summary>
    private static bool IsMatrix4x4At(SokolPipelineState pipeline, int offset)
    {
        foreach (var uniform in pipeline.Reflection.Uniforms)
        {
            if (uniform.Offset == offset)
                return uniform.Type == UniformType.Matrix4x4;
        }

        return false;
    }

    /// <summary>
    /// Writes a row-major 4x4 as the column-major bytes the shaders' constant registers expect.
    ///
    /// Done as an in-place index permutation rather than through <c>Matrix4x4.Transpose</c> so that
    /// the value's interpretation never depends on how the caller's <c>Span&lt;byte&gt;</c> happens
    /// to be aligned - the same reasoning as <c>GlCommandBuffer.Transpose4x4Into</c>, whose body this
    /// duplicates deliberately. The two backends transposing the same way for the same reason is the
    /// point; sharing the helper instead would mean lifting a buffer-layout detail into the
    /// abstraction, which is where the row-major contract is already stated and needs nothing more.
    /// </summary>
    private static void Transpose4x4Into(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var from = (row * 4 + column) * sizeof(float);
                var to = (column * 4 + row) * sizeof(float);
                source.Slice(from, sizeof(float)).CopyTo(destination[to..]);
            }
        }
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

        // A new target's pass has not drawn either, so a clear arriving before its first draw can
        // still restart it rather than being rejected.
        _drawnSinceBeginPass = false;
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

        // A clear only takes effect if it is recorded before the pass it applies to begins, so a
        // Clear arriving after the pass has opened has to restart it. Where the action's load
        // actions can be turned into loads instead, that restart is *lossless*, which matters
        // because closing a swapchain pass and reopening it in the same commit is not merely
        // wasteful on every backend that acquires its drawable per pass - it is rejected outright
        // on Vulkan, whose single present-complete semaphore cannot be re-acquired inside one
        // commit (sokol_gfx.h:23015, and _sg_vk_end_pass leaves it set: only _sg_vk_commit clears
        // it, :23109).
        //
        // A draw cannot be reconstructed by any load action, so a pass that has already drawn is
        // the one case where this claims to clear and would not. Say so rather than replacing the
        // depth buffer with a no-op - Callers should clear before they draw.
        if (_inPass && !_drawnSinceBeginPass)
        {
            if (options.HasFlag(ClearOptions.Color))
                _passAction.colors[0].load_action = sg_load_action.SG_LOADACTION_LOAD;
            if (options.HasFlag(ClearOptions.Depth))
                _passAction.depth.load_action = sg_load_action.SG_LOADACTION_LOAD;
            if (options.HasFlag(ClearOptions.Stencil))
                _passAction.stencil.load_action = sg_load_action.SG_LOADACTION_LOAD;

            EndPass();
            BeginPass();
        }
        else if (_inPass)
        {
            ThrowIfClearWouldDiscardDraws();
        }
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
        // Resources created with initial data register on the device, not here, because they are
        // usually created while this buffer is already recording - see TrackCreated. Adopting them
        // before the emptiness check means a buffer created and drawn in the same frame still
        // uploads, and one created and never drawn costs only this check.
        device.DrainCreatedUploads(_pendingUploads);

        if (_pendingUploads.Count == 0) return;

        var frame = device.FrameIndex;
        foreach (var resource in _pendingUploads)
            if (resource.UploadPending) resource.Upload(frame);
        _pendingUploads.Clear();
    }

    public void Draw(int startVertex, int primitiveCount)
    {
        var pipeline = RequirePipeline();
        Flush(indexed: false);
        Gfx.draw(startVertex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: false), 1);
        _drawnSinceBeginPass = true;
    }

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount)
    {
        var pipeline = RequirePipeline();
        Flush(indexed: true);
        Gfx.draw_ex(startIndex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: true), 1, baseVertex, 0);
        _drawnSinceBeginPass = true;
    }

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount)
    {
        var pipeline = RequirePipeline();
        Flush(indexed: true);
        Gfx.draw_ex(startIndex, ElementCount(pipeline.Desc.Topology, primitiveCount, indexed: true), instanceCount, baseVertex, 0);
        _drawnSinceBeginPass = true;
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
    private unsafe void Flush(bool indexed)
    {
        var pipeline = RequirePipeline();

        // Uploads go before BeginPass: sg_update_buffer/sg_update_image are not pass-recording
        // calls, and sokol asserts they happen outside a pass. Both are also once-per-resource-per-
        // frame, which FlushUploads collapses by flushing each resource at most once.
        FlushUploads();
        BeginPass();

        // sokol bakes the index format into the pipeline (sg_pipeline_desc.index_type) while the
        // abstraction supplies it per buffer at SetIndexBuffer time, so the two are reconciled here
        // by applying the variant that matches the draw about to be issued.
        //
        // Get this wrong and the draw is rejected rather than misdrawn: sg_apply_bindings
        // cross-validates the bound index buffer against the applied pipeline's index_type
        // (:25174-25179) and a mismatch clears _sg.next_draw_valid, which sg_draw then treats as
        // "skip" (:27476). Both halves of the match are needed - the variant chosen here and the
        // index buffer withheld in ApplyBindings - and neither is implied by the other.
        var indexType = indexed ? _indexFormat : sg_index_type.SG_INDEXTYPE_NONE;

        if (_pipelineDirty || indexType != _appliedIndexType)
        {
            // The variant matching this pass's colour attachment and this draw's indexing - a
            // swapchain pass takes the environment default. See SokolPipelineState._variants.
            Gfx.apply_pipeline(pipeline.HandleFor(_target?.ColorFormat ?? device.DefaultColorFormat, indexType));
            _pipelineDirty = false;
            _appliedIndexType = indexType;
        }

        // Viewport and scissor default to the whole drawable, matching FNA3D's device defaults,
        // but they must be (re)applied whenever a new pass opens or a new pipeline binds.
        var viewport = _viewport ?? new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height);
        Gfx.apply_viewportf(viewport.X, viewport.Y, viewport.Width, viewport.Height, SokolNative.Bool(true));

        if (_scissor is { } scissor)
            Gfx.apply_scissor_rect(scissor.X, scissor.Y, scissor.Width, scissor.Height, SokolNative.Bool(true));

        // Uniforms first: sg_apply_uniforms asserts the data matches the applied pipeline's block.
        // A program keeps its uniforms in one flat buffer, and each block the pipeline declares
        // covers a slice of it - the whole thing on D3D11, where both stages read one merged
        // register(b0) cbuffer, and a 16-member piece of it on GL, where sokol's descriptor cannot
        // describe more. Either way the slice is uploaded verbatim: the GL split was arranged so
        // that sokol's own member offsets within a block equal the reflection's offsets minus the
        // block's start, so no per-block translation is needed here.
        foreach (var block in pipeline.UniformBlocks)
        {
            fixed (byte* pointer = _uniformBlock)
            {
                var range = SokolNative.Range(pointer + block.Offset, block.Size);
                // The slot, not the register: see UniformBlock.Slot - on D3D11 the two stages sit
                // in different slots while both binding b0, so passing the register here would
                // apply one block twice and leave the other's slot unfilled.
                Gfx.apply_uniforms(block.Slot, &range);
            }
        }

        ApplyBindings(pipeline, indexed);
    }

    /// <summary>
    /// Fills every view and sampler slot the applied pipeline declares, then binds the set.
    ///
    /// The caller is not obliged to fill them all, and GL does not mind when one goes unbound - a
    /// sampler uniform left at its default reads texture unit 0, so the draw samples whatever
    /// happens to be there. sokol validates instead: every slot the shader desc declares must be
    /// bound or <c>sg_apply_bindings</c> fails <c>VALIDATE_ABND_EXPECTED_VIEW_BINDING</c>
    /// (<c>sokol_gfx.h:25197-25199</c>, which walks the shader desc's slots, not the caller's) and
    /// <em>skips the whole bind</em> - so the draw would proceed against the previous draw's
    /// bindings rather than merely missing one slot.
    ///
    /// Leaving slots unfilled is not an accident here: sokol declares a program's whole reflection,
    /// so a program carrying two techniques declares textures only one of them samples - see
    /// <see cref="SokolGraphicsDevice.PlaceholderView"/> for the three cases in this app. The
    /// declared-but-unfilled slots therefore get that placeholder, which restores GL's tolerant
    /// behaviour with the least surprising content available.
    /// </summary>
    private unsafe void ApplyBindings(SokolPipelineState pipeline, bool indexed)
    {
        // ShaderBindings numbers view and sampler slots sequentially from zero (ViewCount /
        // SamplerCount), so "declared" is the range [0, count) - not the slot maps, which only
        // cover the abstraction's texture slots and would miss a declared-but-unmapped slot.
        for (var i = 0; i < pipeline.DeclaredViewSlots; i++)
        {
            if (_views[i].id == 0) _views[i] = device.PlaceholderView;
        }
        for (var i = 0; i < pipeline.DeclaredSamplerSlots; i++)
        {
            if (_samplers[i].id == 0) _samplers[i] = device.PlaceholderSampler;
        }

        var bindings = new sg_bindings();
        for (var i = 0; i < _vertexBuffers.Length; i++)
        {
            bindings.vertex_buffers[i] = _vertexBuffers[i];
            bindings.vertex_buffer_offsets[i] = _vertexBufferOffsets[i];
        }
        // Withheld from a non-indexed draw, because sokol cross-validates the two: a non-indexed
        // pipeline with an index buffer bound fails VALIDATE_ABND_EXPECTED_NO_IBUF
        // (sokol_gfx.h:25174-25176), which sets _sg.next_draw_valid = false and makes sg_draw a
        // silent no-op (:27476). Leaving it in would drop the draw rather than merely log.
        //
        // This is not a hypothetical leftover: NanoVG binds one index buffer for its fan and strip
        // paths and then calls non-indexed Draw for the strip, so the stale binding is the normal
        // case, not an oversight by the caller.
        bindings.index_buffer = indexed ? _indexBuffer : default;
        bindings.index_buffer_offset = indexed ? _indexBufferOffset : 0;
        for (var i = 0; i < _views.Length; i++) bindings.views[i] = _views[i];
        for (var i = 0; i < _samplers.Length; i++) bindings.samplers[i] = _samplers[i];

        Gfx.apply_bindings(&bindings);
    }

    /// <summary>
    /// Starts a pass if one is open. sokol requires a draw to happen inside a pass, and a pass to
    /// name either the swapchain or explicit attachment views - which is why the target is read
    /// here rather than at <see cref="SetRenderTarget"/>.
    /// </summary>
    private unsafe void BeginPass()
    {
        if (_inPass) return;

        // Every draw passes through here, so this is the cheapest place to catch a render call
        // arriving on a thread other than the one that created the device. The platform decides
        // whether that matters (ISokolPlatform.SingleThreadedLifetime); the check is an integer
        // comparison once the creating thread is known.
        device.EnsureRenderingThread();

        var pass = new sg_pass { action = _passAction };

        if (_target is null)
        {
            // A swapchain pass: sokol's D3D11 backend takes the backbuffer's render-target and
            // depth-stencil views verbatim and calls OMSetRenderTargets with them, with no pooling
            // and no AddRef (sokol_gfx.h:14840-14849), so these must be the platform's live views
            // and must stay valid for as long as the pass is open.
            //
            // Re-read per pass rather than cached, because a resize between frames replaces them.
            // Reading them at BeginPass rather than at AcquireCommandBuffer also means the views
            // sokol gets are the ones from after any resize this frame.
            pass.swapchain = device.AcquireSwapchain();
        }
        else
        {
            pass.attachments.colors[0] = _target.ColorAttachmentView;
            pass.attachments.depth_stencil = _target.DepthStencilAttachmentView;
        }

        Gfx.begin_pass(&pass);
        _inPass = true;

        // Nothing has been drawn into this pass yet, so it can still be restarted losslessly - see
        // _drawnSinceBeginPass.
        _drawnSinceBeginPass = false;

        // A fresh pass resets the applied pipeline, so the next draw must re-issue it - including
        // its index-format variant, which the reset forgets entirely.
        _pipelineDirty = true;
        _appliedIndexType = sg_index_type.SG_INDEXTYPE_NONE;
    }

    private void EndPass()
    {
        if (!_inPass) return;
        Gfx.end_pass();
        _inPass = false;
    }

    /// <summary>
    /// Rejects a clear that arrives after this pass has drawn, because honoring it would mean
    /// discarding the draws it followed.
    ///
    /// The abstraction documents <c>Clear</c> as preceding the draws it precedes
    /// (<see cref="ICommandBuffer.Clear"/>), and on every backend that services a swapchain pass by
    /// acquiring its drawable, reopening the pass to apply a late clear is not possible at all - so
    /// the alternative to throwing is a call that silently does nothing while reporting success.
    /// The message names the call sites' obligation rather than this implementation's limitation.
    ///
    /// Reached from a mid-frame clear on a target that already has geometry recorded, so it is a
    /// caller error rather than a state this class can recover from.
    /// </summary>
    private void ThrowIfClearWouldDiscardDraws() =>
        throw new InvalidOperationException(
            $"{nameof(Clear)} cannot be applied after draws have been recorded into this pass: on the " +
            "swapchain pass, reopening it to make the clear take effect is impossible on the backends " +
            "that acquire their drawable per pass. Issue clears before the draws they precede.");

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
