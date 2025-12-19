using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Chips
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
    
    private readonly Mesh _mesh;
    private readonly GraphicsDevice _graphicsDevice;
    
    private Chip[] _chips;
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _triangles;
    private int _triangleCount;

    public Chips(Mesh mesh, GraphicsDevice graphicsDevice)
    {
        _mesh = mesh;
        _graphicsDevice = graphicsDevice;
        _chips = new Chip[mesh.Polys.Length];
        
        _effect = new BasicEffect(graphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
        _triangles = new VertexPositionColor[3 * mesh.Polys.Length];
    }

    public void GameTick()
    {
        _triangleCount = 0;
        var tri = 0;
        for (var i = 0; i < _mesh.Polys.Length; i++)
        {
            var poly = _mesh.Polys[i];
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
                    if (!_mesh.Wasted)
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

                chip.V0 += chip.Delta * GameSparker.PHYSICS_MULTIPLIER;
                chip.V1 += chip.Delta * GameSparker.PHYSICS_MULTIPLIER;
                chip.V2 += chip.Delta * GameSparker.PHYSICS_MULTIPLIER;
                chip.Delta += chip.Velocity * GameSparker.PHYSICS_MULTIPLIER;
                chip.Velocity.Y += 7 * GameSparker.PHYSICS_MULTIPLIER;
                if (chip.V0.Y > World.Ground)
                {
                    chip.State = 59;
                }

                if (!_mesh.Wasted)
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
                    if (brightness > _mesh.Flames.Darken)
                    {
                        brightness = _mesh.Flames.Darken;
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

    public void Render(Camera camera)
    {
        if (_triangleCount == 0) return;

        _effect.World = _mesh.MatrixWorld;
        _effect.View = camera.ViewMatrix;
        _effect.Projection = camera.ProjectionMatrix;
        
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _triangles,
                0,
                _triangleCount,
                VertexPositionColor.VertexDeclaration
            );
        }
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
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
}