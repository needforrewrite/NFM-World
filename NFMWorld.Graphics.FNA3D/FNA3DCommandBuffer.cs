using System.Numerics;
using System.Runtime.InteropServices;
using NFMWorld.Shaders;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Forwards every call straight to FNA3D's native functions the instant it's made - FNA3D
/// itself is an immediate-mode device, so there is nothing to record/replay here. This is why
/// <see cref="FNA3DGraphicsDevice.Submit"/> can be a no-op for this backend, and why
/// <see cref="UpdateBuffer"/> needs no extra CPU-side copy: the caller's span is pinned and
/// passed straight through to FNA3D_Set*BufferData, which performs the one unavoidable
/// CPU-to-GPU copy itself.
/// </summary>
internal sealed class FNA3DCommandBuffer : ICommandBuffer
{
    // FNA3D_ApplyEffect writes into this every call to report render/sampler state changes the
    // Effect's technique pass made (mirrors FNA's own GraphicsDevice.effectStateChangesPtr) - it
    // must point at a valid, non-null MOJOSHADER_effectStateChanges-sized buffer or FNA3D
    // segfaults trying to write its output fields. Nothing reads this back yet (no shader ported
    // so far uses Effect-driven render states), so one process-lifetime buffer shared by every
    // command buffer is enough - only one is ever live at a time by design.
    private static readonly IntPtr StateChangesBuffer = AllocateZeroedStateChanges();

    private static IntPtr AllocateZeroedStateChanges()
    {
        const int size = 64; // MOJOSHADER_effectStateChanges is 3x (uint + pointer) with padding; rounded up for safety.
        var ptr = Marshal.AllocHGlobal(size);
        for (var i = 0; i < size; i++) Marshal.WriteByte(ptr, i, 0);
        return ptr;
    }

    private readonly IntPtr _device;
    private FNA3DPipelineState? _pipeline;
    private IntPtr[] _vertexBufferHandles = [];
    private int[] _vertexOffsets = [];
    private IntPtr _indexBufferHandle;
    private FNA3D_IndexElementSize _indexFormat;

    public FNA3DCommandBuffer(IntPtr device) => _device = device;

    public void SetPipeline(IPipelineState pipeline)
    {
        var fna = (FNA3DPipelineState)pipeline;
        _pipeline = fna;
        _vertexBufferHandles = new IntPtr[fna.VertexDeclarations.Length];
        _vertexOffsets = new int[fna.VertexDeclarations.Length];

        var blend = fna.BlendState;
        var depth = fna.DepthStencilState;
        var raster = fna.RasterizerState;
        FNA3D_SetBlendState(_device, ref blend);
        FNA3D_SetDepthStencilState(_device, ref depth);
        FNA3D_ApplyRasterizerState(_device, ref raster);
    }

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0)
    {
        var fna = (FNA3DBuffer)buffer;
        _vertexBufferHandles[slot] = fna.Handle;
        _vertexOffsets[slot] = offsetBytes;
    }

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0)
    {
        var fna = (FNA3DBuffer)buffer;
        _indexBufferHandle = fna.Handle;
        _indexFormat = Mapping.ToNative(fna.IndexFormat);
        // offsetBytes: FNA3D_DrawIndexedPrimitives takes startIndex, not a byte offset, so a
        // non-zero index-buffer offset is folded into startIndex by the caller at draw time.
        if (offsetBytes != 0)
            throw new NotSupportedException("Non-zero index buffer offsets aren't supported by this backend yet - fold the offset into DrawIndexed's startIndex instead.");
    }

    public void SetShaderResource(int slot, ITexture texture, ISampler sampler) =>
        throw new NotImplementedException("No shader ported so far binds a texture - implemented alongside the first one that does (Milestone 5).");

    public unsafe void SetUniform(int slot, ReadOnlySpan<byte> value)
    {
        if (_pipeline is null)
            throw new InvalidOperationException($"{nameof(SetUniform)} requires a pipeline to be bound first.");

        var uniform = _pipeline.Reflection.Uniforms[slot];
        var destination = (float*)_pipeline.UniformValuePointers[slot];

        // HLSL packs matrix parameters into constant registers column-major, while callers hold
        // their matrices row-major (System.Numerics/XNA convention). FNA's own
        // EffectParameter.SetValue(Matrix) bridges that by writing M11, M21, M31, M41, ... - i.e.
        // storing the transpose - so this does the same, and ported game code can keep setting
        // matrices exactly as it did before, with no .Transpose() sprinkled at call sites.
        if (uniform.Type == UniformType.Matrix4x4)
        {
            if (value.Length < 16 * sizeof(float))
                throw new ArgumentException($"{uniform.Name} is a Matrix4x4 uniform - expected at least 64 bytes, got {value.Length}.", nameof(value));

            var source = MemoryMarshal.Cast<byte, float>(value);
            for (var column = 0; column < 4; column++)
            for (var row = 0; row < 4; row++)
                destination[column * 4 + row] = source[row * 4 + column];
            return;
        }

        value.CopyTo(new Span<byte>(destination, value.Length));
    }

    public unsafe void SetRenderTarget(IRenderTarget? target)
    {
        if (target is null)
        {
            // Null/zero-count targets the backbuffer, per FNA3D_SetRenderTargets semantics.
            FNA3D_SetRenderTargets(_device, null, 0, IntPtr.Zero, FNA3D_DepthFormat.None, 0);
            return;
        }
        throw new NotImplementedException("Off-screen render target binding is implemented alongside shadow-cascade rendering in Milestone 5.");
    }

    public void SetViewport(Viewport viewport)
    {
        var native = new FNA3D_Viewport
        {
            x = (int)viewport.X,
            y = (int)viewport.Y,
            w = (int)viewport.Width,
            h = (int)viewport.Height,
            minDepth = viewport.MinDepth,
            maxDepth = viewport.MaxDepth,
        };
        FNA3D_SetViewport(_device, ref native);
    }

    public unsafe void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0)
    {
        var fna = (FNA3DBuffer)buffer;
        var options = fna.Usage == BufferUsage.Dynamic ? FNA3D_SetDataOptions.Discard : FNA3D_SetDataOptions.None;
        fixed (byte* ptr = data)
        {
            if (fna.Kind == BufferKind.Vertex)
            {
                // elementCount=byteLength, elementSizeInBytes=1, vertexStride=1 is FNA's own
                // convention for a raw untyped copy (see GraphicsDevice.PrepareUserVertexBuffer).
                FNA3D_SetVertexBufferData(_device, fna.Handle, offsetBytes, (IntPtr)ptr, data.Length, 1, 1, options);
            }
            else
            {
                FNA3D_SetIndexBufferData(_device, fna.Handle, offsetBytes, (IntPtr)ptr, data.Length, options);
            }
        }
    }

    public void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        var native = new Vector4 { X = color.R, Y = color.G, Z = color.B, W = color.A };
        FNA3D_Clear(_device, Mapping.ToNative(options), ref native, depth, stencil);
    }

    public void Draw(int startVertex, int primitiveCount)
    {
        ApplyVertexBuffersAndEffect(baseVertex: 0);
        FNA3D_DrawPrimitives(_device, Mapping.ToNative(RequirePipeline().Desc.Topology), startVertex, primitiveCount);
    }

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount)
    {
        ApplyVertexBuffersAndEffect(baseVertex);
        var pipeline = RequirePipeline();
        FNA3D_DrawIndexedPrimitives(
            _device, Mapping.ToNative(pipeline.Desc.Topology), baseVertex,
            minVertexIndex: 0, numVertices: 0, startIndex, primitiveCount,
            _indexBufferHandle, _indexFormat);
    }

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount)
    {
        ApplyVertexBuffersAndEffect(baseVertex);
        var pipeline = RequirePipeline();
        FNA3D_DrawInstancedPrimitives(
            _device, Mapping.ToNative(pipeline.Desc.Topology), baseVertex,
            minVertexIndex: 0, numVertices: 0, startIndex, primitiveCount, instanceCount,
            _indexBufferHandle, _indexFormat);
    }

    private FNA3DPipelineState RequirePipeline() =>
        _pipeline ?? throw new InvalidOperationException($"{nameof(SetPipeline)} must be called before issuing a draw call.");

    /// <summary>
    /// Applies the currently bound vertex streams and Effect pass right before a draw - Effect
    /// application has to happen after every SetUniform call for this draw, and before the draw
    /// itself, per FNA3D_ApplyEffect's contract.
    /// </summary>
    private unsafe void ApplyVertexBuffersAndEffect(int baseVertex)
    {
        var pipeline = RequirePipeline();

        // FNA3D_ApplyEffect must run FIRST: D3D11's ApplyVertexBufferBindings builds its input
        // layout by querying the vertex shader currently bound to the native shader context
        // (MOJOSHADER_d3d11GetBoundShaders), which ApplyEffect is what actually binds - calling
        // these in the opposite order (as FNA itself never does - Effect.Apply() always precedes
        // GraphicsDevice.DrawIndexedPrimitives()) leaves that shader pointer null/stale and
        // segfaults on the D3D11/OpenGL drivers the first time a pipeline is applied.
        FNA3D_ApplyEffect(_device, pipeline.EffectHandle, pass: 0, stateChanges: StateChangesBuffer);

        var bindings = stackalloc FNA3D_VertexBufferBinding[pipeline.VertexDeclarations.Length];
        for (var slot = 0; slot < pipeline.VertexDeclarations.Length; slot++)
        {
            bindings[slot] = new FNA3D_VertexBufferBinding
            {
                vertexBuffer = _vertexBufferHandles[slot],
                vertexDeclaration = pipeline.VertexDeclarations[slot],
                vertexOffset = _vertexOffsets[slot],
                instanceFrequency = pipeline.InstanceFrequencies[slot],
            };
        }
        FNA3D_ApplyVertexBufferBindings(_device, bindings, pipeline.VertexDeclarations.Length, bindingsUpdated: 1, baseVertex);
    }
}
