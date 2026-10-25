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

        var skyline = -300;

        var layers = new LinkedList<(Vector3 Position, Vector3 Color)>();
        layers.AddLast((new Vector3(0, skyline - 700, 7000), World.Sky.Snap(World.Snap)));

        Vector3 col = World.Sky.Snap(World.Snap);
        for (var i = 0; i < 16; ++i) {
            col = ((new Vector3(7, 7, 7) * col) + World.Fog) / (new Vector3(8, 8, 8));
            layers.AddLast((new Vector3(0, skyline, Fade(i)), col));
        }

        col = World.Sky.Snap(World.Snap);
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            layers.AddFirst((new Vector3(0, skyline - 700 - i * 70, 7000), col));
        }
        layers.AddLast((new Vector3(0, 10250, 7000), World.Fog));

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
        return;

        static float Fade(int i) {
            return World.FadeFrom / 2f * (i + 1);
        }
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

        // The sky is authored in camera-relative space (a huge wall at z = -7000, see the
        // constructor), so it must NOT be transformed by the camera's world view matrix. The
        // original FNA implementation built a rotation-only (yaw) view matrix here and used that -
        // passing camera.ViewMatrix instead (which also carries the camera's translation) leaves
        // the wall at an arbitrary world position, which is why the sky vanished entirely.
        var viewDirection = Vector3.Normalize(camera.LookAt - camera.Position);
        var yaw = (float)Math.Atan2(viewDirection.X, viewDirection.Z);
        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -yaw);
        var fullRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
        var combinedRotation = Quaternion.Inverse(yawRotation * fullRotation);
        var viewMatrix = Matrix.CreateFromQuaternion(combinedRotation);

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
