using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rendering;

namespace NFMWorld;

public class FixFlare : IDisposable, IImmediateRenderElement
{
    private const float Inflation = 1.3f;
    private const int ShellVertexCount = FixFlareGeometry.ShellVertexCount;
    private const int ShellIndexCount = FixFlareGeometry.ShellTriangleCount * 3;

    private readonly CarVisual _visual;
    private readonly PositionColorVertex[] _verts = new PositionColorVertex[ShellVertexCount * 2];
    private readonly Vector3[] _shell = new Vector3[ShellVertexCount];
    private readonly float[] _innerJitter = new float[ShellVertexCount];
    private readonly float[] _outerJitter = new float[ShellVertexCount];
    private readonly Vector3 _originalMin;
    private readonly Vector3 _originalMax;
    private Vector3 _min;
    private Vector3 _max;
    private bool _active;
    private static readonly ushort[] Indices = BuildIndices();
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

    public void SetFixFx()
    {
        _active = true;
        _min = _originalMin;
        _max = _originalMax;
        foreach (var poly in _visual.Mesh.Polys)
        foreach (var point in poly.Points)
        {
            _min = Vector3.Min(_min, point);
            _max = Vector3.Max(_max, point);
        }
        for (var i = 0; i < ShellVertexCount; i++)
        {
            _innerJitter[i] = 1 + URandom.Single() * 0.06f;
            _outerJitter[i] = 1.2f + URandom.Single() * 0.1f;
        }
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (!_active) return;

        var center = (_min + _max) * 0.5f;
        var halfSize = FixFlareGeometry.BallHalfSize((_max - _min) * 0.5f);
        FixFlareGeometry.WriteShell(center, halfSize, Inflation, _shell);

        var outerColor = new Color(
            Math.Clamp((int)(191 + 191 * World.Snap[0] / 350f), 0, 255),
            Math.Clamp((int)(232 + 232 * World.Snap[1] / 350f), 0, 255),
            Math.Clamp((int)(255 + 255 * World.Snap[2] / 350f), 0, 255));
        var innerColor = new Color(
            Math.Clamp((int)(213 + 213 * World.Snap[0] / 350f), 0, 255),
            Math.Clamp((int)(239 + 239 * World.Snap[1] / 350f), 0, 255),
            Math.Clamp((int)(255 + 255 * World.Snap[2] / 350f), 0, 255));
        for (var i = 0; i < ShellVertexCount; i++)
        {
            _verts[i] = new PositionColorVertex(center + (_shell[i] - center) * _outerJitter[i], outerColor);
            _verts[ShellVertexCount + i] = new PositionColorVertex(center + (_shell[i] - center) * _innerJitter[i], innerColor);
        }
        cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes(_verts.AsSpan()));

        var p = Effects.ParticleParameters;
        cb.SetPipeline(Effects.ParticleDepthReadPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);
        p.World.SetValue(cb, _visual.MatrixWorld);
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);
        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: Indices.Length / 3);
    }

    private static ushort[] BuildIndices()
    {
        var indices = new ushort[ShellIndexCount * 2];
        FixFlareGeometry.WriteShellIndices(indices, 0);
        FixFlareGeometry.WriteShellIndices(indices.AsSpan(ShellIndexCount), (ushort)ShellVertexCount);
        return indices;
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
