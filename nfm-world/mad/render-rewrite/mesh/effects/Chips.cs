using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world.compat;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world.camera;
using nfm_world.shaders;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.mesh.effects;

public class Chips : IDisposable
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
    
    private readonly ClientCar _car;
    private readonly GraphicsDevice _graphicsDevice;
    
    private Chip[] _chips;
    private readonly VertexPositionColor[] _triangles;
    private int _triangleCount;
    private readonly GpuBuffer _vertexBuffer;

    public Chips(ClientCar car, GraphicsDevice graphicsDevice)
    {
        _car = car;
        _graphicsDevice = graphicsDevice;
        _chips = new Chip[_car.Mesh.Polys.Length];
        
        var maxVerts = (uint)(3 * _car.Mesh.Polys.Length);
        _vertexBuffer = GpuBuffer.Create<VertexPositionColor>(graphicsDevice, BufferUsageFlags.Vertex, maxVerts);
        _triangles = new VertexPositionColor[maxVerts];
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
            
                _triangles[tri++] = new VertexPositionColor(chip.V0, chip.Color);
                _triangles[tri++] = new VertexPositionColor(chip.V1, chip.Color);
                _triangles[tri++] = new VertexPositionColor(chip.V2, chip.Color);
                _triangleCount++;

                chip.State++;
                if (chip.State == 60)
                {
                    chip.State = 0;
                }
            }
        }
    }

    public void UploadBuffers(CopyPass copyPass)
    {
        if (_triangleCount == 0) return;

        var vtxCount = (uint)(_triangleCount * 3);
        using var vtxTransfer = TransferBuffer.Create<VertexPositionColor>(_graphicsDevice, TransferBufferUsage.Upload, vtxCount);
        var vtxSpan = vtxTransfer.Map<VertexPositionColor>(false);
        _triangles.AsSpan(0, (int)vtxCount).CopyTo(vtxSpan);
        vtxTransfer.Unmap();

        copyPass.UploadToBuffer<VertexPositionColor>(vtxTransfer, _vertexBuffer, 0, 0, vtxCount, true);
    }

    public void Render(Camera camera)
    {
        if (_triangleCount == 0) return;

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        var vtxCount = (uint)(_triangleCount * 3);

        var wvp = _car.MatrixWorld * camera.ViewMatrix * camera.ProjectionMatrix;
        cmd.PushVertexUniformData(new BasicEffectVertexUniforms { WorldViewProjection = wvp });

        pass.BindGraphicsPipeline(Pipelines.BasicEffect);
        pass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
        pass.DrawPrimitives(vtxCount, 1, 0, 0);
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
        _vertexBuffer.Dispose();
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