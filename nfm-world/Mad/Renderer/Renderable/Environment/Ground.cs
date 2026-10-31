using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Ground : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IBuffer _vertexBuffer;
    private readonly int _triangleCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Ground(IGraphicsDevice graphicsDevice)
    {
        // Generate a quad on World.Ground extending infinitely in X and Z
        _graphicsDevice = graphicsDevice;
        const int size = 1_000_000;
        var color = (Color)World.GroundColor.Snap(World.Snap);
        Span<PositionColorVertex> data =
        [
            new(new Vector3(-size, World.Ground, -size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, size), color)
        ];

        var vertexBytes = MemoryMarshal.AsBytes(data);
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);
        _triangleCount = data.Length / 3;
    }

    ~Ground()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.Ground), this);
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting)
    {
        var p = Effects.GroundParameters;

        cb.SetPipeline(Effects.GroundPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);

        p.WorldView.SetValue(cb, camera.ViewMatrix);
        p.WorldViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.FogColor.SetValue(cb, World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);

        // See Submesh.Render: the shader default (0.0005) is too large for this camera's depth
        // range and leaves the terrain lit almost everywhere. Pre-migration Ground set 0.00005 too.
        p.DepthBias.SetValue(cb, 0.00005f);

        lighting?.SetShadowMapParameters(cb, Effects.GroundPipeline.Reflection);

        cb.Draw(startVertex: 0, primitiveCount: _triangleCount);
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
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
