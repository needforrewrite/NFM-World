using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer);
}

public class CollisionObject(Mesh mesh) : MeshedGameObject(mesh)
{
    public Rad3dBoxDef[] Boxes => Mesh.Rad.Boxes;
    private readonly CollisionDebugMesh? _collisionDebugMesh;
    
    public CollisionObject(Mesh mesh, Vector3 position, Euler rotation) : this(mesh)
    {
        Position = position;
        Rotation = rotation;
        if (mesh.Rad.Boxes.Length > 0)
        {
            _collisionDebugMesh = new CollisionDebugMesh(mesh.Rad.Boxes)
            {
                Parent = this
            };
        }
    }

    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        base.Render(camera, lighting);
        if (_collisionDebugMesh != null && lighting?.IsCreateShadowMap != true && GameSparker.devRenderTrackers)
        {
            _collisionDebugMesh.Render(camera, lighting);
        }
    }
}

public class Car : MeshedGameObject
{
    public CarStats Stats;
    private readonly MeshedGameObject[] _wheels;

    public Rad3dWheelDef[] Wheels => Mesh.Rad.Wheels;
    public Rad3dRimsDef? Rims => Mesh.Rad.Rims;
    
    // Stores "brokenness" phase for damageable meshes
    public readonly float[] Bfase;

    internal readonly Flames Flames;
    internal readonly Dust Dust;
    internal readonly Chips Chips;
    internal readonly Sparks Sparks;
    
    public Euler TurningWheelAngle { get; set; }
    public Euler WheelAngle { get; set; }

    public int GroundAt => Mesh.GroundAt;
    public int MaxRadius => Mesh.MaxRadius;
    public string FileName => Mesh.FileName;

    // visually wasted
    public bool Wasted;

    public Car(Mesh mesh) : base(new Mesh(mesh))
    {
        string? invalidStat = mesh.Rad.Stats.Validate(mesh.FileName);
        if (invalidStat != null)
        {
            Stats = CarStats.Default;
            if(invalidStat == nameof(Stats.Name) || mesh.Rad.Stats.Name.IsNullOrEmpty())
            {
                Stats = Stats with { Name = mesh.FileName };
            }
        }
        else
        {
            Stats = mesh.Rad.Stats;
        }
        
        Bfase = new float[mesh.Polys.Length];

        var graphicsDevice = mesh.GraphicsDevice;
        _wheels = Array.ConvertAll(Wheels, wheel => new WheelMeshBuilder(wheel, Rims).BuildGameObject(graphicsDevice, this));
        Flames = new Flames(this, graphicsDevice);
        Dust = new Dust(this, graphicsDevice);
        Chips = new Chips(this, graphicsDevice);
        Sparks = new Sparks(this, graphicsDevice);
    }

    public Car(Mesh mesh, Vector3 position, Euler rotation) : this(mesh)
    {
        Position = position;
        Rotation = rotation;
    }

    public override void GameTick()
    {
        Flames.GameTick();
        Dust.GameTick();
        Chips.GameTick();
        Sparks.GameTick();
        base.GameTick();
    }

    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true && !(CastsShadow || Position.Y < World.Ground)) yield break;

        var matrixWorld = MatrixWorld;
        
        for (var i = 0; i < _wheels.Length; i++)
        {
            var wheel = _wheels[i];
            wheel.Parent = this;
            if (Wheels[i].Rotates == 11)
            {
                wheel.Rotation = TurningWheelAngle;
            }
            else
            {
                wheel.Rotation = WheelAngle;
            }

            foreach (var renderData in wheel.GetRenderData(lighting))
            {
                yield return renderData;
            }
        }
        
        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        base.Render(camera, lighting);
        
        foreach (var wheel in _wheels)
        {
            wheel.Render(camera, lighting);
        }

        if (lighting?.IsCreateShadowMap != true)
        {
            Flames.Render(camera);
            Dust.Render(camera);
            Chips.Render(camera);
            Sparks.Render(camera);
        }
    }

    public override void OnBeforeRender()
    {
        base.OnBeforeRender();
        
        foreach (var wheel in _wheels)
        {
            wheel.OnBeforeRender();
        }
    }

    public void AddDust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
    {
        Dust.AddDust(wheelidx, wheelx, wheely, wheelz, scx, scz, simag, tilt, onRoof, wheelGround);
    }

    public void Chip(int polyIdx, float breakFactor)
    {
        Chips.AddChip(polyIdx, breakFactor);
    }

    public void ChipWasted()
    {
        Chips.ChipWasted();
        // breakFactor = 2.0f
        // bfase = -7
    }

    public void Spark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        Sparks.AddSpark(wheelx, wheely, wheelz, scx, scy, scz, type, wheelGround);
    }
}