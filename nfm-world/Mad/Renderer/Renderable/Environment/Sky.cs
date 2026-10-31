using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Sky : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IBuffer _vertexBuffer;
    private readonly int _triangleCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Sky(IGraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        const float skyHeight = 1000;
        const float skyDistance = 7000;
        Vector3 skyColor = World.Sky.Snap(World.Snap);
        Vector3 fogColor = World.Fog.Snap(World.Snap);
        var layers = new LinkedList<(Vector3 Position, Vector3 Color)>();
        layers.AddLast((new Vector3(0, -skyHeight, skyDistance), skyColor));

        // Reach the terrain's fog color at the horizon with a flat slope at both ends.
        const int fogLayers = 32;
        for (var i = 1; i <= fogLayers; ++i) {
            var t = i / (float)fogLayers;
            var blend = t * t * (3 - 2 * t);
            layers.AddLast((new Vector3(0, -skyHeight * (1 - t), skyDistance),
                Vector3.Lerp(skyColor, fogColor, blend)));
        }

        var col = skyColor;
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            layers.AddFirst((new Vector3(0, -skyHeight - i * 70, skyDistance), col));
        }
        layers.AddLast((new Vector3(0, 10250, skyDistance), fogColor));

        var data = new List<PositionColorVertex>();

        var layersArr = layers.ToArray();
        for (var i = 0; i + 1 < layers.Count; ++i) {
            ReadOnlySpan<(Vector3 Position, Vector3 Color)> vertices = [
                (new Vector3(-1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(-1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
                (new Vector3(1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
            ];
            data.Add(new PositionColorVertex(vertices[0].Position, new Color(vertices[0].Color)));
            data.Add(new PositionColorVertex(vertices[1].Position, new Color(vertices[1].Color)));
            data.Add(new PositionColorVertex(vertices[2].Position, new Color(vertices[2].Color)));
            data.Add(new PositionColorVertex(vertices[1].Position, new Color(vertices[1].Color)));
            data.Add(new PositionColorVertex(vertices[2].Position, new Color(vertices[2].Color)));
            data.Add(new PositionColorVertex(vertices[3].Position, new Color(vertices[3].Color)));
        }

        var vertexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(data));
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);

        _triangleCount = data.Count / 3;
    }

    ~Sky()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.Sky), this);
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        var p = Effects.SkyParameters;

        cb.SetPipeline(Effects.SkyPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);

        // Keep the sky wall facing the camera horizontally, but let pitch and roll move its horizon.
        var viewMatrix = camera.ViewMatrix;
        viewMatrix.Translation = Vector3.Zero;
        var horizontalDirection = new Vector3(-viewMatrix.M13, 0, -viewMatrix.M33);
        if (horizontalDirection.LengthSquared() < 1e-6f)
        {
            horizontalDirection = Vector3.UnitZ;
        }
        var skyWorld = Matrix.CreateWorld(Vector3.Zero, Vector3.Normalize(horizontalDirection), -Vector3.UnitY);
        viewMatrix = skyWorld * viewMatrix;

        // Background: World.Sky darkened by the same 0.991/0.998-per-step falloff the topmost
        // gradient layer uses, so the clear colour and the layers match. This is colour-only (the
        // sky must never touch depth) and mirrors the original, which cleared the target here too -
        // the gradient layers only cover the upper part of the screen, and this fills the rest.
        Vector3 col = World.Sky.Snap(World.Snap);
        for (var i = 1; i < 20; ++i)
        {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
        }
        cb.Clear(NFMWorld.Graphics.ClearOptions.Color, new ColorRgba(col.X, col.Y, col.Z));

        p.WorldViewProj.SetValue(cb, viewMatrix * camera.ProjectionMatrix);

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
