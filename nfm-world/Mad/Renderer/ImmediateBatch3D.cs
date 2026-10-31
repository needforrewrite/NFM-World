using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

/// <summary>
/// <see cref="IGraphics3D"/> backed by three CPU batches (opaque triangles, lines, alpha triangles)
/// that each become one dynamic upload and one indexed draw in <see cref="Flush"/>. Indices are
/// 32-bit and rebased per call.
/// </summary>
public sealed class ImmediateBatch3D(IGraphicsDevice device) : IGraphics3D, IDisposable
{
    private sealed class Batch<TVertex> where TVertex : unmanaged
    {
        private static readonly int Stride = Unsafe.SizeOf<TVertex>();

        public TVertex[] Vertices = new TVertex[256];
        public uint[] Indices = new uint[768];
        public int VertexCount, IndexCount;
        private IBuffer? _vertexBuffer, _indexBuffer;
        private int _vertexBufferCapacity, _indexBufferCapacity;

        public IBuffer VertexBuffer => _vertexBuffer!;
        public IBuffer IndexBuffer => _indexBuffer!;

        public void Reserve(int vertices, int indices)
        {
            Grow(ref Vertices, VertexCount + vertices);
            Grow(ref Indices, IndexCount + indices);

            static void Grow<T>(ref T[] array, int needed)
            {
                if (needed > array.Length)
                    Array.Resize(ref array, Math.Max(needed, array.Length * 2));
            }
        }

        public void Reset() => VertexCount = IndexCount = 0;

        public void Upload(ICommandBuffer cb, IGraphicsDevice device)
        {
            if (VertexCount > _vertexBufferCapacity)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferCapacity = Vertices.Length;
                _vertexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, _vertexBufferCapacity * Stride));
            }
            if (IndexCount > _indexBufferCapacity)
            {
                _indexBuffer?.Dispose();
                _indexBufferCapacity = Indices.Length;
                _indexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Dynamic, _indexBufferCapacity * sizeof(uint), IndexFormat.UInt32));
            }

            cb.UpdateBuffer(_vertexBuffer!, MemoryMarshal.AsBytes(Vertices.AsSpan(0, VertexCount)));
            cb.UpdateBuffer(_indexBuffer!, MemoryMarshal.AsBytes(Indices.AsSpan(0, IndexCount)));
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }

    private readonly Batch<PositionColorVertex> _opaque = new();
    private readonly Batch<PositionColorVertex> _alpha = new();
    private readonly Batch<LineMesh.LineMeshVertexAttribute> _lines = new();

    // Line.fx is instanced for its world matrix; lines here are already in world space, so one identity instance.
    private IBuffer? _identityInstance;

    public void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color)
    {
        var batch = ReserveTriangles(positions.Length, indices, color.A != 255);
        var dst = batch.Vertices.AsSpan(batch.VertexCount, positions.Length);
        for (var i = 0; i < positions.Length; i++)
            dst[i] = new PositionColorVertex(positions[i], color);
        CommitIndices(batch, positions.Length, indices);
    }

    public void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices)
    {
        if (colors.Length != positions.Length)
            throw new ArgumentException("colors must be the same length as positions.", nameof(colors));

        var translucent = false;
        foreach (var c in colors)
            translucent |= c.A != 255;

        var batch = ReserveTriangles(positions.Length, indices, translucent);
        var dst = batch.Vertices.AsSpan(batch.VertexCount, positions.Length);
        for (var i = 0; i < positions.Length; i++)
            dst[i] = new PositionColorVertex(positions[i], colors[i]);
        CommitIndices(batch, positions.Length, indices);
    }

    public void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color)
        => AddLines(positions, indices, color, default);

    public void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices)
    {
        if (colors.Length != positions.Length)
            throw new ArgumentException("colors must be the same length as positions.", nameof(colors));
        AddLines(positions, indices, default, colors);
    }

    // Each segment becomes the quad Line.fx expands in screen space (see LineMeshHelpers.CreateLineMesh).
    private void AddLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color, ReadOnlySpan<Color> colors)
    {
        if (indices.Length % 2 != 0)
            throw new ArgumentException("indices length must be a multiple of 2.", nameof(indices));
        Validate(positions.Length, indices);

        var lineCount = indices.Length / 2;
        _lines.Reserve(lineCount * LineMeshHelpers.VerticesPerLine, lineCount * LineMeshHelpers.IndicesPerLine);

        var vertices = _lines.Vertices;
        var vi = _lines.VertexCount;
        var ii = _lines.IndexCount;
        for (var i = 0; i < indices.Length; i += 2)
        {
            int a = indices[i], b = indices[i + 1];
            var pa = positions[a];
            var pb = positions[b];
            var ca = colors.IsEmpty ? color : colors[a];
            var cb = colors.IsEmpty ? color : colors[b];
            var mid = (pa + pb) * 0.5f;

            // Normal is unused (unlit) but must be finite; the centroid drives the shader's distance logic.
            vertices[vi] = new(pa, pb, -1f, Vector3.UnitY, mid, ca, 0f);
            vertices[vi + 1] = new(pa, pb, 1f, Vector3.UnitY, mid, ca, 0f);
            vertices[vi + 2] = new(pa, pb, -2f, Vector3.UnitY, mid, cb, 0f);
            vertices[vi + 3] = new(pa, pb, 2f, Vector3.UnitY, mid, cb, 0f);

            var @base = (uint)vi;
            _lines.Indices[ii++] = @base;
            _lines.Indices[ii++] = @base + 1;
            _lines.Indices[ii++] = @base + 3;
            _lines.Indices[ii++] = @base;
            _lines.Indices[ii++] = @base + 3;
            _lines.Indices[ii++] = @base + 2;
            vi += LineMeshHelpers.VerticesPerLine;
        }
        _lines.VertexCount = vi;
        _lines.IndexCount = ii;
    }

    private Batch<PositionColorVertex> ReserveTriangles(int vertexCount, ReadOnlySpan<int> indices, bool translucent)
    {
        if (indices.Length % 3 != 0)
            throw new ArgumentException("indices length must be a multiple of 3.", nameof(indices));
        Validate(vertexCount, indices);

        var batch = translucent ? _alpha : _opaque;
        batch.Reserve(vertexCount, indices.Length);
        return batch;
    }

    private static void Validate(int vertexCount, ReadOnlySpan<int> indices)
    {
        foreach (var index in indices)
        {
            if ((uint)index >= (uint)vertexCount)
                throw new ArgumentOutOfRangeException(nameof(indices), $"Index {index} is out of range for {vertexCount} vertices.");
        }
    }

    private static void CommitIndices(Batch<PositionColorVertex> batch, int vertexCount, ReadOnlySpan<int> indices)
    {
        var dst = batch.Indices.AsSpan(batch.IndexCount, indices.Length);
        var baseVertex = (uint)batch.VertexCount;
        for (var i = 0; i < indices.Length; i++)
            dst[i] = baseVertex + (uint)indices[i];
        batch.VertexCount += vertexCount;
        batch.IndexCount += indices.Length;
    }

    /// <summary>Drops anything queued but not yet flushed.</summary>
    public void Reset()
    {
        _opaque.Reset();
        _alpha.Reset();
        _lines.Reset();
    }

    public void Flush(ICommandBuffer cb, Camera camera)
    {
        FlushTriangles(cb, _opaque, Effects.Immediate3DOpaquePipeline, camera);
        FlushLines(cb, camera);
        FlushTriangles(cb, _alpha, Effects.Immediate3DAlphaPipeline, camera);
    }

    private void FlushTriangles(ICommandBuffer cb, Batch<PositionColorVertex> batch, IPipelineState pipeline, Camera camera)
    {
        if (batch.IndexCount == 0) return;

        batch.Upload(cb, device);
        cb.SetPipeline(pipeline);
        cb.SetVertexBuffer(0, batch.VertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(batch.IndexBuffer);
        Effects.ParticleParameters.World.SetValue(cb, Matrix.Identity);
        Effects.ParticleParameters.View.SetValue(cb, camera.ViewMatrix);
        Effects.ParticleParameters.Projection.SetValue(cb, camera.ProjectionMatrix);
        cb.DrawIndexed(0, 0, batch.IndexCount / 3);

        batch.Reset();
    }

    private void FlushLines(ICommandBuffer cb, Camera camera)
    {
        if (_lines.IndexCount == 0) return;

        _identityInstance ??= device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, InstanceData.Stride),
            MemoryMarshal.AsBytes([new InstanceData(Matrix.Identity)]));

        _lines.Upload(cb, device);
        cb.SetPipeline(Effects.LinePipeline);
        cb.SetVertexBuffer(0, _lines.VertexBuffer, LineMesh.LineMeshVertexAttribute.Stride);
        cb.SetVertexBuffer(1, _identityInstance, InstanceData.Stride);
        cb.SetIndexBuffer(_lines.IndexBuffer);

        // Uniforms live in the pipeline, so every one the unlit path reads is set rather than left to
        // whatever the last LineMesh wrote. Alpha is not here: Line.fx's vertex color is float3.
        var p = Effects.LineParameters;
        p.IsFullbright.SetValue(cb, true);
        p.UseBaseColor.SetValue(cb, false);
        p.ChargedBlinkAmount.SetValue(cb, 0f);
        p.HalfThickness.SetValue(cb, World.OutlineThickness);
        LineEffectDistantOutlineSettings.Apply(cb, DistantOutlineBehavior.AlwaysRender);
        p.FogColor.SetValue(cb, World.Fog);
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);
        p.ViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.CameraPosition.SetValue(cb, camera.Position);
        p.Expand.SetValue(cb, false);
        p.Darken.SetValue(cb, 1f);
        p.RandomFloat.SetValue(cb, 0f);
        p.Alpha.SetValue(cb, 1f);
        p.Resolution.SetValue(cb, new Vector2(device.Swapchain.Width, device.Swapchain.Height));

        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: _lines.IndexCount / 3, instanceCount: 1);

        _lines.Reset();
    }

    public void Dispose()
    {
        _opaque.Dispose();
        _alpha.Dispose();
        _lines.Dispose();
        _identityInstance?.Dispose();
    }
}
