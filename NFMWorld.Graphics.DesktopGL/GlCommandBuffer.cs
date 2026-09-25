// LLM maintained.
//
// The command buffer. GL is immediate-mode, so every method here issues its GL call straight away
// rather than recording anything - the abstraction's one-live-buffer rule exists precisely so this
// is a legal implementation, and it is why Submit has almost nothing to do.
//
// The one piece of real bookkeeping is the uniform block: the abstraction addresses uniforms by
// byte offset into the merged block (D3D packoffset numbering), while GL has a std140 UBO, so the
// buffer has to keep the block's bytes on the CPU, let SetUniform poke at them, and upload the
// whole block before a draw. That is the same model sokol's backend uses, and for the same reason.
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace NFMWorld.Graphics.DesktopGL;

internal sealed class GlCommandBuffer : ICommandBuffer
{
    private readonly GL _gl;
    private readonly GlGraphicsDevice _device;

    private GlPipelineState? _pipeline;

    /// <summary>
    /// The merged uniform block's bytes, sized to the pipeline's reflection. Reused across draws
    /// and reallocated only when a pipeline with a different block size is bound.
    /// </summary>
    private byte[] _uniforms = [];

    /// <summary>The UBO the block is uploaded through, created lazily on the first draw.</summary>
    private uint _uniformBuffer;

    /// <summary>The width/height of the current target, needed to flip Y for the viewport and scissor.</summary>
    private int _targetHeight;

    private readonly GlVertexStream[] _vertexStreams = new GlVertexStream[MaxVertexStreams];
    private GlBuffer? _indexBuffer;
    private int _indexOffsetBytes;

    /// <summary>
    /// What <see cref="BindVertexStreams"/> last pointed each VAO's attributes at.
    ///
    /// The backend re-points every attribute of the pipeline on every draw because ES 3.0's
    /// <c>glVertexAttribPointer</c> takes the buffer currently bound rather than the buffer it should
    /// use (ES 3.1's <c>glBindVertexBuffer</c> is the call that separates the two, and ANGLE's ES 3.0
    /// context rejects it). But the state it recomputes is identical to the previous draw's whenever
    /// the same pipeline is drawn from the same buffers again, which is the common case within a
    /// render element's batch.
    ///
    /// Keyed by VAO because attribute pointers and divisors are VAO state: each pipeline owns its own
    /// VAO, and a VAO other than the one bound when these were recorded has its own, unrelated,
    /// bindings. One entry per pipeline, so the table is bounded by the pipeline count and costs
    /// nothing to keep.
    /// </summary>
    private readonly Dictionary<uint, VertexBindingCache> _bindingCaches = [];

    /// <summary>One VAO's last-recorded attribute bindings, in attribute order.</summary>
    private sealed class VertexBindingCache
    {
        internal CachedBinding[] Bindings = [];
        internal int Count;
    }

    /// <summary>What one attribute was last pointed at. Everything the pointer and divisor calls derive.</summary>
    private readonly record struct CachedBinding(
        GlBuffer? Buffer, int StrideBytes, int OffsetBytes, int InstanceStepRate);

    /// <summary>
    /// The abstraction allows as many vertex streams as the pipeline declares. The app's instanced
    /// layouts use two (geometry + per-instance), so a small fixed array covers every real caller
    /// while keeping the state a plain stack value.
    /// </summary>
    private const int MaxVertexStreams = 4;

    private readonly struct GlVertexStream
    {
        internal GlBuffer? Buffer { get; init; }
        internal int StrideBytes { get; init; }
        internal int OffsetBytes { get; init; }
    }

    internal GlCommandBuffer(GL gl, GlGraphicsDevice device)
    {
        _gl = gl;
        _device = device;
        _targetHeight = device.Swapchain.Height;
    }

    public void SetPipeline(IPipelineState pipeline)
    {
        var glPipeline = (GlPipelineState)pipeline;
        _pipeline = glPipeline;

        // Sized from the reflection's offsets, not from the uniform count: the block is a byte
        // range, and two shaders can declare the same number of uniforms at different offsets.
        // Reallocating drops any previously written value, which is the point - a uniform the new
        // pipeline never writes must not inherit bytes from an unrelated shader.
        var size = ComputeBlockSize(glPipeline);
        if (_uniforms.Length != size)
            _uniforms = new byte[size];

        glPipeline.Apply(_gl);
    }

    /// <summary>
    /// The uniform block's size: the highest offset+size any reflected uniform reaches, rounded up
    /// to 16 bytes.
    ///
    /// The 16-byte rounding is not cosmetic - it matches the constant-buffer alignment D3D11
    /// requires and that the generated reflection was laid out against, and a UBO bound with a size
    /// that is not a multiple of 16 is rejected on some drivers. The driver's own size is checked
    /// against this in the smoke test; see <see cref="GlShaderProgram.UniformBlock"/>.
    /// </summary>
    private static int ComputeBlockSize(GlPipelineState pipeline)
    {
        var end = 0;
        foreach (var uniform in pipeline.Reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);
        return (end + 15) & ~15;
    }

    /// <summary>
    /// Writes a uniform into the block at the caller's byte offset.
    ///
    /// The abstraction's slot is the offset the generated bundle carries (D3D packoffset numbering,
    /// verified to agree with std140 for this shader set), and the upload is the whole block, so a
    /// partial write here is safe: the untouched bytes keep their previous values and the block is
    /// only ever uploaded as a unit.
    ///
    /// Matrix4x4 values are transposed on the way in, and that is not a detail of this backend - it
    /// is what the abstraction's contract already required of its command buffers. Callers hold
    /// System.Numerics/XNA matrices row-major; GL (like HLSL's constant registers, which is what the
    /// generated reflection's offsets were laid out against) wants them column-major. FNA3D's
    /// backend did this transpose inside its own SetUniform - the comment there is explicit that
    /// callers hand it row-major matrices - and this backend was written without it.
    ///
    /// The consequence of omitting it is not a subtle skew: a translation lives in M41/M42/M43, which
    /// under the shaders' row-vector convention (mul(float4(p, 1), M)) has to occupy the GPU's fourth
    /// *column*. Written unconverted it lands in the fourth *row* instead, where the shader's
    /// multiply reads it as the w component - so a translation becomes a perspective divide and the
    /// geometry explodes rather than moving. Identity matrices are symmetric, so every unrotated,
    /// untranslated object still looked right, which is why this survived until now.
    /// </summary>
    public void SetUniform(int slot, ReadOnlySpan<byte> value)
    {
        var pipeline = RequirePipeline();
        if (slot < 0 || slot + value.Length > _uniforms.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot),
                $"Uniform write of {value.Length} byte(s) at offset {slot} does not fit the " +
                $"{_uniforms.Length}-byte block '{pipeline.Desc.VertexShader.GetType().Name}' declares.");
        }

        // The reflection decides, not the byte count: a 64-byte write is only a matrix because the
        // bundle says so, and transposing something the shader reads as four vec4s would corrupt it.
        // The type comes from the reflection at this slot's own offset rather than from the slot
        // index, because the slot is a byte offset into the block and the uniform list is ordered by
        // name - the two are not the same numbering.
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
    /// Matched on the offset because that is what a caller's slot actually is. Two uniforms cannot
    /// share an offset in a block this backend sized from the same offsets, so the first match is the
    /// only match, and the loop is over a list of a few dozen entries on a path that already walks
    /// one to compute the block size.
    /// </summary>
    private static bool IsMatrix4x4At(GlPipelineState pipeline, int offset)
    {
        foreach (var uniform in pipeline.Reflection.Uniforms)
        {
            if (uniform.Offset == offset)
                return uniform.Type == Shaders.UniformType.Matrix4x4;
        }

        return false;
    }

    /// <summary>
    /// Writes a row-major 4x4 as the column-major bytes GL and HLSL constant registers expect.
    ///
    /// Done as an in-place index permutation rather than through <c>Matrix4x4.Transpose</c> so that
    /// the value's interpretation never depends on how the caller's <c>Span&lt;byte&gt;</c> happens to
    /// be aligned. The abstraction's contract is bytes, and reinterpreting them as a struct to
    /// transpose would make alignment a correctness requirement that nothing states.
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

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0)
    {
        if ((uint)slot >= MaxVertexStreams)
            throw new ArgumentOutOfRangeException(nameof(slot), $"Vertex stream slot {slot} exceeds the {MaxVertexStreams} this backend tracks.");

        _vertexStreams[slot] = new GlVertexStream
        {
            Buffer = (GlBuffer)buffer,
            StrideBytes = strideBytes,
            OffsetBytes = offsetBytes,
        };
    }

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0)
    {
        _indexBuffer = (GlBuffer)buffer;
        _indexOffsetBytes = offsetBytes;
    }

    /// <summary>
    /// Binds a texture and its sampler to a texture unit.
    ///
    /// The unit is the abstraction's slot, which is the SPIR-V binding the shader compiler assigned
    /// and which the ES GLSL's sampler uniforms read from. The sampler uniform itself has to be set
    /// to that unit number, and it is set here rather than at pipeline creation because GL requires
    /// the program to be current - which it is not until a pipeline is bound.
    /// </summary>
    public void SetShaderResource(int slot, ITexture texture, ISampler sampler)
    {
        var pipeline = RequirePipeline();
        var glTexture = (GlTexture)texture;
        var glSampler = (GlSampler)sampler;

        // TextureUnit's members are consecutive from GL_TEXTURE0, so the abstract slot is a
        // plain offset into the enum's range.
        var unit = (TextureUnit)((int)TextureUnit.Texture0 + slot);
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, glTexture.Handle);
        _gl.BindSampler((uint)slot, glSampler.Handle);

        // Point the shader's sampler uniform at the unit. The location comes from the reflection's
        // name, which only resolves because the shader compiler names the combined sampler after
        // the texture - see NameCombinedSamplers.
        if (slot < pipeline.Program.TextureLocations.Count)
        {
            var location = pipeline.Program.TextureLocations[slot];
            if (location >= 0)
                _gl.Uniform1(location, slot);
        }
    }

    public void SetRenderTarget(IRenderTarget? target)
    {
        var glTarget = (GlRenderTarget?)target;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, glTarget?.Handle ?? 0);

        // Only the height is kept: the viewport and scissor both have to be flipped into GL's
        // bottom-left origin, which needs to know how tall the surface currently being drawn into.
        _targetHeight = glTarget?.ColorTexture.Height ?? _device.Swapchain.Height;

        // Without this a shader writing to attachment 0 of a non-zero FBO writes nowhere, which is
        // the single most common cause of a black off-screen target.
        //
        // Deliberately only for a real target. On the default framebuffer there is no attachment 0
        // to name - its draw buffer is GL_BACK, and ES 3.0 rejects GL_COLOR_ATTACHMENT0 there with
        // GL_INVALID_OPERATION. The default framebuffer keeps its own draw buffer across a rebind,
        // so skipping the call is enough to restore it.
        if (glTarget is not null)
            _gl.DrawBuffers([GLEnum.ColorAttachment0]);
    }

    /// <summary>
    /// Sets the viewport, flipping Y.
    ///
    /// D3D's viewport origin is the top-left of the render target; GL's is the bottom-left. The
    /// abstraction follows D3D (it was designed against FNA3D), and the ES GLSL's clip space is
    /// D3D's, so a NDC-to-window mapping that matched GL's default would render the scene upside
    /// down. Flipping here - rather than negating Y in every vertex shader, which is what the
    /// common GL-on-Windows workaround does - keeps the shader sources shared with the D3D and
    /// Metal paths.
    /// </summary>
    public void SetViewport(Viewport viewport)
    {
        var y = _targetHeight - (viewport.Y + viewport.Height);
        _gl.Viewport((int)viewport.X, (int)y, (uint)viewport.Width, (uint)viewport.Height);
        // The abstraction's depth range is D3D's [0, 1] convention; GL's default is [-1, 1]. Since
        // the shader emits D3D-style clip space (spirv-cross does not rewrite Z), the range is set
        // to match rather than relied on.
        _gl.DepthRange(viewport.MinDepth, viewport.MaxDepth);
    }

    /// <summary>
    /// Sets the scissor rectangle. Like the viewport, it is flipped into GL's bottom-left origin,
    /// because the D3D convention is top-left.
    /// </summary>
    public void SetScissorRect(ScissorRect rect)
    {
        var y = _targetHeight - (rect.Y + rect.Height);
        _gl.Scissor(rect.X, y, (uint)rect.Width, (uint)rect.Height);
    }

    public void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        // Whether each aspect is *writable* is pipeline state in GL, not a clear parameter, so a
        // colour write mask of none would make a colour clear a silent no-op. The abstraction
        // treats Clear as unconditional, so the writes are forced on around it and the pipeline
        // re-applies its own state on the next SetPipeline - which is why nothing is saved and
        // restored here. The alternative, reading back each mask with glGet*, is a synchronous
        // driver round trip on a path the renderer hits every frame.
        _gl.ColorMask(true, true, true, true);
        _gl.DepthMask(true);
        _gl.StencilMask(uint.MaxValue);

        if (options.HasFlag(ClearOptions.Color))
            _gl.ClearColor(color.R, color.G, color.B, color.A);
        if (options.HasFlag(ClearOptions.Depth))
            _gl.ClearDepth(depth);
        if (options.HasFlag(ClearOptions.Stencil))
            _gl.ClearStencil(stencil);

        var bits = ClearBufferMask.None;
        if (options.HasFlag(ClearOptions.Color)) bits |= ClearBufferMask.ColorBufferBit;
        if (options.HasFlag(ClearOptions.Depth)) bits |= ClearBufferMask.DepthBufferBit;
        if (options.HasFlag(ClearOptions.Stencil)) bits |= ClearBufferMask.StencilBufferBit;
        if (bits != ClearBufferMask.None)
            _gl.Clear(bits);
    }

    public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0) =>
        ((GlBuffer)buffer).Update(_gl, data, offsetBytes);

    public void UpdateTexture(ITexture texture, int x, int y, int width, int height, ReadOnlySpan<byte> data) =>
        ((GlTexture)texture).Update(_gl, x, y, width, height, data);

    public void Draw(int startVertex, int primitiveCount) =>
        DrawInternal(baseVertex: 0, startVertex, primitiveCount, 1, indexed: false);

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount) =>
        DrawInternal(baseVertex, startIndex, primitiveCount, 1, indexed: true);

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount) =>
        DrawInternal(baseVertex, startIndex, primitiveCount, instanceCount, indexed: true);

    /// <summary>
    /// The one draw path. Everything a draw needs - the VAO's bindings, the uniform block, and the
    /// element count - is assembled here so the three public entry points cannot drift.
    /// </summary>
    private void DrawInternal(int baseVertex, int firstIndex, int primitiveCount, int instanceCount, bool indexed)
    {
        var pipeline = RequirePipeline();

        UploadUniforms(pipeline);
        BindVertexStreams(pipeline);

        var mode = pipeline.Desc.Topology.ToPrimitiveType();
        var count = (uint)ElementCount(pipeline.Desc.Topology, primitiveCount);

        if (!indexed)
        {
            if (instanceCount == 1)
                _gl.DrawArrays(mode, firstIndex, count);
            else
                _gl.DrawArraysInstanced(mode, firstIndex, count, (uint)instanceCount);
            return;
        }

        var indexBuffer = _indexBuffer
            ?? throw new InvalidOperationException($"DrawIndexed requires {nameof(SetIndexBuffer)} to have been called.");

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer.Handle);
        var indexType = indexBuffer.IndexFormat.ToIndexType();
        var byteOffset = _indexOffsetBytes + firstIndex * IndexSize(indexBuffer.IndexFormat);

        // The element pointer is a byte offset into the bound index buffer, which is why it is
        // computed by hand rather than passed as an index - GL has no separate index operand.
        unsafe
        {
            var pointer = (void*)byteOffset;

            // The base-vertex draw is core desktop GL from 3.2, which is below this backend's 3.3
            // floor, so unlike the ANGLE backend there is no extension to resolve and no
            // configuration in which the offset cannot be honoured. That is the whole reason this
            // backend needs no GlBaseVertexDraw equivalent: the call the ANGLE path had to look up
            // through GL_EXT_draw_elements_base_vertex is plain core API here.
            if (instanceCount == 1)
                _gl.DrawElementsBaseVertex(mode, count, indexType, pointer, baseVertex);
            else
                _gl.DrawElementsInstancedBaseVertex(mode, count, indexType, pointer, (uint)instanceCount, baseVertex);
        }
    }

    private static int IndexSize(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => 2,
        IndexFormat.UInt32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Points the VAO's attributes at the buffers their streams named, if they are not already.
    ///
    /// This is where the vertex binding actually happens, and it is per draw rather than per
    /// pipeline because ES 3.0's <c>glVertexAttribPointer</c> records the buffer that is bound at
    /// the moment it is called. The separation the abstraction assumes - a layout fixed at pipeline
    /// creation, buffers supplied per draw - is only expressible in ES 3.1's
    /// <c>glBindVertexBuffer</c>, which this context does not have; see GlPipelineState.
    ///
    /// Everything the calls below derive is a function of the buffer handles, the strides, the
    /// offsets and the pipeline's attribute list, so the result is compared against the last state
    /// this command buffer set up and reissued only when it differs. Across a frame's instanced
    /// draws the common case is that it does not: one render element's draws repeat the same
    /// pipeline and the same two buffers, so the whole loop is skipped and only the VAO bind is left.
    /// </summary>
    private void BindVertexStreams(GlPipelineState pipeline)
    {
        _gl.BindVertexArray(pipeline.VertexArray);

        if (!_bindingCaches.TryGetValue(pipeline.VertexArray, out var cache))
        {
            cache = new VertexBindingCache();
            _bindingCaches[pipeline.VertexArray] = cache;
        }

        if (cache.Bindings.Length < pipeline.Attributes.Count)
            cache.Bindings = new CachedBinding[pipeline.Attributes.Count];

        // Grouped by stream so each buffer is bound once for all of its attributes rather than once
        // per attribute - the common single-stream case is then a single glBindBuffer.
        GlBuffer? boundBuffer = null;
        var boundSlot = -1;

        for (var i = 0; i < pipeline.Attributes.Count; i++)
        {
            var attribute = pipeline.Attributes[i];
            var stream = _vertexStreams[attribute.VertexSlot];
            var buffer = stream.Buffer
                ?? throw new InvalidOperationException(
                    $"The pipeline declares vertex layout slot {attribute.VertexSlot} but " +
                    $"{nameof(SetVertexBuffer)} was never called for it.");

            var current = new CachedBinding(buffer, stream.StrideBytes, stream.OffsetBytes, attribute.InstanceStepRate);
            if (i < cache.Count && cache.Bindings[i] == current)
                continue;

            if (attribute.VertexSlot != boundSlot)
            {
                boundSlot = attribute.VertexSlot;
                boundBuffer = buffer;
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, boundBuffer.Handle);
            }

            // The divisor is per *attribute* in ES 3.0 rather than per stream, which is what the
            // abstraction's per-stream InstanceStepRate becomes once each attribute of a stream is
            // given it. Zero means "advance per vertex", one means "per instance".
            _gl.VertexAttribDivisor(attribute.Location, (uint)attribute.InstanceStepRate);

            // The offset is a byte offset into the bound buffer - not an index - which is why the
            // stream's own offset is added to the attribute's. The IntPtr overload is used rather
            // than the raw-pointer one so this stays in safe code: nothing is dereferenced here,
            // the value is just carried to the driver as the start of the attribute's data.
            _gl.VertexAttribPointer(
                attribute.Location, attribute.Components, attribute.Type, attribute.Normalized,
                (uint)stream.StrideBytes,
                (nint)(stream.OffsetBytes + attribute.OffsetInBytes));
            _gl.EnableVertexAttribArray(attribute.Location);

            cache.Bindings[i] = current;
            cache.Count = Math.Max(cache.Count, i + 1);
        }
    }

    /// <summary>
    /// Uploads the block as a whole UBO. The buffer is created on first use and resized
    /// (reallocated) when a pipeline with a different block size binds, which is rare enough that
    /// keeping a pool would be premature.
    ///
    /// The orphan-then-fill below looks like waste - <c>BufferData</c> with no data discards the
    /// store and the driver allocates again - and a ring buffer of sub-ranges looks like the obvious
    /// way to remove it. It is not, and the attempt is worth recording so it is not made twice:
    /// writing into a sub-range of a buffer the GPU may still be reading makes ANGLE's D3D11 backend
    /// rename and copy the *entire* buffer, which cost ~10 ms per frame here against the ~0.35 ms
    /// this idiom spends on per-draw allocation. Orphaning wins precisely because the driver can hand
    /// out fresh storage and forget the old one instead of preserving it. A ring would need explicit
    /// sync (or a fence per range) to be safe, and even then only pays off when the per-draw
    /// allocation itself shows up as the cost, which measurement said it is not.
    ///
    /// The trailing <c>UseProgram</c> stays for the same reason it was written: it is one call, and
    /// "nothing rebinds between SetPipeline and the draw" is an invariant about *today's* callers
    /// that a lower layer cannot enforce.
    /// </summary>
    private void UploadUniforms(GlPipelineState pipeline)
    {
        var block = pipeline.Program.UniformBlock;
        if (block is null || _uniforms.Length == 0)
            return;

        if (_uniformBuffer == 0)
            _uniformBuffer = _gl.GenBuffer();

        _gl.BindBuffer(BufferTargetARB.UniformBuffer, _uniformBuffer);
        _gl.BufferData(BufferTargetARB.UniformBuffer, (nuint)_uniforms.Length, ReadOnlySpan<byte>.Empty, BufferUsageARB.DynamicDraw);
        _gl.BufferSubData(BufferTargetARB.UniformBuffer, 0, _uniforms);
        _gl.BindBufferBase(BufferTargetARB.UniformBuffer, block.Binding, _uniformBuffer);
        _gl.UseProgram(pipeline.Program.Handle);
    }

    /// <summary>
    /// Converts the abstraction's primitive count (matching FNA3D's <c>DrawPrimitives</c>) into
    /// GL's element count. A strip consumes one extra element for its first primitive, so it cannot
    /// use the list multiplier - the same rule sokol's backend applies.
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

    private GlPipelineState RequirePipeline() =>
        _pipeline ?? throw new InvalidOperationException($"{nameof(SetPipeline)} must be called before issuing draw work.");

    /// <summary>
    /// Drops the per-frame state. Called by the device when the command buffer is submitted.
    ///
    /// The binding caches are deliberately *not* cleared. They describe GL state that outlives the
    /// command buffer - the VAOs and their recorded attribute pointers persist in the context across
    /// frames - so dropping them would only force the first draw of every frame to re-point
    /// everything, which is work with no correctness value.
    /// </summary>
    internal void Reset()
    {
        _pipeline = null;
        _indexBuffer = null;
        _indexOffsetBytes = 0;
        Array.Clear(_vertexStreams);
    }
}
