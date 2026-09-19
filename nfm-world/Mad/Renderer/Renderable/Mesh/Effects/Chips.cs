using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Chips : IDisposable, IImmediateRenderElement
{
    private struct Chip
    {
        public Vector3 V0;
        public Vector3 V1;
        public Vector3 V2;
        public byte State;
        public float Ctmag;
        public Vector3 Delta;
        public Vector3 Velocity;
        public Color3 Color;
    }

    private readonly CarVisual _car;
    private readonly IGraphicsDevice _graphicsDevice;

    private Chip[] _chips;
    private readonly PositionColorVertex[] _triangles;
    private int _triangleCount;

    // Upload happens in Render() rather than GameTick() - see Sparks.cs's identical pattern for why.
    private bool _dirty;
    private readonly IBuffer _triangleBuffer;

    public Chips(CarVisual car, IGraphicsDevice graphicsDevice)
    {
        _car = car;
        _graphicsDevice = graphicsDevice;
        _chips = new Chip[_car.Mesh.Polys.Length];

        _triangles = new PositionColorVertex[3 * _car.Mesh.Polys.Length];
        var maxBytes = _triangles.Length * PositionColorVertex.Stride;
        _triangleBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, maxBytes), new byte[maxBytes]);
    }

    public void GameTick()
    {
        _triangleCount = 0;
        var tri = 0;
        for (var i = 0; i < _car.Mesh.Polys.Length; i++)
        {
            var poly = _car.Mesh.Polys[i];
            ref var chip = ref _chips[i];
            if (chip.State != 0)
            {
                if (chip.State == 1)
                {
                    var p = URandom.Int(0, poly.Points.Length);
                    chip.V0 = poly.Points[p];

                    if (chip.Ctmag > 3.0F)
                    {
                        chip.Ctmag = 3.0F;
                    }

                    if (chip.Ctmag < -3.0F)
                    {
                        chip.Ctmag = -3.0F;
                    }

                    chip.V1.X = (chip.V0.X + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.V2.X = (chip.V0.X + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.V1.Y = (chip.V0.Y + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.V2.Y = (chip.V0.Y + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.V1.Z = (chip.V0.Z + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.V2.Z = (chip.V0.Z + chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                    chip.Delta = new Vector3(0, 0, 0);
                    if (!_car.VisuallyWasted)
                    {
                        var vx = (chip.Ctmag * (30.0F - URandom.Single() * 60.0F));
                        var vz = (chip.Ctmag * (30.0F - URandom.Single() * 60.0F));
                        var vy = (chip.Ctmag * (30.0F - URandom.Single() * 60.0F));
                        chip.Velocity = new Vector3(vx, vy, vz);
                    }
                    else
                    {
                        var vx = (chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                        var vz = (chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                        var vy = (chip.Ctmag * (10.0F - URandom.Single() * 20.0F));
                        chip.Velocity = new Vector3(vx, vy, vz);
                    }
                }

                chip.V0 += chip.Delta * Physics.PHYSICS_MULTIPLIER;
                chip.V1 += chip.Delta * Physics.PHYSICS_MULTIPLIER;
                chip.V2 += chip.Delta * Physics.PHYSICS_MULTIPLIER;
                chip.Delta += chip.Velocity * Physics.PHYSICS_MULTIPLIER;
                chip.Velocity.Y += 7 * Physics.PHYSICS_MULTIPLIER;
                if (chip.V0.Y > World.Ground)
                {
                    chip.State = 59;
                }

                if (!_car.VisuallyWasted)
                {
                    var c = URandom.Int(0, 3);

                    chip.Color = c switch
                    {
                        0 => poly.Color.Darker(),
                        1 => poly.Color,
                        2 => poly.Color.Brighter(),
                        _ => chip.Color
                    };
                }
                else
                {
                    var c = poly.Color;
                    c.ToHSB(out var hue, out var saturation, out var brightness);
                    if (brightness > _car.Mesh.Darken)
                    {
                        brightness = _car.Mesh.Darken;
                    }
                    chip.Color = Color3.FromHSB(hue, saturation, brightness);
                }

                // NFMM doesn't have this but it looks much better with it
                chip.Color = chip.Color.Snap(World.Snap);

                var chipColor = (Color)chip.Color;
                _triangles[tri++] = new PositionColorVertex(chip.V0, chipColor);
                _triangles[tri++] = new PositionColorVertex(chip.V1, chipColor);
                _triangles[tri++] = new PositionColorVertex(chip.V2, chipColor);
                _triangleCount++;

                chip.State++;
                if (chip.State == 60)
                {
                    chip.State = 0;
                }
            }
        }

        if (_triangleCount > 0)
        {
            _dirty = true;
        }
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? _)
    {
        if (_triangleCount == 0) return;

        if (_dirty)
        {
            cb.UpdateBuffer(_triangleBuffer, MemoryMarshal.AsBytes(_triangles.AsSpan(0, _triangleCount * 3)));
            _dirty = false;
        }

        var p = Effects.ParticleParameters;

        cb.SetPipeline(Effects.ParticleOpaquePipeline);
        cb.SetVertexBuffer(0, _triangleBuffer, PositionColorVertex.Stride);

        p.World.SetValue(cb, _car.MatrixWorld);
        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);

        cb.Draw(startVertex: 0, primitiveCount: _triangleCount);
    }

    public void AddChip(int polyIdx, float breakFactor)
    {
        _chips[polyIdx].State = 1;
        _chips[polyIdx].Ctmag = breakFactor;
    }

    public void ChipWasted()
    {
        for (var i = 0; i < _chips.Length; i++)
        {
            _chips[i].State = 1;
            _chips[i].Ctmag = 2f;
        }
    }

    private void ReleaseUnmanagedResources()
    {
        _triangleBuffer.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Chips()
    {
        Dispose(false);
    }
}
