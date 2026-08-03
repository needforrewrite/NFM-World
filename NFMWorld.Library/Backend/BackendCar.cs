using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorld.Sentry;

namespace NFMWorldLibrary.Backend;

public class BackendCar : BackendGameObject, IInGameCar
{
    public int GroundAt { get; }
    public int MaxRadius { get; }
    public f64Euler WheelAngle { get; set; }
    public f64Euler TurningWheelAngle { get; set; }
    public IReadOnlyList<Rad3dWheelDef> Wheels { get; }

    public CarPhysics CarPhysics { get; }
    public Control Control { get; }
    public ushort CurrentCheckpoint { get; set; }
    public byte CurrentLap { get; set; } // mad.nlaps
    public int TotalCheckpoint { get; set; } // mad.clear
    public int LastCheckpointNode { get; set; } = -1; // resets on new lap
    public int Placement { get; set; } // cp.pos
    public Rad3d Rad { get; }
    public CarStats Stats { get; }
    public bool Wasted => CarPhysics.Wasted;

    public BaseAi? Bot { get; set; }

    public event DamageFunc? DamagedX;
    public event RoofDamageFunc? DamagedY;
    public event DamageFunc? DamagedZ;
    public event SparkFunc? Sparked;
    public event DustFunc? Dusted;
    public event Action? Fixed;

    public PlayerParameters Player { get; }
    
    private bool _fixing;
    private byte _fixTimer;
    private int _fixTick = 0;

    public BackendCar(
        IInGameCar other,
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

    public BackendCar(PlayerParameters player, int im, fix64 x, fix64 z) : this(BackendGameSparker.GetCar(player.CarName).Rad!, im, x, z, player.IsClientPlayer)
    {
        Player = player;
    }

    public BackendCar(Rad3d rad, int im, fix64 x, fix64 z, bool isClientPlayer)
    {
        Rad = rad;
        Stats = CarStats.ValidateStats(rad.Stats, "hogan rewish");

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        MaxRadius = rad.MaxRadius;
        Wheels = rad.Wheels;
        
        CarPhysics = new CarPhysics(Stats, im, isClientPlayer);
        CarPhysics.Reseto(CarPhysics.Im, this);
        Control = new Control();
        
        Position = new f64Vector3(x, World.Ground - GroundAt, z);
        Rotation = f64Euler.Identity;
        
        Player = new PlayerParameters
        {
            CarName = rad.FileName,
            IsClientPlayer = false,
            Color = new Color3(255, 255, 255),
            IsBot = false,
            PlayerName = "hogan rewish"
        };
    }

    public void Drive(IStage stage)
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

    public void Collide(IInGameCar otherCar)
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

    public void AddDust(int wheelidx, float x, float y, float z, int scx, int scz, float simag, int tilt,
        bool onRoof, int wheelGround)
    {
        Dusted?.Invoke(wheelidx, x, y, z, scx, scz, simag, tilt, onRoof, wheelGround);
    }

    public void Spark(float x, float y, float z, float scx, float scy, float scz, int type, int wheelGround)
    {
        Sparked?.Invoke(x, y, z, scx, scy, scz, type, wheelGround);
    }

    public void DamageX(int wheelnum, fix64 amount)
    {
        DamagedX?.Invoke(Stats, wheelnum, amount);
    }

    public void DamageY(int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash)
    {
        DamagedY?.Invoke(Stats, wheelnum, amount, mtouch, nbsq, squash);
    }

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