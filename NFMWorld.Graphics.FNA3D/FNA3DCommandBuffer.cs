using NFMWorld.Graphics.FNA3D.Native;

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
    private readonly IntPtr _device;

    public FNA3DCommandBuffer(IntPtr device) => _device = device;

    public void SetPipeline(IPipelineState pipeline)
    {
        var fna = (FNA3DPipelineState)pipeline;
        var blend = fna.BlendState;
        var depth = fna.DepthStencilState;
        var raster = fna.RasterizerState;
        FNA3DNative.FNA3D_SetBlendState(_device, ref blend);
        FNA3DNative.FNA3D_SetDepthStencilState(_device, ref depth);
        FNA3DNative.FNA3D_ApplyRasterizerState(_device, ref raster);
        // Effect application (FNA3D_ApplyEffect) requires the shader/Effect-blob design decided
        // in Milestone 3 - not wired up yet.
    }

    public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0) =>
        throw new NotImplementedException("Requires FNA3D_ApplyVertexBufferBindings wiring - implemented alongside pipeline/shader support in Milestone 3.");

    public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0) =>
        throw new NotImplementedException("Requires draw-call wiring - implemented alongside pipeline/shader support in Milestone 3.");

    public void SetShaderResource(int slot, ITexture texture, ISampler sampler) =>
        throw new NotImplementedException("Requires FNA3D_VerifySampler wiring - implemented alongside pipeline/shader support in Milestone 3.");

    public void SetUniform(int slot, ReadOnlySpan<byte> value) =>
        throw new NotImplementedException("Requires the Effect-parameter mapping decided in Milestone 3.");

    public unsafe void SetRenderTarget(IRenderTarget? target)
    {
        if (target is null)
        {
            // Null/zero-count targets the backbuffer, per FNA3D_SetRenderTargets semantics.
            FNA3DNative.FNA3D_SetRenderTargets(_device, null, 0, IntPtr.Zero, FNA3D_DepthFormat.None, 0);
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
        FNA3DNative.FNA3D_SetViewport(_device, ref native);
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
                FNA3DNative.FNA3D_SetVertexBufferData(_device, fna.Handle, offsetBytes, (IntPtr)ptr, data.Length, 1, 1, options);
            }
            else
            {
                FNA3DNative.FNA3D_SetIndexBufferData(_device, fna.Handle, offsetBytes, (IntPtr)ptr, data.Length, options);
            }
        }
    }

    public void Clear(ClearOptions options, ColorRgba color, float depth = 1f, int stencil = 0)
    {
        var native = new FNA3D_Vec4 { x = color.R, y = color.G, z = color.B, w = color.A };
        FNA3DNative.FNA3D_Clear(_device, Mapping.ToNative(options), ref native, depth, stencil);
    }

    public void Draw(int startVertex, int primitiveCount) =>
        throw new NotImplementedException("Requires pipeline/shader support - Milestone 3.");

    public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount) =>
        throw new NotImplementedException("Requires pipeline/shader support - Milestone 3.");

    public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount) =>
        throw new NotImplementedException("Requires pipeline/shader support - Milestone 3.");
}
