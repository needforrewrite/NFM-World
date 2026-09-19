using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class GroundPolys : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;
    private readonly int _triangleCount;
    private readonly int _vertexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public GroundPolys(IGraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        _graphicsDevice = graphicsDevice;

        var data = new List<PositionColorVertex>();
        var indices = new List<uint>();

        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];

            var baseIndex = (uint)data.Count;
            var color = (Color)poly.Color;
            foreach (var point in poly.Points)
            {
                data.Add(new PositionColorVertex(point, color));
            }

            for (var index = 0; index < poly.Triangles.Length; index += 3)
            {
                var i0 = poly.Triangles[index];
                var i1 = poly.Triangles[index + 1];
                var i2 = poly.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }
        }

        var vertexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(data));
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);

        var indexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(indices));
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexBytes.Length, IndexFormat.UInt32), indexBytes);

        _triangleCount = indices.Count / 3;
        _vertexCount = data.Count;
    }

    ~GroundPolys()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.GroundPolys), this);
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting)
    {
        var p = Effects.GroundParameters;

        cb.SetPipeline(Effects.GroundPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        p.WorldView.SetValue(cb, camera.ViewMatrix);
        p.WorldViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.FogColor.SetValue(cb, World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);

        lighting?.SetShadowMapParameters(cb, Effects.GroundPipeline.Reflection);

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
