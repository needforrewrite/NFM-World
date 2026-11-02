using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rendering;

namespace NFMWorld;

public class FixFlare : IDisposable, IImmediateRenderElement
{
    private readonly CarVisual _visual;
    private readonly PositionColorVertex[] _verts = new PositionColorVertex[16];
    private readonly float[] _innerJitter = new float[8];
    private readonly float[] _outerJitter = new float[8];
    private readonly Vector3 _originalMin;
    private readonly Vector3 _originalMax;
    private Vector3 _min;
    private Vector3 _max;
    private bool _active;
    private float _rotation;
    private static readonly ushort[] Indices =
    [
        0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7,
        8, 9, 10, 8, 10, 11, 8, 11, 12, 8, 12, 13, 8, 13, 14, 8, 14, 15
    ];
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;

    public FixFlare(CarVisual visual, IReadOnlyList<MeshedGameObject> wheels, IGraphicsDevice graphicsDevice)
    {
        _visual = visual;
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var poly in visual.Mesh.OriginalPolys)
        foreach (var point in poly.Points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        foreach (var wheel in wheels)
        {
            var position = (Vector3)wheel.Position;
            var radius = new Vector3(wheel.Mesh.MaxRadius);
            min = Vector3.Min(min, position - radius);
            max = Vector3.Max(max, position + radius);
        }
        if (!float.IsFinite(min.X)) { min = new Vector3(-1); max = Vector3.One; }
        _originalMin = min;
        _originalMax = max;
        _min = min;
        _max = max;

        var maxVertexBytes = _verts.Length * PositionColorVertex.Stride;
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, maxVertexBytes), new byte[maxVertexBytes]);
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, Indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)Indices));
    }

    public void DeleteFixFx() => _active = false;

    public void SetFixFx(int frame)
    {
        _active = true;
        _rotation = float.DegreesToRadians(frame is 3 or 4 ? 22 : frame is 6 or 7 ? -22 : 0);
        _min = _originalMin;
        _max = _originalMax;
        foreach (var poly in _visual.Mesh.Polys)
        foreach (var point in poly.Points)
        {
            _min = Vector3.Min(_min, point);
            _max = Vector3.Max(_max, point);
        }
        for (var i = 0; i < 8; i++)
        {
            _innerJitter[i] = 1 + URandom.Single() * 0.06f;
            _outerJitter[i] = 1.2f + URandom.Single() * 0.1f;
        }
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (!_active || !Matrix.Invert(camera.ViewMatrix, out var cameraWorld)) return;
        if (!FixFlareGeometry.TryFit(_min, _max, _visual.MatrixWorld * camera.ViewMatrix,
                camera.Near, MathF.Abs(camera.ProjectionMatrix.M34) > 0.5f, out var center, out var halfSize)) return;

        Span<Vector2> octagon = stackalloc Vector2[8];
        FixFlareGeometry.WriteOctagon(halfSize, _rotation, octagon);
        var outerColor = new Color(
            Math.Clamp((int)(191 + 191 * World.Snap[0] / 350f), 0, 255),
            Math.Clamp((int)(232 + 232 * World.Snap[1] / 350f), 0, 255),
            Math.Clamp((int)(255 + 255 * World.Snap[2] / 350f), 0, 255));
        var innerColor = new Color(
            Math.Clamp((int)(213 + 213 * World.Snap[0] / 350f), 0, 255),
            Math.Clamp((int)(239 + 239 * World.Snap[1] / 350f), 0, 255),
            Math.Clamp((int)(255 + 255 * World.Snap[2] / 350f), 0, 255));
        for (var i = 0; i < 8; i++)
        {
            _verts[i] = new PositionColorVertex(new Vector3(octagon[i] * _outerJitter[i], 0), outerColor);
            _verts[i + 8] = new PositionColorVertex(new Vector3(octagon[i] * _innerJitter[i], 0), innerColor);
        }
        cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes(_verts.AsSpan()));

        var world = cameraWorld;
        world.Translation = Vector3.Transform(center, cameraWorld);
        var p = Effects.ParticleParameters;
        cb.SetPipeline(Effects.ParticleNoDepthPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);
        p.World.SetValue(cb, world);
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);
        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: Indices.Length / 3);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        GC.SuppressFinalize(this);
    }

    ~FixFlare()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }
}
