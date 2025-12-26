using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

public interface IInstancedRenderElement
{
    void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount);
}

public class CollisionObject(PlaceableObjectInfo placeableObjectInfo) : MeshedGameObject(placeableObjectInfo.Mesh)
{
    public PlaceableObjectInfo PlaceableObjectInfo = placeableObjectInfo;

    public Rad3dBoxDef[] Boxes => PlaceableObjectInfo.Boxes;
    private readonly CollisionDebugMesh? _collisionDebugMesh;
    
    public CollisionObject(PlaceableObjectInfo placeableObjectInfo, Vector3 position, Euler rotation) : this(placeableObjectInfo)
    {
        Position = position;
        Rotation = rotation;
        if (PlaceableObjectInfo.Boxes.Length > 0)
        {
            _collisionDebugMesh = new CollisionDebugMesh(PlaceableObjectInfo.Boxes)
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
        _collisionDebugMesh?.Render(camera, lighting);
    }
}

public class ObjectInfo(Mesh mesh)
{
    public Mesh Mesh = mesh;
    public int GroundAt => Mesh.GroundAt;
    public int MaxRadius => Mesh.MaxRadius;
    public string FileName => Mesh.FileName;
    public GraphicsDevice GraphicsDevice => Mesh.GraphicsDevice;
}

public class CarInfo : ObjectInfo
{
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;

    public CarInfo(GraphicsDevice graphicsDevice, Rad3d rad, string fileName) : base(new Mesh(graphicsDevice, rad, fileName))
    {
        string? invalidStat = rad.Stats.Validate(fileName);
        if (invalidStat != null)
        {
            Stats = CarStats.Default;
            if(invalidStat == nameof(Stats.Name) || rad.Stats.Name.IsNullOrEmpty())
            {
                Stats = Stats with { Name = fileName };
            }
        }
        else
        {
            Stats = rad.Stats;
        }

        Wheels = rad.Wheels;
        Rims = rad.Rims;
    }
}

public class PlaceableObjectInfo : ObjectInfo
{
    public Rad3dBoxDef[] Boxes;

    public PlaceableObjectInfo(GraphicsDevice graphicsDevice, Rad3d rad, string fileName) : base(new Mesh(graphicsDevice, rad, fileName))
    {
        Boxes = rad.Boxes;
    }
}

public class Car : MeshedGameObject
{
    public CarInfo CarInfo;

    public CarStats Stats => CarInfo.Stats;
    private readonly MeshedGameObject[] _wheels;

    public Rad3dWheelDef[] Wheels => CarInfo.Wheels;
    public Rad3dRimsDef? Rims => CarInfo.Rims;
    
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

    public Car(CarInfo carInfo) : base(new Mesh(carInfo.Mesh))
    {
        CarInfo = carInfo;
        Bfase = new float[carInfo.Mesh.Polys.Length];

        var graphicsDevice = carInfo.GraphicsDevice;
        _wheels = Array.ConvertAll(Wheels, wheel => new WheelMeshBuilder(wheel, Rims).BuildGameObject(graphicsDevice, this));
        Flames = new Flames(this, graphicsDevice);
        Dust = new Dust(this, graphicsDevice);
        Chips = new Chips(this, graphicsDevice);
        Sparks = new Sparks(this, graphicsDevice);
    }

    public Car(CarInfo carInfo, Vector3 position, Euler rotation) : this(carInfo)
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