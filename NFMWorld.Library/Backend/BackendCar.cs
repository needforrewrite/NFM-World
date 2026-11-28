using NFMWorld.PluginSystem;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Multiplayer;
using NFMWorld.Sentry;

namespace NFMWorldLibrary.Backend;

[PluginVisible]
public partial class BackendCar : BackendGameObject
{
    [PluginVisible] public int GroundAt { get; }
    [PluginVisible] public int MaxRadius { get; }
    [PluginVisible] public f64Euler WheelAngle { get; set; }
    [PluginVisible] public f64Euler TurningWheelAngle { get; set; }
    [PluginVisible] public IReadOnlyList<Rad3dWheelDef> Wheels { get; }
    public IReadOnlyList<Rad3dWheelDef> CollisionWheels { get; }

    [PluginVisible] public CarPhysics CarPhysics { get; }
    [PluginVisible] public Control Control { get; }
    [PluginVisible] public ushort CurrentCheckpoint { get; set; }
    [PluginVisible] public byte CurrentLap { get; set; } // mad.nlaps
    [PluginVisible] public int TotalCheckpoint { get; set; } // mad.clear
    [PluginVisible] public int LastCheckpointNode { get; set; } = -1; // resets on new lap

    /// <summary>Car angles at the moment the last checkpoint was crossed.</summary>
    [PluginVisible]
    public f64Euler LastCheckpointRotation { get; set; } = f64Euler.Identity;

    [PluginVisible] public fix64 LastCheckpointPxy { get; set; }
    [PluginVisible] public fix64 LastCheckpointPzy { get; set; }

    [PluginVisible] public int Placement { get; set; } // cp.pos
    [PluginVisible] public Rad3d Rad { get; }
    [PluginVisible] public CarStats Stats { get; internal set; }
    [PluginVisible] public bool Wasted => CarPhysics.Wasted;
    [PluginVisible] public bool DamagesStageObjects { get; set; } = true;
    [PluginVisible] public bool HasLeftRace => CarPhysics.DepartureReason.HasValue;
    public PlayerDepartureReason? DepartureReason => CarPhysics.DepartureReason;
    internal bool AnnounceDeparture { get; private set; }

    public void MarkDeparted(PlayerDepartureReason reason, bool announce = true)
    {
        if (HasLeftRace)

            return;
        AnnounceDeparture = announce;
        _fixing = false;
        _fixTick = _fixTimer = 0;
        Control.Up = Control.Down = Control.Left = Control.Right = Control.Handb = false;
        CarPhysics.MarkDeparted(reason);
    }

    public event DamageFunc? DamagedX;
    public event RoofDamageFunc? DamagedY;
    public event DamageFunc? DamagedZ;
    public event SparkFunc? Sparked;
    public event DustFunc? Dusted;
    public event Action? Fixed;

    /// <summary>
    /// Whether a model can be turned into a car at all. The physics builds a four-wheel
    /// contact set, so an unfinished editor model with fewer
    /// wheels throws in the constructor; the garage must not offer it, and the archive
    /// must not accept it. See <see cref="CarModelValidator"/> for the full check.
    /// </summary>
    public static bool IsDrivable(Rad3d rad) => CarModelValidator.HasWheelsAndGeometry(rad);

    [PluginVisible] public ClientSidePlayerInfo Player { get; }

    private bool _fixing;
    [PluginVisible] public bool IsFixing => _fixing;
    private byte _fixTimer;
    private int _fixTick = 0;

    [PluginVisible]
    public BackendCar(
        BackendCar other,
        int im,
        bool isClientPlayer
    ) : this(
        other.Rad,
        im,
        other.Position.X,
        other.Position.Z,
        isClientPlayer
    )
    {
    }

    [PluginVisible]
    public BackendCar(ClientSidePlayerInfo player, int im, fix64 x, fix64 z) : this(
        CarPaint.CreateInstance(BackendGameSparker.GetCar(player.CarName).Rad!, player.CarColors, player.Wheels),
        im,
        x,
        z,
        player.IsClientPlayer
    )
    {
        Player = player;
    }

    public BackendCar(Rad3d rad, int im, fix64 x, fix64 z, bool isClientPlayer)
    {
        Rad = rad;
        Stats = CarStats.ValidateStats(rad.Stats, "hogan rewish");

        MaxRadius = rad.MaxRadius;
        Wheels = rad.Wheels;
        CollisionWheels = SelectCollisionWheels(rad.Wheels);
        GroundAt = rad.Wheels.Length > 4 ? CollisionWheels.Max(wheel => wheel.Ground) : rad.Wheels.FirstOrDefault().Ground;

        CarPhysics = new CarPhysics(Stats, im, isClientPlayer);
        CarPhysics.Car = this;
        CarPhysics.Reseto(CarPhysics.Im, this);
        Control = new Control();

        Position = new f64Vector3(x, World.Ground - GroundAt, z);
        Rotation = f64Euler.Identity;

        Player = new ClientSidePlayerInfo
        {
            CarName = rad.FileName,
            IsClientPlayer = false,
            Color = new Color3(255, 255, 255),
            IsBot = false,
            PlayerName = "hogan rewish"
        };
    }

    private static Rad3dWheelDef[] SelectCollisionWheels(Rad3dWheelDef[] wheels)
    {
        if (wheels.Length <= 4)

            return wheels;

        // Keep the four physics slots in their authored order. Extra wheels compete at the nearest slot.
        var contacts = wheels[..4];

        foreach (var wheel in wheels.AsSpan(4))
        {
            if (wheel.Height == 0)

                continue;
            var slot = 0;
            var closest = double.PositiveInfinity;

            for (var i = 0; i < contacts.Length; i++)
            {
                var dx = (double)wheel.Position.X - (double)wheels[i].Position.X;
                var dz = (double)wheel.Position.Z - (double)wheels[i].Position.Z;
                var distance = dx * dx + dz * dz;

                if (distance >= closest)

                    continue;
                closest = distance;
                slot = i;
            }

            var current = contacts[slot];
            if (current.Height == 0
                || wheel.Ground > current.Ground
                || wheel.Ground == current.Ground
                &&
                fix64.Abs(wheel.Position.X) + fix64.Abs(wheel.Width) * (fix64)0.4
                > fix64.Abs(current.Position.X) + fix64.Abs(current.Width) * (fix64)0.4)
                contacts[slot] = wheel;
        }

        return contacts;
    }

    [PluginVisible]
    public void Drive(BackendStage stage)
    {
        var transaction = SentrySdk.StartTransaction("BackendCar.Drive", "drive-car");
        CarPhysics.Drive(Control, this, stage);
        transaction.Finish();

        CheckPointHelper.HandleAerialFall(stage, this);

        IterateFix();
    }

    private void IterateFix()
    {
        if (HasLeftRace)

            return;

        if (_fixing)
        {
            if (++_fixTick == Physics.OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
            {
                _fixTick = 0;

                if (_fixTimer > 7)
                {
                    _fixTimer = 0;
                    _fixing = false;
                    CarPhysics.FinishedFix();
                }
                else
                {
                    _fixTimer++;
                }
            }
        }
    }

    [PluginVisible]
    public void Collide(BackendCar otherCar, BackendStage stage)
    {
        var transaction = SentrySdk.StartTransaction("BackendCar.Collide", "car-collide");
        CarPhysics.Collide(this, otherCar.CarPhysics, otherCar, stage);
        transaction.Finish();
    }

    [PluginVisible]
    public void ResetPosition()
    {
        if (HasLeftRace)

            return;
        CarPhysics.Reseto(CarPhysics.Im, this);
        Position = new f64Vector3(fix64.Zero, World.Ground - GroundAt, fix64.Zero);
        Rotation = f64Euler.Identity;
    }

    [PluginVisible]
    public void Fix()
    {
        if (HasLeftRace)

            return;
        _fixing = true;
        Fixed?.Invoke();
    }

    [PluginVisible]
    public void RespawnInPlace()
    {
        if (!Wasted || HasLeftRace)

            return;
        CarPhysics.Reseto(CarPhysics.Im, this);
        CarPhysics.FinishedFix();
        Position = new f64Vector3(Position.X, fix64.Min(Position.Y, World.Ground - GroundAt), Position.Z);
        Rotation = new f64Euler(Rotation.Xz, f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
        _fixing = false;
        _fixTick = _fixTimer = 0;
        Fixed?.Invoke();
    }

    /// <summary>
    /// Generates dust at the given position and velocity
    /// </summary>
    /// <param name="wheelidx"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="scx"></param>
    /// <param name="scz"></param>
    /// <param name="simag"></param>
    /// <param name="tilt"></param>
    /// <param name="onRoof"></param>
    /// <param name="wheelGround"></param>
    /// <param name="surface">The track-piece surface, or null for bare ground.</param>
    public void AddDust(
        int wheelidx,
        float x,
        float y,
        float z,
        int scx,
        int scz,
        float simag,
        int tilt,
        bool onRoof,
        int wheelGround,
        SurfaceType? surface
    )
    {
        Dusted?.Invoke(wheelidx, x, y, z, scx, scz, simag, tilt, onRoof, wheelGround, surface);
    }

    /// <summary>
    /// Generates spark at the given position and velocity
    /// </summary>
    /// <param name="x">The X coordinate of the spark</param>
    /// <param name="y">The Y coordinate of the spark</param>
    /// <param name="z">The Z coordinate of the spark</param>
    /// <param name="scx">The X component of the spark's velocity</param>
    /// <param name="scy">The Y component of the spark's velocity</param>
    /// <param name="scz">The Z component of the spark's velocity</param>
    /// <param name="type">0 = wall, 1 = roof, 2 = player</param>
    /// <param name="wheelGround">The wheel ground</param>
    public void Spark(float x, float y, float z, float scx, float scy, float scz, int type, int wheelGround)
    {
        Sparked?.Invoke(x, y, z, scx, scy, scz, type, wheelGround);
    }

    /// <summary>
    /// Applies visual damage to the car on the X axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    public void DamageX(int wheelnum, fix64 amount)
    {
        DamagedX?.Invoke(Stats, wheelnum, amount);
    }

    /// <summary>
    /// Applies visual damage to the car on the Y axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    /// <param name="mtouch">Mtouch physics parameter</param>
    /// <param name="nbsq">Nbsq physics parameter</param>
    /// <param name="squash">Roof squash physics parameter</param>
    public void DamageY(int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash)
    {
        DamagedY?.Invoke(Stats, wheelnum, amount, mtouch, nbsq, squash);
    }

    /// <summary>
    /// Applies visual damage to the car on the Z axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    public void DamageZ(int wheelnum, fix64 amount)
    {
        DamagedZ?.Invoke(Stats, wheelnum, amount);
    }

    public static implicit operator ContO(BackendCar car) => new(car);
}

public delegate void DamageFunc(CarStats stat, int wheelnum, fix64 amount);

public delegate void RoofDamageFunc(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash);

public delegate void
    SparkFunc(
        float wheelx,
        float wheely,
        float wheelz,
        float scx,
        float scy,
        float scz,
        int type,
        int wheelGround
    );

public delegate void DustFunc(
    int wheelidx,
    float wheelx,
    float wheely,
    float wheelz,
    int scx,
    int scz,
    float simag,
    int tilt,
    bool onRoof,
    int wheelGround,
    SurfaceType? surface
);
