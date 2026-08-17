using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorld.Sentry;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

[LuaVisible]
public partial class BackendCar : BackendGameObject
{
    [LuaName] public int GroundAt { get; }
    [LuaName] public int MaxRadius { get; }
    [LuaName] public f64Euler WheelAngle { get; set; }
    [LuaName] public f64Euler TurningWheelAngle { get; set; }
    [LuaName] public ReadOnlyLuaArray<Rad3dWheelDef> Wheels { get; }

    [LuaName] public CarPhysics CarPhysics { get; }
    [LuaName] public Control Control { get; }
    [LuaName] public ushort CurrentCheckpoint { get; set; }
    [LuaName] public byte CurrentLap { get; set; } // mad.nlaps
    [LuaName] public int TotalCheckpoint { get; set; } // mad.clear
    [LuaName] public int LastCheckpointNode { get; set; } = -1; // resets on new lap
    [LuaName] public int Placement { get; set; } // cp.pos
    [LuaName] public Rad3d Rad { get; }
    [LuaName] public CarStats Stats { get; }
    [LuaName] public bool Wasted => CarPhysics.Wasted;

    public event DamageFunc? DamagedX;
    public event RoofDamageFunc? DamagedY;
    public event DamageFunc? DamagedZ;
    public event SparkFunc? Sparked;
    public event DustFunc? Dusted;
    public event Action? Fixed;

    [LuaName] public ClientSidePlayerInfo Player { get; }
    
    private bool _fixing;
    private byte _fixTimer;
    private int _fixTick = 0;
    
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

    public BackendCar(ClientSidePlayerInfo player, int im, fix64 x, fix64 z) : this(BackendGameSparker.GetCar(player.CarName).Rad!, im, x, z, player.IsClientPlayer)
    {
        Player = player;
    }

    public BackendCar(Rad3d rad, int im, fix64 x, fix64 z, bool isClientPlayer)
    {
        Rad = rad;
        Stats = CarStats.ValidateStats(rad.Stats, "hogan rewish");

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        MaxRadius = rad.MaxRadius;
        Wheels = new(rad.Wheels);
        
        CarPhysics = new CarPhysics(Stats, im, isClientPlayer);
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

    [LuaName]
    public void Drive(BackendStage stage)
    {
        var transaction = SentrySdk.StartTransaction("BackendCar.Drive", "drive-car");
        CarPhysics.Drive(Control, this, stage);
        transaction.Finish();

        IterateFix();
    }

    private void IterateFix()
    {
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

    public void Collide(BackendCar otherCar)
    {
        var transaction = SentrySdk.StartTransaction("BackendCar.Collide", "car-collide");
        CarPhysics.Collide(this, otherCar.CarPhysics, otherCar);
        transaction.Finish();
    }

    public void ResetPosition()
    {
        CarPhysics.Reseto(CarPhysics.Im, this);
        Position = new f64Vector3(fix64.Zero, World.Ground - GroundAt, fix64.Zero);
        Rotation = f64Euler.Identity;
    }

    public void Fix()
    {
        _fixing = true;
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
    public void AddDust(int wheelidx, float x, float y, float z, int scx, int scz, float simag, int tilt,
        bool onRoof, int wheelGround)
    {
        Dusted?.Invoke(wheelidx, x, y, z, scx, scz, simag, tilt, onRoof, wheelGround);
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
public delegate void SparkFunc(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround);
public delegate void DustFunc(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround);