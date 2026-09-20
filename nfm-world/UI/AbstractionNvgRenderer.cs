using System.Drawing;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;
using NFMWorldLibrary;
using NvgSharp;

namespace NFMWorld;

/// <summary>
/// NanoVG's <see cref="INvgRenderer"/>, ported from the reference XNA implementation
/// (<c>NvgSharp/src/XNA/XNARenderer.cs</c>) against the new command-buffer graphics abstraction
/// instead of XNA's <c>GraphicsDevice</c>. Built against the <c>PLATFORM_AGNOSTIC</c> build of
/// NvgSharp (<c>NvgSharp.csproj</c>/<c>NvgSharp.Text.csproj</c>), where <c>Texture2D</c> is a plain
/// <see cref="object"/> (boxing whatever this renderer's <see cref="CreateTexture"/> returns - here,
/// an <see cref="ITexture"/>) and colors/rects are <see cref="System.Drawing"/> types rather than
/// XNA's.
///
/// NanoVG's fill algorithm needs a stencil-buffer pass and a color-write-masked pass that the
/// original renderer set per-draw via mutable <c>GraphicsDevice.BlendState</c>/<c>DepthStencilState</c>
/// assignment - this abstraction bakes that state into an <see cref="IPipelineState"/> at creation
/// time instead, and additionally bakes primitive topology per-pipeline (unlike XNA, where the same
/// technique/state combo can freely switch between <c>TriangleList</c> and <c>TriangleStrip</c> per
/// draw call). So instead of the original's 4 techniques x runtime state switching, this renderer
/// builds a fixed set of pipeline permutations up front, one per (technique, blend, stencil,
/// topology) combination NanoVG's own call sites actually use - see the <c>_p*</c> fields below.
/// </summary>
public sealed class AbstractionNvgRenderer : INvgRenderer, IDisposable
{
    private const int VertexStride = 16; // Vertex: float2 Position + float2 TextureCoordinate

    private static readonly VertexLayoutDesc VertexLayout = new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
        ],
        StrideInBytes: VertexStride);

    // NanoVG's two-sided stencil "fill1" pass: front faces increment, back faces decrement,
    // both always pass the stencil test (only the pass-op writes into the buffer).
    private static readonly DepthStencilStateDesc StencilFill1 = new(
        DepthTestEnabled: false, DepthWriteEnabled: false,
        StencilTestEnabled: true, TwoSidedStencil: true,
        StencilWriteMask: 0xff, StencilReadMask: 0xff, ReferenceStencil: 0,
        StencilFunction: CompareFunction.Always, StencilPass: StencilOperation.Increment,
        CcwStencilFunction: CompareFunction.Always, CcwStencilPass: StencilOperation.Decrement);

    // "fill2": draw only where stencil == 0 (i.e. NOT covered by the shape), used for the AA fringe.
    private static readonly DepthStencilStateDesc StencilFill2 = new(
        DepthTestEnabled: false, DepthWriteEnabled: false,
        StencilTestEnabled: true, TwoSidedStencil: false,
        StencilWriteMask: 0xff, StencilReadMask: 0xff, ReferenceStencil: 0,
        StencilFunction: CompareFunction.Equal);

    // "fill3": draw (and clear) only where stencil != 0 - the cleanup quad that also zeroes the buffer.
    private static readonly DepthStencilStateDesc StencilFill3 = new(
        DepthTestEnabled: false, DepthWriteEnabled: false,
        StencilTestEnabled: true, TwoSidedStencil: false,
        StencilWriteMask: 0xff, StencilReadMask: 0xff, ReferenceStencil: 0,
        StencilFunction: CompareFunction.NotEqual,
        StencilFail: StencilOperation.Zero, StencilDepthFail: StencilOperation.Zero, StencilPass: StencilOperation.Zero);

    private static readonly RasterizerStateDesc Rasterizer = RasterizerStateDesc.Default with { CullMode = CullMode.None };

    private readonly IGraphicsDevice _device;
    private readonly NvgEffect _effect;
    private readonly ISampler _sampler;

    private readonly struct Pipeline(IPipelineState state, NvgEffectParameters parameters)
    {
        public readonly IPipelineState State = state;
        public readonly NvgEffectParameters Parameters = parameters;
    }

    // See the class doc comment - one pipeline per (technique, blend, stencil, topology) combo
    // NanoVG's fill/stroke/triangles code paths actually exercise.
    private readonly Pipeline _pSimpleStencilFill1;      // Simple, no color write, stencil incr/decr, indexed (fan) list
    private readonly Pipeline _pGradientFringe;           // FillGradient, alpha blend, stencil==0, strip
    private readonly Pipeline _pImageFringe;               // FillImage, alpha blend, stencil==0, strip
    private readonly Pipeline _pGradientCleanup;           // FillGradient, alpha blend, stencil!=0 (clears), strip
    private readonly Pipeline _pGradientFillList;          // FillGradient, alpha blend, no stencil, indexed (fan) list
    private readonly Pipeline _pGradientStrip;             // FillGradient, alpha blend, no stencil, strip
    private readonly Pipeline _pImageFillList;             // FillImage, alpha blend, no stencil, indexed (fan) list
    private readonly Pipeline _pImageStrip;                // FillImage, alpha blend, no stencil, strip
    private readonly Pipeline _pTrianglesList;             // Triangles, alpha blend, no stencil, non-indexed list

    private IBuffer? _vertexBuffer;
    private int _vertexBufferCapacity;

    private IBuffer? _fanIndexBuffer;
    private int _fanIndexBufferCapacity; // in vertices (fan length), not index count

    private ICommandBuffer? _cb;
    private System.Numerics.Matrix4x4 _transform;

    public AbstractionNvgRenderer(IGraphicsDevice device)
    {
        _device = device;

        _effect = new NvgEffect(VFS.ReadAllBytes("./data/shaders/Nvg.fxb"));
        _sampler = device.CreateSampler(new SamplerDesc(Filter: TextureFilter.Linear, AddressU: TextureAddressMode.Clamp, AddressV: TextureAddressMode.Clamp));

        _pSimpleStencilFill1 = BuildPipeline("Simple", BlendStateDesc.ColorWriteNone, StencilFill1, PrimitiveTopology.TriangleList);
        _pGradientFringe = BuildPipeline("FillGradient", BlendStateDesc.AlphaBlend, StencilFill2, PrimitiveTopology.TriangleStrip);
        _pImageFringe = BuildPipeline("FillImage", BlendStateDesc.AlphaBlend, StencilFill2, PrimitiveTopology.TriangleStrip);
        _pGradientCleanup = BuildPipeline("FillGradient", BlendStateDesc.AlphaBlend, StencilFill3, PrimitiveTopology.TriangleStrip);
        _pGradientFillList = BuildPipeline("FillGradient", BlendStateDesc.AlphaBlend, DepthStencilStateDesc.None, PrimitiveTopology.TriangleList);
        _pGradientStrip = BuildPipeline("FillGradient", BlendStateDesc.AlphaBlend, DepthStencilStateDesc.None, PrimitiveTopology.TriangleStrip);
        _pImageFillList = BuildPipeline("FillImage", BlendStateDesc.AlphaBlend, DepthStencilStateDesc.None, PrimitiveTopology.TriangleList);
        _pImageStrip = BuildPipeline("FillImage", BlendStateDesc.AlphaBlend, DepthStencilStateDesc.None, PrimitiveTopology.TriangleStrip);
        _pTrianglesList = BuildPipeline("Triangles", BlendStateDesc.AlphaBlend, DepthStencilStateDesc.None, PrimitiveTopology.TriangleList);
    }

    private Pipeline BuildPipeline(string technique, BlendStateDesc blend, DepthStencilStateDesc depthStencil, PrimitiveTopology topology)
    {
        var pipeline = _device.CreatePipeline(new PipelineDesc(
            VertexShader: _effect.Module,
            PixelShader: _effect.Module,
            VertexLayouts: [VertexLayout],
            BlendState: blend,
            DepthStencilState: depthStencil,
            RasterizerState: Rasterizer,
            Topology: topology,
            TechniqueName: technique));
        return new Pipeline(pipeline, _effect.Bind(pipeline));
    }

    /// <summary>
    /// Must be called with the frame's live command buffer before any drawing is done through this
    /// renderer's owning <c>NvgContext</c> (path/text calls can trigger texture uploads, e.g. font
    /// atlas growth, synchronously - not just <see cref="Draw"/> itself) and cleared afterward.
    /// </summary>
    public void BeginFrame(ICommandBuffer cb) => _cb = cb;
    public void EndFrame() => _cb = null;

    public object CreateTexture(int width, int height)
    {
        return _device.CreateTexture(new TextureDesc(width, height, TextureFormat.Rgba8));
    }

    public Point GetTextureSize(object texture)
    {
        var t = (ITexture)texture;
        return new Point(t.Width, t.Height);
    }

    public void SetTextureData(object texture, Rectangle bounds, byte[] data)
    {
        if (_cb == null)
        {
            Logging.Warning("AbstractionNvgRenderer.SetTextureData called with no live command buffer - dropped.");
            return;
        }
        _cb.UpdateTexture((ITexture)texture, bounds.X, bounds.Y, bounds.Width, bounds.Height, data);
    }

    public void Draw(float devicePixelRatio, ReadOnlySpan<CallInfo> calls, Vertex[] vertexes)
    {
        if (_cb == null)
        {
            Logging.Warning("AbstractionNvgRenderer.Draw called with no live command buffer - dropped.");
            return;
        }
        var cb = _cb;

        EnsureVertexBuffer(cb, vertexes);

        // Each of the 9 pipelines below owns its own cloned FNA3D Effect instance (and therefore
        // its own uniform value buffer, keyed by whichever pipeline is currently SetPipeline-bound
        // - see FNA3DCommandBuffer.SetUniform) - transformMat has to be (re-)set on whichever
        // pipeline is about to draw, not once globally, or every pipeline except the last one
        // SetUniform happened to touch keeps its zero-initialized (all-zero matrix) default.
        _transform = System.Numerics.Matrix4x4.CreateOrthographicOffCenter(0, _device.Swapchain.Width, _device.Swapchain.Height, 0, 0, -1);

        foreach (var call in calls)
        {
            switch (call.Type)
            {
                case CallType.Fill: RenderFill(cb, call); break;
                case CallType.ConvexFill: RenderConvexFill(cb, call); break;
                case CallType.Stroke: RenderStroke(cb, call); break;
                case CallType.Triangles: RenderTriangles(cb, call); break;
            }
        }
    }

    private void EnsureVertexBuffer(ICommandBuffer cb, Vertex[] vertexes)
    {
        if (vertexes.Length > _vertexBufferCapacity)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferCapacity = (int)(vertexes.Length * 1.5f) + 16;
            _vertexBuffer = _device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, _vertexBufferCapacity * VertexStride));
        }
        cb.UpdateBuffer(_vertexBuffer!, System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertexes.AsSpan(0, vertexes.Length)));
    }

    private void EnsureFanIndexBuffer(ICommandBuffer cb, int fanVertexCount)
    {
        if (fanVertexCount <= _fanIndexBufferCapacity) return;

        _fanIndexBuffer?.Dispose();
        _fanIndexBufferCapacity = Math.Max(fanVertexCount, 2048 * 6);
        var indices = new ushort[(_fanIndexBufferCapacity - 2) * 3];
        for (var j = 2; j < _fanIndexBufferCapacity; j++)
        {
            indices[(j - 2) * 3 + 0] = 0;
            indices[(j - 2) * 3 + 1] = (ushort)(j - 1);
            indices[(j - 2) * 3 + 2] = (ushort)j;
        }
        _fanIndexBuffer = _device.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(indices.AsSpan()));
    }

    /// <summary>
    /// <c>Nvg.fx</c>'s <c>scissorMask</c>/paint-space code (ported 1:1 from Effect.fx) truncates
    /// the 4x4 uniform to a <c>float3x3</c> before using it in <c>mul((float3x3)m, float3(p,1))</c>
    /// - the reference XNA renderer's <c>Transform.ToMatrix()</c> (the <c>#if MONOGAME || FNA ||
    /// STRIDE</c> branch) packs translation into M13/M23 (column 3, rows 1-2) specifically so it
    /// survives that truncation. NvgSharp's PLATFORM_AGNOSTIC <c>ToMatrix()</c> (the one
    /// <see cref="UniformInfo"/> actually uses here) instead packs translation into M41/M42 (row 4)
    /// - a row-vector-convention layout matching <c>transformMat</c>'s own `mul(vec, mat)` usage,
    /// but wrong for scissorMat/paintMat's matrix-first, 3x3-truncated usage. Since
    /// FNA3DCommandBuffer.SetUniform's Matrix4x4 transpose is shared, generic infrastructure
    /// (correct for transformMat), this moves translation from row 4 into M13/M23 before that
    /// transpose, mirroring the reference layout exactly instead of the unreachable M41/M42.
    /// </summary>
    private static System.Numerics.Matrix4x4 FixupMatrixForFloat3x3Truncation(System.Numerics.Matrix4x4 m)
    {
        m.M13 = m.M41; m.M23 = m.M42; m.M33 = 1f;
        m.M41 = 0f; m.M42 = 0f; m.M43 = 0f; m.M44 = 1f;
        return m;
    }

    private void SetUniform(ICommandBuffer cb, in NvgEffectParameters p, in UniformInfo uniform)
    {
        p.transformMat.SetValue(cb, _transform);
        p.scissorMat.SetValue(cb, FixupMatrixForFloat3x3Truncation(uniform.scissorMat));
        p.paintMat.SetValue(cb, FixupMatrixForFloat3x3Truncation(uniform.paintMat));
        p.innerCol.SetValue(cb, uniform.innerCol);
        p.outerCol.SetValue(cb, uniform.outerCol);
        p.scissorExt.SetValue(cb, uniform.scissorExt);
        p.scissorScale.SetValue(cb, uniform.scissorScale);
        p.extent.SetValue(cb, uniform.extent);
        p.radius.SetValue(cb, uniform.radius);
        p.feather.SetValue(cb, uniform.feather);
        p.strokeMult.SetValue(cb, uniform.strokeMult);
        p.strokeThr.SetValue(cb, uniform.strokeThr);
        p.g_texture.SetValue(cb, uniform.Image as ITexture, _sampler);
    }

    private void DrawFanList(ICommandBuffer cb, in Pipeline pipeline, in UniformInfo uniform, int vertexOffset, int vertexCount)
    {
        if (vertexCount == 0) return;
        EnsureFanIndexBuffer(cb, vertexCount);
        cb.SetPipeline(pipeline.State);
        cb.SetVertexBuffer(0, _vertexBuffer!, VertexStride);
        cb.SetIndexBuffer(_fanIndexBuffer!);
        SetUniform(cb, pipeline.Parameters, uniform);
        cb.DrawIndexed(vertexOffset, 0, vertexCount - 2);
    }

    private void DrawStrip(ICommandBuffer cb, in Pipeline pipeline, in UniformInfo uniform, int vertexOffset, int vertexCount)
    {
        if (vertexCount == 0) return;
        cb.SetPipeline(pipeline.State);
        cb.SetVertexBuffer(0, _vertexBuffer!, VertexStride);
        SetUniform(cb, pipeline.Parameters, uniform);
        cb.Draw(vertexOffset, vertexCount - 2);
    }

    private void DrawTriangleList(ICommandBuffer cb, in Pipeline pipeline, in UniformInfo uniform, int vertexOffset, int vertexCount)
    {
        if (vertexCount == 0) return;
        cb.SetPipeline(pipeline.State);
        cb.SetVertexBuffer(0, _vertexBuffer!, VertexStride);
        SetUniform(cb, pipeline.Parameters, uniform);
        cb.Draw(vertexOffset, vertexCount / 3);
    }

    private void RenderConvexFill(ICommandBuffer cb, in CallInfo call)
    {
        var fillPipeline = call.UniformInfo.Image != null ? _pImageFillList : _pGradientFillList;
        var stripPipeline = call.UniformInfo.Image != null ? _pImageStrip : _pGradientStrip;

        foreach (var fs in call.FillStrokeInfos)
        {
            DrawFanList(cb, fillPipeline, call.UniformInfo, fs.FillOffset, fs.FillCount);
            DrawStrip(cb, stripPipeline, call.UniformInfo, fs.StrokeOffset, fs.StrokeCount);
        }
    }

    private void RenderFill(ICommandBuffer cb, in CallInfo call)
    {
        foreach (var fs in call.FillStrokeInfos)
            DrawFanList(cb, _pSimpleStencilFill1, call.UniformInfo, fs.FillOffset, fs.FillCount);

        var fringePipeline = call.UniformInfo2.Image != null ? _pImageFringe : _pGradientFringe;
        foreach (var fs in call.FillStrokeInfos)
            DrawStrip(cb, fringePipeline, call.UniformInfo2, fs.StrokeOffset, fs.StrokeCount);

        DrawStrip(cb, _pGradientCleanup, call.UniformInfo2, call.TriangleOffset, call.TriangleCount);
    }

    private void RenderStroke(ICommandBuffer cb, in CallInfo call)
    {
        var stripPipeline = call.UniformInfo.Image != null ? _pImageStrip : _pGradientStrip;
        foreach (var fs in call.FillStrokeInfos)
            DrawStrip(cb, stripPipeline, call.UniformInfo, fs.StrokeOffset, fs.StrokeCount);
    }

    private void RenderTriangles(ICommandBuffer cb, in CallInfo call)
    {
        DrawTriangleList(cb, _pTrianglesList, call.UniformInfo, call.TriangleOffset, call.TriangleCount);
    }

    public void Dispose()
    {
        _pSimpleStencilFill1.State.Dispose();
        _pGradientFringe.State.Dispose();
        _pImageFringe.State.Dispose();
        _pGradientCleanup.State.Dispose();
        _pGradientFillList.State.Dispose();
        _pGradientStrip.State.Dispose();
        _pImageFillList.State.Dispose();
        _pImageStrip.State.Dispose();
        _pTrianglesList.State.Dispose();
        _sampler.Dispose();
        _vertexBuffer?.Dispose();
        _fanIndexBuffer?.Dispose();
    }
}
