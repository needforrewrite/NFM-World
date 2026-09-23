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
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

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

        value.CopyTo(_uniforms.AsSpan(slot));
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

        // ES 3.0 core has no base-vertex draw (glDrawElementsBaseVertex is an extension on GLES; on
        // ANGLE it is not exposed at all), so there is no way to add baseVertex to each index. The
        // common case - a non-zero base vertex used to reach a sub-range of a shared vertex buffer -
        // is not silently approximated here: the element pointer trick would only be equivalent for
        // a zero base vertex, and silently ignoring it would draw geometry from the wrong part of
        // the buffer, which reads as a scene bug rather than an API limitation.
        if (baseVertex != 0)
        {
            throw new NotSupportedException(
                $"OpenGL ES 3.0 has no base-vertex draw and ANGLE exposes no extension for one, so " +
                $"baseVertex {baseVertex} cannot be honoured. Draw from a buffer whose indices are " +
                "already relative to the intended base vertex.");
        }

        // The element pointer is a byte offset into the bound index buffer, which is why it is
        // computed by hand rather than passed as an index - GL has no separate index operand.
        unsafe
        {
            var pointer = (void*)byteOffset;
            if (instanceCount == 1)
                _gl.DrawElements(mode, count, indexType, pointer);
            else
                _gl.DrawElementsInstanced(mode, count, indexType, pointer, (uint)instanceCount);
        }
    }

    private static int IndexSize(IndexFormat format) => format switch
    {
        IndexFormat.UInt16 => 2,
        IndexFormat.UInt32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Points the VAO's attributes at the buffers their streams named.
    ///
    /// This is where the vertex binding actually happens, and it is per draw rather than per
    /// pipeline because ES 3.0's <c>glVertexAttribPointer</c> records the buffer that is bound at
    /// the moment it is called. The separation the abstraction assumes - a layout fixed at pipeline
    /// creation, buffers supplied per draw - is only expressible in ES 3.1's
    /// <c>glBindVertexBuffer</c>, which this context does not have; see GlPipelineState.
    ///
    /// Re-pointing the same attributes at the same buffers every draw is wasted work in principle
    /// and not worth avoiding in practice. The alternative is a cache keyed on the buffers, offsets
    /// and strides, which has to be invalidated whenever another pipeline binds a different VAO or
    /// a draw leaves the array-buffer binding somewhere else - state that GL keeps one copy of but
    /// two abstractions (this one and the caller's) both touch.
    /// </summary>
    private void BindVertexStreams(GlPipelineState pipeline)
    {
        _gl.BindVertexArray(pipeline.VertexArray);

        // Grouped by stream so each buffer is bound once for all of its attributes rather than once
        // per attribute - the common single-stream case is then a single glBindBuffer.
        GlBuffer? boundBuffer = null;
        var boundSlot = -1;

        foreach (var attribute in pipeline.Attributes)
        {
            if (attribute.VertexSlot != boundSlot)
            {
                boundSlot = attribute.VertexSlot;
                boundBuffer = _vertexStreams[boundSlot].Buffer
                    ?? throw new InvalidOperationException(
                        $"The pipeline declares vertex layout slot {boundSlot} but " +
                        $"{nameof(SetVertexBuffer)} was never called for it.");

                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, boundBuffer.Handle);
            }

            // The divisor is per *attribute* in ES 3.0 rather than per stream, which is what the
            // abstraction's per-stream InstanceStepRate becomes once each attribute of a stream is
            // given it. Zero means "advance per vertex", one means "per instance".
            var stream = _vertexStreams[boundSlot];
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
        }
    }

    /// <summary>
    /// Uploads the block as a whole UBO. The buffer is created on first use and resized
    /// (reallocated) when a pipeline with a different block size binds, which is rare enough that
    /// keeping a pool would be premature.
    /// </summary>
    private void UploadUniforms(GlPipelineState pipeline)
    {
        var block = pipeline.Program.UniformBlock;
        if (block is null || _uniforms.Length == 0)
            return;

        if (_uniformBuffer == 0)
            _uniformBuffer = _gl.GenBuffer();

        _gl.BindBuffer(BufferTargetARB.UniformBuffer, _uniformBuffer);
        // Orphan-then-fill: BufferData with no data discards the old storage, which avoids a stall
        // waiting for the previous frame's reads to finish. This is the standard dynamic-UBO idiom
        // and costs one allocation per draw.
        _gl.BufferData(BufferTargetARB.UniformBuffer, (nuint)_uniforms.Length, ReadOnlySpan<byte>.Empty, BufferUsageARB.DynamicDraw);
        _gl.BufferSubData(BufferTargetARB.UniformBuffer, 0, _uniforms);
        _gl.BindBufferBase(BufferTargetARB.UniformBuffer, block.Binding, _uniformBuffer);

        // Rebind to the pipeline in case another pipeline bound a different program in between.
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

    /// <summary>Releases the per-frame UBO. Called by the device when the command buffer is submitted.</summary>
    internal void Reset()
    {
        _pipeline = null;
        _indexBuffer = null;
        _indexOffsetBytes = 0;
        Array.Clear(_vertexStreams);
    }
}
