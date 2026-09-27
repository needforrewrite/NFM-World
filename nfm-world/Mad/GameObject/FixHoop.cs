using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;

namespace NFMWorld;

public class FixHoop : StageObjectGameObject, IImmediateRenderElement
{
    private readonly IGraphicsDevice _graphicsDevice;

    private const int CntLines = 4;

    private readonly int[] _edl = new int[CntLines];
    private readonly int[] _edr = new int[CntLines];
    private readonly int[] _elc = new int[CntLines];

    private PositionColorVertex[] _vertices = new PositionColorVertex[8*CntLines];
    private ushort[] _indices = new ushort[18*CntLines];
    // Upload happens in Render() rather than MakeElectrifiedMesh() - see Sparks.cs's identical
    // pattern for why.
    private bool _dirty;
    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;

    public FixHoop(Mesh mesh, StageObject obj) : base(mesh, obj)
    {
        // Mesh.GraphicsDevice is still FNA's XNA-typed GraphicsDevice (Mesh itself isn't fully
        // converted) - use the new static device directly, matching GameSparker.NewGraphicsDevice's
        // doc comment.
        _graphicsDevice = GameSparker.GraphicsDevice;

        var maxVertexBytes = _vertices.Length * PositionColorVertex.Stride;
        var maxIndexBytes = _indices.Length * sizeof(ushort);
        _vertexBuffer = _graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, maxVertexBytes), new byte[maxVertexBytes]);
        _indexBuffer = _graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Dynamic, maxIndexBytes, IndexFormat.UInt16), new byte[maxIndexBytes]);

        MakeElectrifiedMesh();
    }

    ~FixHoop()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public bool IsSpecial { get; set; }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (_dirty)
        {
            cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes((ReadOnlySpan<PositionColorVertex>)_vertices));
            cb.UpdateBuffer(_indexBuffer, MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)_indices));
            _dirty = false;
        }

        var p = Effects.ParticleParameters;

        cb.SetPipeline(Effects.ParticleOpaquePipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        p.World.SetValue(cb, Matrix.CreateRotationY((float)Rotation.Xz.Radians) *
                              Matrix.CreateTranslation((Vector3)Position));
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);

        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: 6 * CntLines);
    }

    private void PrepareLine(int idx)
    {
        Span<int> x = stackalloc int[8];
        Span<int> y = stackalloc int[8];
        Span<int> z = stackalloc int[8];
        if (_elc[idx] == 0)
        {
            _edl[idx] = (int)(380.0F - URandom.Single() * 760.0F);
            _edr[idx] = (int)(380.0F - URandom.Single() * 760.0F);
            _elc[idx] = 1;
        }

        var yl = (int)(_edl[idx] + (190.0F - URandom.Single() * 380.0F));
        var yr = (int)(_edr[idx] + (190.0F - URandom.Single() * 380.0F));
        var x2nd = (int)(URandom.Single() * 126.0F);
        var x1st = (int)(URandom.Single() * 126.0F);
        for (var i = 0; i < 8; i++)
        {
            z[i] = 0;
        }

        x[0] = -504;
        y[0] = -_edl[idx] - 5 - URandom.Int(0, 5);
        x[1] = -252 + x1st;
        y[1] = -yl - 5 - URandom.Int(0, 5);
        x[2] = +252 - x2nd;
        y[2] = -yr - 5 - URandom.Int(0, 5);
        x[3] = +504;
        y[3] = -_edr[idx] - 5 - URandom.Int(0, 5);
        x[4] = +504;
        y[4] = -_edr[idx] + 5 + URandom.Int(0, 5);
        x[5] = +252 - x2nd;
        y[5] = -yr + 5 + URandom.Int(0, 5);
        x[6] = -252 + x1st;
        y[6] = -yl + 5 + URandom.Int(0, 5);
        x[7] = -504;
        y[7] = -_edl[idx] + 5 + URandom.Int(0, 5);

        var r = (int) (160.0F + 160.0F * (World.Snap[0] / 500.0F));
        if (r > 255)
        {
            r = 255;
        }
        if (r < 0)
        {
            r = 0;
        }
        var g = (int) (238.0F + 238.0F * (World.Snap[1] / 500.0F));
        if (g > 255)
        {
            g = 255;
        }
        if (g < 0)
        {
            g = 0;
        }
        var b = (int) (255.0F + 255.0F * (World.Snap[2] / 500.0F));
        if (b > 255)
        {
            b = 255;
        }
        if (b < 0)
        {
            b = 0;
        }
        r = (r * 2 + 214 * (_elc[idx] - 1)) / (_elc[idx] + 1);
        g = (g * 2 + 236 * (_elc[idx] - 1)) / (_elc[idx] + 1);
        var color = (Color)new Color3((short)r,(short) g, (short)b);

        int startVertIdx = idx * 8;
        _vertices[startVertIdx + 0] = new PositionColorVertex(new Vector3(x[0], y[0], z[0]), color);
        _vertices[startVertIdx + 1] = new PositionColorVertex(new Vector3(x[1], y[1], z[1]), color);
        _vertices[startVertIdx + 2] = new PositionColorVertex(new Vector3(x[2], y[2], z[2]), color);
        _vertices[startVertIdx + 3] = new PositionColorVertex(new Vector3(x[3], y[3], z[3]), color);
        _vertices[startVertIdx + 4] = new PositionColorVertex(new Vector3(x[4], y[4], z[4]), color);
        _vertices[startVertIdx + 5] = new PositionColorVertex(new Vector3(x[5], y[5], z[5]), color);
        _vertices[startVertIdx + 6] = new PositionColorVertex(new Vector3(x[6], y[6], z[6]), color);
        _vertices[startVertIdx + 7] = new PositionColorVertex(new Vector3(x[7], y[7], z[7]), color);

        // vertices represents an outline of a polygon with 8 vertices
        // we need to create indices for 4 triangles to fill the shape

        int startTriIdx = idx * 18;
        _indices[startTriIdx + 0] = (ushort)(startVertIdx + 0);
        _indices[startTriIdx + 1] = (ushort)(startVertIdx + 1);
        _indices[startTriIdx + 2] = (ushort)(startVertIdx + 7);
        _indices[startTriIdx + 3] = (ushort)(startVertIdx + 1);
        _indices[startTriIdx + 4] = (ushort)(startVertIdx + 6);
        _indices[startTriIdx + 5] = (ushort)(startVertIdx + 7);
        _indices[startTriIdx + 6] = (ushort)(startVertIdx + 1);
        _indices[startTriIdx + 7] = (ushort)(startVertIdx + 2);
        _indices[startTriIdx + 8] = (ushort)(startVertIdx + 6);
        _indices[startTriIdx + 9] = (ushort)(startVertIdx + 2);
        _indices[startTriIdx + 10] = (ushort)(startVertIdx + 5);
        _indices[startTriIdx + 11] = (ushort)(startVertIdx + 6);
        _indices[startTriIdx + 12] = (ushort)(startVertIdx + 2);
        _indices[startTriIdx + 13] = (ushort)(startVertIdx + 3);
        _indices[startTriIdx + 14] = (ushort)(startVertIdx + 5);
        _indices[startTriIdx + 15] = (ushort)(startVertIdx + 3);
        _indices[startTriIdx + 16] = (ushort)(startVertIdx + 4);
        _indices[startTriIdx + 17] = (ushort)(startVertIdx + 5);

        if (_elc[idx] > URandom.Single() * 60.0F)
        {
            _elc[idx] = 0;
        }
        else
        {
            _elc[idx]++;
        }
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        base.SubmitDraws(queue, camera, lighting, pass);

        if (!pass.IsShadow)
        {
            queue.AddImmediate(SortKey.Create(RenderBucket.FixHoopElectricity), this);
        }
    }

    private fix64 _rotAccumulator = 0;

    private int _tick;

    public override void GameTick(BackendStage? stage = null)
    {
        base.GameTick(stage);

        _rotAccumulator += 11 * Physics.PHYSICS_MULTIPLIER_F64;
        if (_rotAccumulator > 360)
        {
            _rotAccumulator -= 360;
        }
        Rotation = Rotation with { Xy = f64AngleSingle.FromDegrees(Rotation.Xy.Degrees + _rotAccumulator) };

        if (++_tick == Physics.OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
        {
            MakeElectrifiedMesh();

            _tick = 0;
        }
    }

    private void MakeElectrifiedMesh()
    {
        for (var i = 0; i < 4; i++)
        {
            PrepareLine(i);
        }
        _dirty = true;
    }
}
