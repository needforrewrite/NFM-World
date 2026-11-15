using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class Mountains : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;
    private readonly int _triangleCount;
    private readonly int _vertexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Mountains(IGraphicsDevice graphicsDevice, Rad3dPoly[] polys)
        : this(graphicsDevice, EnvironmentMeshData.Prepare(polys))
    {
    }

    internal Mountains(IGraphicsDevice graphicsDevice, EnvironmentMeshData prepared)
    {
        _graphicsDevice = graphicsDevice;
        var vertexBytes = MemoryMarshal.AsBytes(prepared.Vertices.AsSpan());
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length),
            vertexBytes
        );

        var indexBytes = MemoryMarshal.AsBytes(prepared.Indices.AsSpan());
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexBytes.Length, IndexFormat.UInt32),
            indexBytes
        );

        _triangleCount = prepared.Indices.Length / 3;
        _vertexCount = prepared.Vertices.Length;
    }

    ~Mountains()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.Mountains), this);
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting)
    {
        var p = Effects.MountainsParameters;

        cb.SetPipeline(Effects.MountainsPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        p.WorldView.SetValue(cb, camera.ViewMatrix);
        p.WorldViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.FogColor.SetValue(cb, World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);

        // See Submesh.Render: the shader default (0.0005) is too large for this camera's depth
        // range and leaves the terrain lit almost everywhere. Pre-migration Mountains set 0.00005 too.
        p.DepthBias.SetValue(cb, 0.00005f);

        lighting?.SetShadowMapParameters(cb, Effects.MountainsPipeline.Reflection);

        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: _triangleCount);
    }

    private void ReleaseUnmanagedResources()
    {
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
