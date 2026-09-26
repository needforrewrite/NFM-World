// LLM maintained.
//
// The command buffer. D3D11's immediate context executes as it is called, so like GL's this records
// nothing - every method issues its call straight away, and Submit is left with only the per-frame
// bookkeeping to drop.
//
// The real work is the constant buffer. The abstraction addresses uniforms by byte offset into the
// block the shader compiler laid out with D3D packoffsets, which is exactly what a D3D11 constant
// buffer is, so the block is kept as CPU bytes, written through by SetUniform, and uploaded whole
// before a draw - the same shape both other backends use, and for the same reason.

using System.Numerics;
using System.Runtime.InteropServices;
using Maxine.Extensions.Mathematics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

internal sealed unsafe class D3D11CommandBuffer : ICommandBuffer
{
    /// <summary>
    /// The abstraction allows as many vertex streams as the pipeline declares. The app's instanced
    /// layouts use two (geometry plus per-instance), so a small fixed array covers every real caller
    /// while keeping the state a plain stack value - the same bound the GL backend picks.
    /// </summary>
    private const int MaxVertexStreams = 4;

    /// <summary>
    /// How many texture and sampler slots this backend binds per draw.
    ///
    /// Six, and the number is dictated rather than chosen: a slot <em>is</em> a shader's bind point
    /// (`SlotOf` in the generated bundles returns the reflection's slot, which for D3D11 is the
    /// <c>register(tN)</c> the source declared), so the array has to reach the highest register any
    /// shader in the tree uses. That is <c>apos-shapes.fx</c>: four glyph-atlas textures at t0..t3, a
    /// blue-noise tile at t4 and a ramp at t5. The app's widest engine shader is far narrower - the
    /// three shadow cascades at t0..t2 - which is why three looked like enough, and why a fixed
    /// bound derived from that would have been wrong the first time a shape was drawn.
    ///
    /// The check in <see cref="SetShaderResource"/> is what makes an under-sized bound a named
    /// failure at the bind rather than a silently dropped texture, which is how this was found.
    /// </summary>
    private const int ResourceSlots = 6;

    private readonly D3D11GraphicsDevice _device;

    private D3D11PipelineState? _pipeline;

    /// <summary>
    /// The merged uniform block's bytes, sized to the pipeline's reflection, plus the GPU buffer they
    /// are uploaded through. Both are reallocated when a pipeline with a different block size binds.
    /// </summary>
    private byte[] _uniforms = [];
    private ID3D11Buffer* _uniformBuffer;

    /// <summary>The render target bound by the caller, or null for the swapchain's back buffer.</summary>
    private D3D11RenderTarget? _target;

    /// <summary>
    /// The last viewport and scissor set, in the target's own pixel coordinates.
    ///
    /// Kept rather than written straight to the context and forgotten, because D3D11 rejects both
    /// calls with no render target bound and NanoVG draws without ever calling either - so binding a
    /// target has to re-apply whatever was last asked for. See <see cref="ApplyViewportAndScissor"/>.
    /// </summary>
    private bool _hasViewport;
    private D3D11_VIEWPORT _viewport;
    private bool _hasScissor;
    private RECT _scissor;

    /// <summary>
    /// The render target size the viewport and scissor fall back to when the caller has set neither.
    /// Updated by <see cref="SetRenderTarget"/> and by the swapchain, which the device re-reports to
    /// the live command buffer after a resize.
    /// </summary>
    private int _targetWidth;
    private int _targetHeight;

    private readonly VertexStream[] _vertexStreams = new VertexStream[MaxVertexStreams];
    private D3D11Buffer? _indexBuffer;
    private int _indexOffsetBytes;

    /// <summary>
    /// The resource and sampler views this command buffer has handed out, each with its own
    /// reference.
    ///
    /// The command buffer takes a reference on everything it binds, because a draw outlives the
    /// caller's variable - the app creates a texture, binds it and drops its own reference in the
    /// same frame, and D3D11 would then be reading free storage. Releasing the previous occupant of a
    /// slot at the same time is what makes rebinding the same texture twice in a frame a no-op rather
    /// than a leak.
    /// </summary>
    private readonly ID3D11ShaderResourceView*[] _resources = new ID3D11ShaderResourceView*[ResourceSlots];
    private readonly ID3D11SamplerState*[] _samplers = new ID3D11SamplerState*[ResourceSlots];

    /// <summary>
    /// Whether <see cref="Clear"/> has changed state the bound pipeline owns since it was applied, so
    /// a rebind has to re-apply even though the pipeline itself has not changed.
    ///
    /// The same flag the GL backend carries and for the same mechanism: D3D11's depth-write mask is
    /// part of the pipeline's depth-stencil object, so a pipeline that disabled depth writes cannot
    /// have its depth cleared without swapping that object out - and the pipeline is the only thing
    /// that can put its own state back.
    /// </summary>
    private bool _pipelineStateLostToClear;

    /// <summary>The depth-stencil state installed around a depth clear. See <see cref="Clear"/>.</summary>
    private ID3D11DepthStencilState* _depthClearState;

    private readonly struct VertexStream
    {
        internal D3D11Buffer? Buffer { get; init; }
        internal int StrideBytes { get; init; }
        internal int OffsetBytes { get; init; }
    }

    /// <summary>
    /// The swapchain, typed. <see cref="IGraphicsDevice.Swapchain"/> is the interface, which carries
    /// neither the multisampled colour view this backend renders into nor the depth view it clears, so
    /// the concrete field is what the draw path reads.
    /// </summary>
    private readonly D3D11Swapchain _swapchain;

    internal D3D11CommandBuffer(D3D11GraphicsDevice device)
    {
        _device = device;
        _swapchain = device.D3d11Swapchain;
        _targetWidth = _swapchain.Width;
        _targetHeight = _swapchain.Height;
    }

    /// <summary>
    /// Binds a pipeline. Re-binding the one already bound is a no-op, apart from the case where
    /// <see cref="Clear"/> has moved the state it owns.
    /// </summary>
    public void SetPipeline(IPipelineState pipeline)
    {
        var d3dPipeline = (D3D11PipelineState)pipeline;

        // Resized on every call rather than only on a pipeline change: a caller may write a uniform
        // after re-binding the same pipeline, and that write has to land in the size this pipeline's
        // block actually is. The comparison is a length check, so the steady-state cost is that.
        var size = ComputeBlockSize(d3dPipeline);
        if (_uniforms.Length != size)
        {
            _uniforms = new byte[size];
            D3D11Interop.Release(ref _uniformBuffer);
        }

        if (ReferenceEquals(_pipeline, d3dPipeline) && !_pipelineStateLostToClear)
            return;

        _pipeline = d3dPipeline;
        _pipelineStateLostToClear = false;
        d3dPipeline.Apply(_device);
    }

    /// <summary>
    /// The uniform block's size: the highest offset+size any reflected uniform reaches, rounded up
    /// to 16 bytes.
    ///
    /// The rounding is not cosmetic. D3D11 requires a constant buffer to be a multiple of 16 bytes,
    /// and the packoffset numbering the reflection was generated from is in 16-byte registers - so a
    /// block sized to the last member alone would be rejected at creation for every shader whose last
    /// member does not end on a register boundary.
    /// </summary>
    private static int ComputeBlockSize(D3D11PipelineState pipeline)
    {
        var end = 0;
        foreach (var uniform in pipeline.Reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);
        return (end + 15) & ~15;
    }

    /// <summary>
    /// Writes a uniform into the block at the caller's byte offset.
    ///
    /// The abstraction's slot is the offset the generated bundle carries - <c>SlotOf</c> in the
    /// generated types returns <c>uniform.Offset</c> - so this is a byte-offset write into a byte
    /// buffer and a partial one is safe, because the whole block is uploaded as a unit and the
    /// untouched bytes keep whatever the previous write left.
    ///
    /// Matrix4x4 values are transposed on the way in. That is the abstraction's contract rather than
    /// a D3D11 quirk: callers hold row-major <c>System.Numerics</c> matrices, the generated HLSL
    /// declares them <c>column_major</c>, and its arithmetic is row-vector style
    /// (<c>mul(float4(input_Centroid, 1.0f), world)</c>), so a row-major value written through
    /// unconverted is read as its own transpose. A translation lives in M41/M42/M43 and has to
    /// occupy the GPU's fourth <em>column</em>; written straight through it lands in the fourth
    /// <em>row</em>, where the multiply reads it as w - a perspective divide instead of a
    /// translation. Identity matrices are symmetric, so untranslated geometry keeps looking right,
    /// which is what makes the failure easy to misread.
    /// </summary>
    public void SetUniform(int slot, ReadOnlySpan<byte> value)
    {
        var pipeline = RequirePipeline();

        // A negative slot resolves to a uniform the HLSL compiler dropped. The generated wrappers
        // already no-op on it, so this is only reachable from a caller that skipped the reflection.
        if (slot < 0)
            return;

        if (slot + value.Length > _uniforms.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot),
                $"Uniform write of {value.Length} byte(s) at offset {slot} does not fit the " +
                $"{_uniforms.Length}-byte block {pipeline.Desc.VertexShader.GetType().Name} declares.");
        }

        if (IsMatrix4x4At(pipeline, slot))
        {
            Transpose4x4Into(value, _uniforms.AsSpan(slot));
            return;
        }

        value.CopyTo(_uniforms.AsSpan(slot));
    }

    /// <summary>
    /// Whether the reflected uniform at this byte offset is a <c>float4x4</c>.
    ///
    /// Matched on the offset because that is what a caller's slot is. Two uniforms cannot share an
    /// offset in a block sized from those same offsets, so the first match is the only match.
    /// </summary>
    private static bool IsMatrix4x4At(D3D11PipelineState pipeline, int offset)
    {
        foreach (var uniform in pipeline.Reflection.Uniforms)
        {
            if (uniform.Offset == offset)
                return uniform.Type == Shaders.UniformType.Matrix4x4;
        }

        return false;
    }

    /// <summary>
    /// Writes a row-major 4x4 as the column-major bytes the constant registers expect.
    ///
    /// An index permutation rather than <c>Matrix4x4.Transpose</c>, so the value's interpretation
    /// never depends on how the caller's <c>Span&lt;byte&gt;</c> happens to be aligned - the
    /// abstraction's contract is bytes, and reinterpreting them as a struct would make alignment a
    /// correctness requirement that nothing states.
    /// </summary>
    private static void Transpose4x4Into(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var mat = MemoryMarshal.Read<Matrix4x4>(source);
        mat.Transpose();
        MemoryMarshal.Write(destination, in mat);
    }

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0)
    {
        if ((uint)slot >= MaxVertexStreams)
        {
            throw new ArgumentOutOfRangeException(nameof(slot),
                $"Vertex stream slot {slot} exceeds the {MaxVertexStreams} this backend tracks.");
        }

        _vertexStreams[slot] = new VertexStream
        {
            Buffer = (D3D11Buffer)buffer,
            StrideBytes = strideBytes,
            OffsetBytes = offsetBytes,
        };
    }

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0)
    {
        _indexBuffer = (D3D11Buffer)buffer;
        _indexOffsetBytes = offsetBytes;
    }

    /// <summary>
    /// Records a texture and its sampler against a slot.
    ///
    /// Nothing is bound to the context here - see <see cref="BindResources"/> - because D3D11 takes
    /// the views as a contiguous array starting at a slot, so setting them one at a time would either
    /// clobber a neighbour or need a full rebind anyway.
    /// </summary>
    public void SetShaderResource(int slot, ITexture texture, ISampler sampler)
    {
        RequirePipeline();

        if ((uint)slot >= ResourceSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slot),
                $"Texture slot {slot} exceeds the {ResourceSlots} this backend binds per draw. Raise " +
                $"{nameof(ResourceSlots)} in D3D11CommandBuffer - the shader's bind point is what the " +
                "slot is, so nothing else needs changing.");
        }

        var d3dTexture = (D3D11Texture)texture;

        D3D11Interop.Release(ref _resources[slot]);
        if (d3dTexture.View is not null)
        {
            // A multisampled texture has no shader resource view - D3D11 cannot sample one - so this
            // is a real case rather than a guard: the swapchain's multisampled surface reaches here
            // if a caller binds a render target's texture while it is still multisampled.
            d3dTexture.View->AddRef();
            _resources[slot] = d3dTexture.View;
        }

        var d3dSampler = (D3D11Sampler)sampler;
        D3D11Interop.Release(ref _samplers[slot]);
        d3dSampler.Handle->AddRef();
        _samplers[slot] = d3dSampler.Handle;
    }

    /// <summary>
    /// Binds a render target, or the swapchain's back buffer for null.
    ///
    /// On this backend "the swapchain's back buffer" is whichever surface the multisampled pass
    /// rendered into - the multisampled texture when one exists - so a caller that binds null and a
    /// caller that never binds anything draw into the same place.
    /// </summary>
    public void SetRenderTarget(IRenderTarget? target)
    {
        var d3dTarget = (D3D11RenderTarget?)target;

        var colorView = d3dTarget is null ? _swapchain.ColorView : d3dTarget.ColorView;
        var depthView = d3dTarget is null ? _swapchain.DepthView : d3dTarget.DepthView;

        if (colorView is null)
        {
            // The swapchain's constructor guarantees a back buffer view, so this is a belt-and-braces
            // check that turns a would-be null dereference into a sentence.
            throw new InvalidOperationException(
                "No render target is bound and the swapchain has no back buffer view to fall back to.");
        }

        var views = stackalloc ID3D11RenderTargetView*[1];
        views[0] = colorView;
        _device.Context->OMSetRenderTargets(1, views, depthView);

        _target = d3dTarget;
        _targetWidth = d3dTarget?.ColorTexture.Width ?? _swapchain.Width;
        _targetHeight = d3dTarget?.ColorTexture.Height ?? _swapchain.Height;

        // D3D11 rejects both calls with no target bound, so whatever was last set has to be re-applied
        // here. NanoVG depends on this: it binds a target, sets a uniform and draws, without ever
        // calling SetViewport or SetScissorRect.
        ApplyViewportAndScissor();
    }

    /// <summary>
    /// Sets the viewport.
    ///
    /// No Y flip: D3D's viewport origin is the top left, which is the convention the abstraction
    /// follows (it was designed against FNA3D). The two GL backends flip here precisely because their
    /// origin is the other one; this is the backend where the coordinates mean what they say.
    /// </summary>
    public void SetViewport(Viewport viewport)
    {
        _viewport = new D3D11_VIEWPORT
        {
            TopLeftX = viewport.X,
            TopLeftY = viewport.Y,
            Width = viewport.Width,
            Height = viewport.Height,
            // The abstraction's depth range is D3D's [0, 1], which the generated HLSL's clip space
            // already assumes, so the default is correct and a caller's explicit range replaces it.
            MinDepth = viewport.MinDepth,
            MaxDepth = viewport.MaxDepth,
        };
        _hasViewport = true;

        var viewports = stackalloc D3D11_VIEWPORT[1];
        viewports[0] = _viewport;
        _device.Context->RSSetViewports(1, viewports);
    }

    public void SetScissorRect(ScissorRect rect)
    {
        _scissor = new RECT
        {
            left = rect.X,
            top = rect.Y,
            right = rect.X + rect.Width,
            bottom = rect.Y + rect.Height,
        };
        _hasScissor = true;

        var rects = stackalloc RECT[1];
        rects[0] = _scissor;
        _device.Context->RSSetScissorRects(1, rects);
    }

    /// <summary>
    /// Re-applies the last viewport and scissor, for the case where a target was bound since.
    ///
    /// When a caller has set neither, the fallback is the whole of the current target. That is what
    /// the GL backends get for free - GL's viewport and scissor persist across a framebuffer bind, so
    /// a caller who never calls those two methods renders into whatever the last framebuffer's size
    /// was. Reproducing it here is what keeps a backend that binds the swapchain first and a target
    /// later from resetting the viewport only on one of the two.
    /// </summary>
    private void ApplyViewportAndScissor()
    {
        var viewport = _hasViewport
            ? _viewport
            : new D3D11_VIEWPORT
            {
                TopLeftX = 0f,
                TopLeftY = 0f,
                Width = _targetWidth,
                Height = _targetHeight,
                MinDepth = 0f,
                MaxDepth = 1f,
            };

        var scissor = _hasScissor
            ? _scissor
            : new RECT { left = 0, top = 0, right = _targetWidth, bottom = _targetHeight };

        var viewports = stackalloc D3D11_VIEWPORT[1];
        viewports[0] = viewport;
        _device.Context->RSSetViewports(1, viewports);

        var rects = stackalloc RECT[1];
        rects[0] = scissor;
        _device.Context->RSSetScissorRects(1, rects);
    }

    /// <summary>
    /// Clears the bound target.
    ///
    /// The colour half is easy: <c>ClearRenderTargetView</c> writes the whole subresource and the
    /// pipeline's colour write mask plays no part, so there is no state to work around.
    ///
    /// The depth half is not. A D3D11 clear's depth-write mask is not an argument - it is a field of
    /// the bound depth-stencil object, which is the pipeline's - and several of this app's pipelines
    /// are created with <c>DepthWriteEnabled: false</c> (NanoVG's, ImGui's, every wireframe pass). A
    /// clearDepth on one of those would silently clear nothing. So a depth-write-enabled object is
    /// installed for the duration of the call and then the pipeline is re-applied, which is the only
    /// thing that can put its own object back.
    ///
    /// That is more work than the GL backend does for the same problem - it forces its three masks
    /// and lets the next <c>SetPipeline</c> restore them - and the difference is deliberate: on GL
    /// the restore is a handful of glEnable calls, while here the cheap version would mean a second
    /// depth-stencil object per pipeline built at creation for a state a caller may never use.
    /// </summary>
    public void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        var context = _device.Context;

        if (options.HasFlag(ClearOptions.Color))
        {
            var view = _target is null ? _swapchain.ColorView : _target.ColorView;
            if (view is not null)
            {
                var rgba = stackalloc float[4] { color.R, color.G, color.B, color.A };
                context->ClearRenderTargetView(view, rgba);
            }
        }

        var wantsDepth = options.HasFlag(ClearOptions.Depth);
        var wantsStencil = options.HasFlag(ClearOptions.Stencil);
        if (!wantsDepth && !wantsStencil)
            return;

        var depthView = _target is null ? _swapchain.DepthView : _target.DepthView;
        if (depthView is null)
        {
            // Not an error: a caller may ask for a depth clear against a target built without a
            // depth-stencil attachment, and there is nothing to clear. It is what GL gives for free,
            // since an FBO with no depth attachment accepts the bits and discards them.
            return;
        }

        // The two bits are independent, and a depth-stencil view whose format has no stencil half
        // rejects the stencil bit - so they are forwarded exactly as asked rather than both always
        // being sent. ORed as uints because the flag enum reaches the call as a plain uint.
        var flags = (wantsDepth ? (uint)D3D11_CLEAR_FLAG.D3D11_CLEAR_DEPTH : 0u)
            | (wantsStencil ? (uint)D3D11_CLEAR_FLAG.D3D11_CLEAR_STENCIL : 0u);

        if (!wantsDepth)
        {
            // Nothing to work around: the depth-write mask is irrelevant to a clear that leaves depth
            // alone, so the bound state is already correct.
            context->ClearDepthStencilView(depthView, flags, depth, (byte)stencil);
            return;
        }

        if (_depthClearState is null)
            _depthClearState = CreateDepthClearState();

        context->OMSetDepthStencilState(_depthClearState, (uint)stencil);
        context->ClearDepthStencilView(depthView, flags, depth, (byte)stencil);

        if (_pipeline is not null)
        {
            _pipeline.Apply(_device);
            return;
        }

        // No pipeline to re-apply, so the default goes back - the state the context would have had if
        // nothing had been bound, and which the next SetPipeline overwrites regardless.
        _pipelineStateLostToClear = true;
        context->OMSetDepthStencilState(null, 0);
    }

    /// <summary>
    /// A depth-stencil state with depth writes on and everything else at its permissive default, for
    /// <see cref="Clear"/> to install around a depth clear.
    ///
    /// Created once and kept, rather than per clear: the alternative is a COM object allocation every
    /// frame on the exact path the whole arrangement exists to keep cheap.
    /// </summary>
    private ID3D11DepthStencilState* CreateDepthClearState()
    {
        var desc = new D3D11_DEPTH_STENCIL_DESC
        {
            DepthEnable = true,
            DepthWriteMask = D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ALL,
            // Unused by a clear, which is not a depth test - set to ALWAYS so it can never be the
            // reason a later draw behaves differently if this object were ever left bound.
            DepthFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
            StencilEnable = false,
            StencilReadMask = 0xff,
            StencilWriteMask = 0xff,
        };

        ID3D11DepthStencilState* state = null;
        D3D11Interop.Check(
            _device.Device->CreateDepthStencilState(&desc, &state), "CreateDepthStencilState (depth clear)");
        return state;
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0) =>
        ((D3D11Buffer)buffer).Update(_device, data, offsetBytes);

    public void UpdateTexture(ITexture texture, int x, int y, int width, int height, ReadOnlySpan<byte> data) =>
        ((D3D11Texture)texture).Update(_device, x, y, width, height, data);

    public void Draw(int startVertex, int primitiveCount) =>
        DrawInternal(baseVertex: 0, firstIndex: startVertex, primitiveCount, instanceCount: 1, indexed: false);

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount) =>
        DrawInternal(baseVertex, firstIndex: startIndex, primitiveCount, instanceCount: 1, indexed: true);

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount) =>
        DrawInternal(baseVertex, firstIndex: startIndex, primitiveCount, instanceCount, indexed: true);

    /// <summary>
    /// The one draw path. Everything a draw needs - the constant buffer, the vertex streams, the
    /// resource slots and the element count - is assembled here so the three public entry points
    /// cannot drift.
    ///
    /// <paramref name="firstIndex"/> is the abstraction's <c>startVertex</c>/<c>startIndex</c>: a
    /// position, not a count. It becomes the first vertex for a non-indexed draw and an index-buffer
    /// byte offset for an indexed one - the same split the GL backend makes, and the reason it is
    /// named for its indexed meaning here.
    /// </summary>
    private void DrawInternal(int baseVertex, int firstIndex, int primitiveCount, int instanceCount, bool indexed)
    {
        var pipeline = RequirePipeline();

        // The pipeline's own depth-stencil object is the only thing that can undo a Clear's depth
        // write. Draw is the last point at which that can be put right.
        if (_pipelineStateLostToClear)
        {
            _pipelineStateLostToClear = false;
            pipeline.Apply(_device);
        }

        var context = _device.Context;

        UploadUniforms();
        BindResources();
        BindVertexStreams(pipeline);

        context->IASetPrimitiveTopology(pipeline.Desc.Topology.ToTopology());

        // D3D11 takes the *element* count, not the primitive count, so a strip needs its extra
        // leading element - the same conversion the GL backend makes.
        var count = (uint)ElementCount(pipeline.Desc.Topology, primitiveCount);

        if (!indexed)
        {
            if (instanceCount == 1)
                context->Draw(count, (uint)firstIndex);
            else
                context->DrawInstanced(count, (uint)instanceCount, (uint)firstIndex, 0);
            return;
        }

        var indexBuffer = _indexBuffer
            ?? throw new InvalidOperationException($"DrawIndexed requires {nameof(SetIndexBuffer)} to have been called.");

        // The index buffer is bound here rather than in SetIndexBuffer because its width is a
        // per-draw argument and D3D11 will not take one without the other. Rebinding per draw also
        // means a stale binding can never survive a target or pipeline change.
        //
        // The offset is the buffer's own start plus the first index's byte position, matching the GL
        // backend: the abstraction's startIndex indexes the buffer, and the base vertex then shifts
        // which vertex each of those indices names.
        context->IASetIndexBuffer(
            indexBuffer.Handle,
            indexBuffer.IndexFormat.ToDxgiFormat(),
            (uint)(_indexOffsetBytes + firstIndex * IndexSize(indexBuffer.IndexFormat)));

        if (instanceCount == 1)
            context->DrawIndexed(count, 0, baseVertex);
        else
            context->DrawIndexedInstanced(count, (uint)instanceCount, 0, baseVertex, 0);
    }

    private static int IndexSize(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => 2,
        IndexFormat.UInt32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Binds the vertex streams the pipeline's layouts name.
    ///
    /// One call for all of them: D3D11 - unlike ES 3.0 - takes the buffers, the strides and the
    /// offsets as three parallel arrays in a single <c>IASetVertexBuffers</c>. That is the whole
    /// difference from the GL backend's per-attribute loop, because the attributes' offsets and
    /// formats were fixed into the input layout at pipeline creation; what is left per draw is only
    /// where each stream starts and how wide one element is.
    ///
    /// The loop runs over the pipeline's declared slots rather than over the streams the caller
    /// filled, which is what makes a hole the caller's error rather than a null entry in the middle
    /// of the array - D3D11 rejects those outright, and it rejects them with E_INVALIDARG rather
    /// than naming the slot.
    /// </summary>
    private void BindVertexStreams(D3D11PipelineState pipeline)
    {
        var slotCount = Math.Max(1, pipeline.Desc.VertexLayouts.Count);

        if (slotCount > MaxVertexStreams)
        {
            throw new InvalidOperationException(
                $"The pipeline declares {slotCount} vertex layout slots but this backend tracks {MaxVertexStreams}.");
        }

        var buffers = stackalloc ID3D11Buffer*[MaxVertexStreams];
        var strides = stackalloc uint[MaxVertexStreams];
        var offsets = stackalloc uint[MaxVertexStreams];

        for (var slot = 0; slot < slotCount; slot++)
        {
            var stream = _vertexStreams[slot];
            var buffer = stream.Buffer
                ?? throw new InvalidOperationException(
                    $"The pipeline declares vertex layout slot {slot} but {nameof(SetVertexBuffer)} " +
                    "was never called for it.");

            buffers[slot] = buffer.Handle;
            strides[slot] = (uint)stream.StrideBytes;
            offsets[slot] = (uint)stream.OffsetBytes;
        }

        _device.Context->IASetVertexBuffers(0, (uint)slotCount, buffers, strides, offsets);
    }

    /// <summary>
    /// Uploads the block as a whole constant buffer and binds it to slot 0 of both stages.
    ///
    /// Both, not one, and that is a property of the shaders rather than a guess: the shader compiler
    /// merges both stages' uniforms into a single <c>cbuffer _Global : register(b0)</c> with the same
    /// members and packoffsets in the vertex and pixel HLSL of every bundle in this tree - checked by
    /// diffing the two member lists - so one buffer serves both, and a uniform only the pixel stage
    /// reads would be stale if only the vertex slot were bound.
    ///
    /// <c>UpdateSubresource</c> rather than a mapped write: the buffer is DEFAULT-usage, and a
    /// discard-map would only pay off for a buffer large enough that the driver's copy path shows up,
    /// which this one - under a kilobyte in every bundle here - is not.
    /// </summary>
    private void UploadUniforms()
    {
        if (_uniforms.Length == 0)
            return;

        if (_uniformBuffer is null)
        {
            var desc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)_uniforms.Length,
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11Flags.BindConstantBuffer,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0,
            };

            ID3D11Buffer* buffer = null;
            D3D11Interop.Check(
                _device.Device->CreateBuffer(&desc, null, &buffer),
                $"CreateBuffer (constant, {_uniforms.Length} byte(s))");
            _uniformBuffer = buffer;
        }

        fixed (byte* data = _uniforms)
            _device.Context->UpdateSubresource((ID3D11Resource*)_uniformBuffer, 0, null, data, 0, 0);

        var buffers = stackalloc ID3D11Buffer*[1];
        buffers[0] = _uniformBuffer;
        _device.Context->VSSetConstantBuffers(0, 1, buffers);
        _device.Context->PSSetConstantBuffers(0, 1, buffers);
    }

    /// <summary>
    /// Binds every resource slot as two contiguous arrays.
    ///
    /// Null entries are legal here and are what a shader declaring fewer textures than
    /// <see cref="ResourceSlots"/> wants: D3D11 permits a null in the array, and a pixel shader that
    /// samples a slot nothing was bound to reads zero rather than faulting. That case is real rather
    /// than defensive - the four Nvg bundles declare a sampler binding with no sampler state at all,
    /// and a shader with no textures at all still arrives here.
    /// </summary>
    private void BindResources()
    {
        var views = stackalloc ID3D11ShaderResourceView*[ResourceSlots];
        var samplers = stackalloc ID3D11SamplerState*[ResourceSlots];

        for (var slot = 0; slot < ResourceSlots; slot++)
        {
            views[slot] = _resources[slot];
            samplers[slot] = _samplers[slot];
        }

        _device.Context->PSSetShaderResources(0, ResourceSlots, views);
        _device.Context->PSSetSamplers(0, ResourceSlots, samplers);
    }

    /// <summary>
    /// Converts the abstraction's primitive count (matching FNA3D's <c>DrawPrimitives</c>) into
    /// D3D11's element count. A strip consumes one extra element for its first primitive, so it
    /// cannot use the list multiplier.
    /// </summary>
    private static int ElementCount(PrimitiveTopology topology, int primitiveCount) => topology switch
    {
        PrimitiveTopology.TriangleList => primitiveCount * 3,
        PrimitiveTopology.LineList => primitiveCount * 2,
        PrimitiveTopology.PointList => primitiveCount,
        PrimitiveTopology.TriangleStrip => primitiveCount + 2,
        PrimitiveTopology.LineStrip => primitiveCount + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
    };

    private D3D11PipelineState RequirePipeline() =>
        _pipeline ?? throw new InvalidOperationException($"{nameof(SetPipeline)} must be called before issuing draw work.");

    /// <summary>
    /// Re-reads the swapchain's size. Called by the device after a resize, because the viewport and
    /// scissor fallbacks are sized from it and a live command buffer is the one that would otherwise
    /// keep drawing into the old dimensions.
    /// </summary>
    internal void OnSwapchainResized(int width, int height)
    {
        if (_target is null)
        {
            _targetWidth = width;
            _targetHeight = height;
            if (!_hasViewport && !_hasScissor)
                ApplyViewportAndScissor();
        }
    }

    /// <summary>
    /// Drops the per-frame state. Called by the device when the command buffer is submitted.
    ///
    /// The uniform bytes are zeroed rather than kept: the next frame's first pipeline may be a
    /// different shader entirely, and a uniform it never writes must not inherit a value from an
    /// unrelated one. The GPU buffer itself is kept, so a frame that binds the same pipeline as the
    /// last one allocates nothing.
    /// </summary>
    internal void Reset()
    {
        _pipeline = null;

        // Forced, so the next frame's first bind applies in full. Whatever else touched the context
        // between frames - another renderer, a resize, the UI path - is reason enough not to trust a
        // decision the previous frame's command buffer made.
        _pipelineStateLostToClear = true;

        _indexBuffer = null;
        _indexOffsetBytes = 0;
        Array.Clear(_vertexStreams);

        // The viewport and scissor are deliberately kept, as they are on the GL backends: they are
        // caller state, not per-frame state, and whoever shares this context is expected to set its
        // own - GL has the same property, where the viewport survives a command buffer's reset
        // because nothing but a caller ever changes it.

        // The bound views are released here and the new frame rebinds and re-applies everything, so a
        // caller that drops its texture at the end of a frame is not held alive through this command
        // buffer. The frame is over and every draw that referenced them has been executed.
        for (var slot = 0; slot < ResourceSlots; slot++)
        {
            D3D11Interop.Release(ref _resources[slot]);
            D3D11Interop.Release(ref _samplers[slot]);
        }
    }

    internal void Dispose()
    {
        Reset();
        D3D11Interop.Release(ref _depthClearState);
        D3D11Interop.Release(ref _uniformBuffer);
    }
}
