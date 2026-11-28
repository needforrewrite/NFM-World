using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;

namespace NFMWorld;

public class Dust : IDisposable, IImmediateRenderElement
{
    private const int PuffCapacity = 20;
    private const int CornersPerPuff = 8;
    private const int TrianglesPerPuff = CornersPerPuff - 2;
    private const float PuffSizeScale = 0.5f;

    private static readonly Vector2[] Corners =
    [
        new(0.9238f * 1.5f, -0.3826f * 1.5f),
        new(0.9238f * 1.5f, 0.3826f * 1.5f),
        new(0.3826f, 0.9238f),
        new(-0.3826f, 0.9238f),
        new(-0.9238f * 1.5f, 0.3826f * 1.5f),
        new(-0.9238f * 1.5f, -0.3826f * 1.5f),
        new(-0.3826f * 1.7f, -0.9238f),
        new(0.3826f * 1.7f, -0.9238f)
    ];

    private static readonly ushort[] Indices = BuildIndices();

    private readonly record struct Puff(Vector3 Center, Color Color);

    private int _ust;

    private float[] Sx = new float[PuffCapacity];
    private float[] Sy = new float[PuffCapacity];
    private float[] Sz = new float[PuffCapacity];
    private float[] Osmag = new float[PuffCapacity];
    private float[] Scx = new float[PuffCapacity];
    private float[] Scz = new float[PuffCapacity];
    private int[] Stg = new int[PuffCapacity];
    private float[] _sbln = new float[PuffCapacity];
    private readonly SurfaceType?[] _surfaces = new SurfaceType?[PuffCapacity];
    private float[,] _smag = new float[PuffCapacity, CornersPerPuff];
    private readonly Puff[] _puffs = new Puff[PuffCapacity];
    private readonly float[] _puffRadii = new float[PuffCapacity * CornersPerPuff];
    private readonly PositionColorVertex[] _verts = new PositionColorVertex[PuffCapacity * CornersPerPuff];
    private int _puffCount;
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;

    public Dust(IGraphicsDevice graphicsDevice)
    {
        var maxVertexBytes = _verts.Length * PositionColorVertex.Stride;
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, maxVertexBytes),
            new byte[maxVertexBytes]
        );
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, Indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)Indices)
        );
    }

    public void AddDust(
        int wheelidx,
        float wheelx,
        float wheely,
        float wheelz,
        int scx,
        int scz,
        float simag,
        int tilt,
        int wheelGround,
        SurfaceType? surface
    )
    {
        var noDust = false;

        if (tilt > 5 && wheelidx is 0 or 2)
        {
            noDust = true;
        }

        if (tilt < -5 && wheelidx is 1 or 3)
        {
            noDust = true;
        }

        var dist = (float)((Math.Sqrt(scx * scx + scz * scz) - 40.0) / 160.0);

        if (dist > 1.0F)
        {
            dist = 1.0F;
        }

        if (dist > 0.2 && !noDust)
        {
            _ust++;

            if (_ust == PuffCapacity)
            {
                _ust = 0;
            }

            Sx[_ust] = wheelx;
            Sy[_ust] = wheely - wheelGround;
            Sz[_ust] = wheelz;

            Osmag[_ust] = simag * dist;
            Scx[_ust] = scx;
            Scz[_ust] = scz;
            _surfaces[_ust] = surface;
            Stg[_ust] = 1;
        }
    }

    public void GameTick(BackendStage? stage)
    {
        _puffCount = 0;

        for (var dust = 0; dust < PuffCapacity; dust++)
        {
            if (Stg[dust] != 0)
            {
                TickDust(stage, dust);
            }
        }
    }

    private void TickDust(BackendStage? stage, int dust)
    {
        if (Stg[dust] == 1)
        {
            _sbln[dust] = 0.6F;

            if (stage != null)
            {
                var sx = (fix64)Sx[dust];
                var sz = (fix64)Sz[dust];

                foreach (var tracker in stage.RetrievePointCollidables(sx, sz))
                {
                    if (tracker.TryGetValue(out ShapeRoad boxRoad))
                    {
                        var x = boxRoad.GameObjectPosition.X + boxRoad.TrackersPosition.X;
                        var z = boxRoad.GameObjectPosition.Z + boxRoad.TrackersPosition.Z;

                        if (fix64.Abs(sx - x) < boxRoad.Radius.X && fix64.Abs(sz - z) < boxRoad.Radius.Z)
                        {
                            _sbln[dust] = tracker.SurfaceType switch
                            {
                                SurfaceType.Road => 0.2F,
                                SurfaceType.OffTrack => 0.4F,
                                SurfaceType.OffRoad => 0.45F,
                                _ => _sbln[dust]
                            };

                            break;
                        }
                    }
                }
            }

            var f = 0.1f + URandom.Single();

            if (f > 1.0F)
            {
                f = 1.0F;
            }

            Scx[dust] = (Scx[dust] * f);
            Scz[dust] = (Scz[dust] * f);

            for (var i216 = 0; i216 < CornersPerPuff; i216++)
            {
                _smag[dust, i216] = Osmag[dust] * URandom.Single() * 50.0F;
            }

            for (var vert = 0; vert < CornersPerPuff; vert++)
            {
                var lastVert = vert - 1;

                if (lastVert == -1)
                {
                    lastVert = CornersPerPuff - 1;
                }

                var nextVert = vert + 1;

                if (nextVert == CornersPerPuff)
                {
                    nextVert = 0;
                }

                _smag[dust, vert] = ((_smag[dust, lastVert] + _smag[dust, nextVert]) / 2.0F + _smag[dust, vert]) / 2.0F;
            }

            _smag[dust, 6] = _smag[dust, 7];
        }

        var alpha = _sbln[dust] - Stg[dust] * (_sbln[dust] / 8.0F);
        var tint = _surfaces[dust] switch
        {
            null => Vector3.Lerp(World.GroundColor.Snap(World.Snap), Vector3.One, 0.35f),
            SurfaceType.OffTrack or SurfaceType.OffRoad or SurfaceType.Bump or SurfaceType.BumpySides =>
                new Vector3(0.90f, 0.61f, 0.32f),
            _ => Vector3.One
        };
        var color = new Color(tint.X, tint.Y, tint.Z, alpha);
        var center = new Vector3(Sx[dust], Sy[dust] - _smag[dust, 7] * PuffSizeScale, Sz[dust]);
        _puffs[_puffCount] = new Puff(center, color);
        for (var corner = 0; corner < CornersPerPuff; corner++)
            _puffRadii[_puffCount * CornersPerPuff + corner] = _smag[dust, corner] * PuffSizeScale;
        _puffCount++;

        Sx[dust] += Scx[dust] / (Stg[dust] + 1);
        Sz[dust] += Scz[dust] / (Stg[dust] + 1);

        for (var vert = 0; vert < 7; vert++)
        {
            _smag[dust, vert] += 5.0F + URandom.Single() * 15.0F;
        }

        _smag[dust, 7] = _smag[dust, 6];

        if (Stg[dust] == 7)
        {
            Stg[dust] = 0;
        }
        else
        {
            Stg[dust]++;
        }
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (_puffCount == 0)

            return;

        // Offset each corner in view space so the puff faces the rendered camera.

        for (var i = 0; i < _puffCount; i++)
        {
            var puff = _puffs[i];
            var center = Vector3.Transform(puff.Center, camera.ViewMatrix);

            for (var corner = 0; corner < CornersPerPuff; corner++)
            {
                var index = i * CornersPerPuff + corner;
                var offset = Corners[corner] * _puffRadii[index];
                _verts[index] = new PositionColorVertex(center + new Vector3(offset, 0f), puff.Color);
            }
        }

        cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes(_verts.AsSpan(0, _puffCount * CornersPerPuff)));

        var p = Effects.ParticleParameters;

        cb.SetPipeline(Effects.ParticleDepthReadPipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        p.World.SetValue(cb, Matrix.Identity);
        p.View.SetValue(cb, Matrix.Identity);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);

        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: _puffCount * TrianglesPerPuff);
    }

    private static ushort[] BuildIndices()
    {
        var indices = new ushort[PuffCapacity * TrianglesPerPuff * 3];
        var index = 0;

        for (var puff = 0; puff < PuffCapacity; puff++)
        {
            var first = puff * CornersPerPuff;

            for (var corner = 1; corner < CornersPerPuff - 1; corner++)
            {
                indices[index++] = (ushort)first;
                indices[index++] = (ushort)(first + corner);
                indices[index++] = (ushort)(first + corner + 1);
            }
        }

        return indices;
    }

    private void ReleaseUnmanagedResources()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~Dust()
    {
        ReleaseUnmanagedResources();
    }
}
