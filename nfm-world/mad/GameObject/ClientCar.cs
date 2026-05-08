using Microsoft.Xna.Framework.Graphics;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world_library.SoftFloat;
using nfm_world.camera;
using nfm_world.sfx;

namespace nfm_world;

public class ClientCar : MeshedGameObject, ICar, IDisposable
{
    private ICar _backendCar;

    #region Synced fields - altering these could cause the client to desync
    public Rad3d Rad => _backendCar.Rad;

    public CarStats Stats => _backendCar.Stats;

    public int GroundAt => _backendCar.GroundAt;

    public int MaxRadius => _backendCar.MaxRadius;

    public f64Euler WheelAngle
    {
        get => _backendCar.WheelAngle;
        set => _backendCar.WheelAngle = value;
    }

    public f64Euler TurningWheelAngle
    {
        get => _backendCar.TurningWheelAngle;
        set => _backendCar.TurningWheelAngle = value;
    }

    public IReadOnlyList<Rad3dWheelDef> Wheels => _backendCar.Wheels;
    #endregion

    // Stores "brokenness" phase for damageable meshes
    public readonly float[] Bfase;

    internal readonly Flames Flames;
    internal readonly Dust Dust;
    internal readonly Chips Chips;
    internal readonly Sparks Sparks;
    private readonly MeshedGameObject[] _wheels;

    public string FileName => Mesh.FileName;

    public bool VisuallyWasted { get; set; }

    public MadSfx? Sfx;

    private event Action? GameTicked;

    public ClientCar(GraphicsDevice graphicsDevice, ICar backendCar)
        : base(new CarMesh(graphicsDevice, backendCar.Rad))
    {
        Bfase = new float[Mesh.Polys.Length];
        
        _backendCar = backendCar;
        _wheels = Wheels
            .Select(wheel => new WheelMeshBuilder(wheel, backendCar.Rad.Rims).BuildGameObject(graphicsDevice, this))
            .ToArray();
        Flames = new Flames(this, graphicsDevice);
        Dust = new Dust(this, graphicsDevice);
        Chips = new Chips(this, graphicsDevice);
        Sparks = new Sparks(this, graphicsDevice);
        
        Position = backendCar.Position;
        Rotation = backendCar.Rotation;
    }

    public ClientCar(GraphicsDevice graphicsDevice, IInGameCar backendCar)
        : this(graphicsDevice, (ICar)backendCar)
    {
        backendCar.DamagedX += BackendCarOnDamagedX;
        backendCar.DamagedY += BackendCarOnDamagedY;
        backendCar.DamagedZ += BackendCarOnDamagedZ;
        backendCar.Sparked += BackendCarOnSparked;
        backendCar.Dusted += BackendCarOnDusted;
        backendCar.Mad.Distruct += MadOnDistruct;
        Sfx = new MadSfx(backendCar.Mad);
        // TODO better solution for this
        GameTicked += () =>
        {
            Sfx.Tick(backendCar.Control, backendCar.Mad, backendCar.Stats);
        };

        return;

        void BackendCarOnDamagedX(CarStats stat, int wheelnum, fix64 amount)
        {
            MeshDamage.DamageX(stat, this, wheelnum, (float)amount);
        }

        void BackendCarOnDamagedY(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash)
        {
            MeshDamage.DamageY(stat, this, wheelnum, (float)amount, mtouch, ref nbsq, ref squash);
        }

        void BackendCarOnDamagedZ(CarStats stat, int wheelnum, fix64 amount)
        {
            MeshDamage.DamageZ(stat, this, wheelnum, (float)amount);
        }

        void BackendCarOnSparked(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
        {
            Sparks.AddSpark(wheelx, wheely, wheelz, scx, scy, scz, type, wheelGround);
        }
        
        void BackendCarOnDusted(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
        {
            Dust.AddDust(wheelidx, wheelx, wheely, wheelz, scx, scz, simag, tilt, onRoof, wheelGround);
        }

        void MadOnDistruct(object? sender, EventArgs e)
        {
            VisuallyWasted = true;
        }
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

    public override void GameTick(IStage? stage = null)
    {
        Flames.GameTick();
        Dust.GameTick(stage);
        Chips.GameTick();
        Sparks.GameTick();
        base.GameTick(stage);
        GameTicked?.Invoke();
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

    private void ReleaseUnmanagedResources()
    {
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            Flames.Dispose();
            Dust.Dispose();
            Chips.Dispose();
            Sparks.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ClientCar()
    {
        Dispose(false);
    }
}