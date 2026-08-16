using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Sfx;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld;

/// <summary>
/// Client-side visual representation of an <see cref="IInGameCar"/>.
/// Reads position/rotation from the backend car each tick — does NOT store its own game state.
/// Owns rendering effects (flames, dust, chips, sparks), wheel meshes, and SFX.
/// </summary>
public class CarVisual : MeshedGameObject, IDisposable
{
    /// <summary>
    /// The backend car this visual is bound to.
    /// </summary>
    public BackendCar Car { get; }

    /// <summary>
    /// Visual properties that gamemodes can modify via <see cref="IClientCarCallbacks"/>.
    /// </summary>
    public CarVisualProperties Visuals { get; } = new();

    // Stores "brokenness" phase for damageable meshes
    public readonly float[] Bfase;

    internal readonly Flames Flames;
    internal readonly Dust Dust;
    internal readonly Chips Chips;
    internal readonly Sparks Sparks;
    internal readonly FixFlare FixFlare;
    private readonly MeshedGameObject[] _wheels;

    public string FileName => Mesh.FileName;

    public bool VisuallyWasted { get; set; }

    public MadSfx? Sfx;

    private bool _fixing;
    private byte _fixTimer;
    private int _fixTick = 0;

    public CarVisual(GraphicsDevice graphicsDevice, BackendCar car)
        : base(new CarMesh(graphicsDevice, car.Rad))
    {
        Bfase = new float[Mesh.Polys.Length];

        Car = car;
        _wheels = car.Wheels
            .Select(wheel => new WheelMeshBuilder(wheel, car.Rad.Rims).BuildGameObject(graphicsDevice, this))
            .ToArray();

        // Cars (body + wheels) render after stage pieces so FixFlare sits between them
        RenderBucket = RenderBucket.Cars;
        foreach (var w in _wheels)
            w.RenderBucket = RenderBucket.Cars;

        Flames = new Flames(this, graphicsDevice);
        Dust = new Dust(this, graphicsDevice);
        Chips = new Chips(this, graphicsDevice);
        Sparks = new Sparks(car, this, graphicsDevice);
        FixFlare = new FixFlare(car, this, graphicsDevice);

        Visuals.ApplyDefaultsFrom(this);

        PositionWithoutInterpolation = car.Position;
        RotationWithoutInterpolation = car.Rotation;

        // Subscribe to backend car events
        car.DamagedX += OnDamagedX;
        car.DamagedY += OnDamagedY;
        car.DamagedZ += OnDamagedZ;
        car.Sparked += OnSparked;
        car.Dusted += OnDusted;
        car.CarPhysics.Distruct += OnDistruct;
        car.Fixed += OnFixed;

        Sfx = new MadSfx(car.CarPhysics);
    }

    #region Event handlers

    private void OnDamagedX(CarStats stat, int wheelnum, fix64 amount)
    {
        MeshDamage.DamageX(stat, Car, this, wheelnum, (float)amount);
    }

    private void OnDamagedY(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash)
    {
        MeshDamage.DamageY(stat, Car, this, wheelnum, (float)amount, mtouch, ref nbsq, ref squash);
    }

    private void OnDamagedZ(CarStats stat, int wheelnum, fix64 amount)
    {
        MeshDamage.DamageZ(stat, Car, this, wheelnum, (float)amount);
    }

    private void OnSparked(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        Sparks.AddSpark(wheelx, wheely, wheelz, scx, scy, scz, type, wheelGround);
    }

    private void OnDusted(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
    {
        Dust.AddDust(wheelidx, wheelx, wheely, wheelz, scx, scz, simag, tilt, onRoof, wheelGround);
    }

    private void OnDistruct(object? sender, EventArgs e)
    {
        VisuallyWasted = true;
    }

    private void OnFixed()
    {
        _fixing = true;
    }

    #endregion

    public void Chip(int polyIdx, float breakFactor)
    {
        Chips.AddChip(polyIdx, breakFactor);
    }

    public void ChipWasted()
    {
        Chips.ChipWasted();
    }

    public override void GameTick(BackendStage? stage = null)
    {
        // IMPORTANT: call base first to snapshot the OLD position into PreviousState
        // for interpolation. Then sync the NEW position from the backend car.
        base.GameTick(stage);

        // Sync position/rotation from backend car
        Position = Car.Position;
        Rotation = Car.Rotation;

        // Per-tick visual overrides from gamemode (moved from GetRenderData)
        CastsShadow = Visuals.CastsShadow;
        GetsShadowed = Visuals.GetsShadowed ?? GetsShadowed;
        AlphaOverride = Visuals.AlphaOverride ?? AlphaOverride;
        Glow = Visuals.Glow ?? Glow;
        Finish = Visuals.Finish ?? Finish;

        for (var i = 0; i < _wheels.Length; i++)
        {
            var wheel = _wheels[i];
            wheel.Parent = this;
            wheel.Rotation = Car.Wheels[i].Rotates == 11 ? Car.TurningWheelAngle : Car.WheelAngle;
            wheel.GameTick(stage);
        }
        Flames.GameTick();
        Dust.GameTick(stage);
        Chips.GameTick();
        Sparks.GameTick();
        Sfx?.Tick(Car.Control, Car.CarPhysics, Car.Stats);

        IterateFix();
    }

    private void IterateFix()
    {
        if (_fixing)
        {
            if (++_fixTick == Physics.OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
            {
                _fixTick = 0;
                
                if (Mesh.PolyFixState == 1)
                {
                    Mesh.PolyFixState = 2;
                }

                if (Mesh.PolyFixState == 3)
                {
                    Mesh.PolyFixState = 2;
                }

                if (_fixTimer == 1)
                {
                    Mesh.PolyFixState = 1;
                }

                if (_fixTimer == 2)
                {
                    Mesh.PolyFixState = 1;
                }

                if (_fixTimer == 4)
                {
                    Mesh.PolyFixState = 3;
                }

                if ((_fixTimer == 1 || _fixTimer > 2) && _fixTimer != 9)
                {
                    FixFlare.SetFixFx(_fixTimer);
                }
                else
                {
                    FixFlare.DeleteFixFx();
                }

                if (_fixTimer > 7)
                {
                    Mesh.PolyFixState = 0;
                    _fixTimer = 0;
                    _fixing = false;
                    FixFlare.DeleteFixFx();
                    MeshDamage.NewCar(Car, this);
                }
                else
                {
                    _fixTimer++;
                }
            }
        }
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        // Wheels — parent/rotation already set in GameTick
        foreach (var wheel in _wheels)
        {
            wheel.SubmitDraws(queue, camera, lighting, pass);
        }

        // Body mesh (Visuals overrides already applied in GameTick)
        base.SubmitDraws(queue, camera, lighting, pass);

        // Effects — only during main pass
        if (!pass.IsShadow)
        {
            queue.AddImmediate(SortKey.Create(RenderBucket.Flames), Flames);
            queue.AddImmediate(SortKey.Create(RenderBucket.Dust), Dust);
            queue.AddImmediate(SortKey.Create(RenderBucket.Chips), Chips);
            queue.AddImmediate(SortKey.Create(RenderBucket.Sparks), Sparks);
            queue.AddImmediate(SortKey.Create(RenderBucket.FixFlare), FixFlare);
        }
    }

    public override void OnBeforeRender(float alpha)
    {
        base.OnBeforeRender(alpha);

        foreach (var wheel in _wheels)
        {
            wheel.OnBeforeRender(alpha);
        }
    }

    #region IDisposable

    private void ReleaseUnmanagedResources()
    {
        Chips.Dispose();
        Dust.Dispose();
        Flames.Dispose();
        Sparks.Dispose();
        FixFlare.Dispose();

        foreach (var wheel in _wheels)
        {
            wheel.Mesh.Dispose();
        }

        Mesh.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~CarVisual()
    {
        Dispose(false);
    }

    #endregion
}
