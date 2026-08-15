using Lua;
using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary;

[LuaVisible]
public partial interface IInGameCar : ICar
{
    [LuaName("carPhysics")]
    CarPhysics CarPhysics { get; }
    
    [LuaName("control")]
    Control Control { get; }
    
    [LuaName("currentCheckpoint")]
    ushort CurrentCheckpoint { get; set; }
    
    [LuaName("nlaps")]
    byte CurrentLap { get; set; } // mad.nlaps
    
    [LuaName("clear")]
    int TotalCheckpoint { get; set; } // mad.clear
    
    [LuaName("lastCheckpointNode")]
    int LastCheckpointNode { get; set; } // resets on new lap
    
    [LuaName("placement")]
    int Placement { get; set; } // cp.pos
    
    [LuaName("wasted")]
    bool Wasted { get; }
    
    [LuaName("player")]
    ClientSidePlayerParameters Player { get; }

    [LuaHidden]
    public event DamageFunc? DamagedX;
    
    [LuaHidden]
    public event RoofDamageFunc? DamagedY;
    
    [LuaHidden]
    public event DamageFunc? DamagedZ;
    
    [LuaHidden]
    public event SparkFunc? Sparked;
    
    [LuaHidden]
    public event DustFunc? Dusted;
    
    [LuaHidden]
    public event Action? Fixed;

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
    void AddDust(int wheelidx, float x, float y, float z, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround);
    
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
    void Spark(float x, float y, float z, float scx, float scy, float scz, int type, int wheelGround);
    
    /// <summary>
    /// Applies visual damage to the car on the X axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    void DamageX(int wheelnum, fix64 amount);

    /// <summary>
    /// Applies visual damage to the car on the Y axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    /// <param name="mtouch">Mtouch physics parameter</param>
    /// <param name="nbsq">Nbsq physics parameter</param>
    /// <param name="squash">Roof squash physics parameter</param>
    void DamageY(int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash);
    
    /// <summary>
    /// Applies visual damage to the car on the Z axis.
    /// </summary>
    /// <param name="wheelnum">The wheel index that the damage originates from</param>
    /// <param name="amount">The amount of damage in hit points</param>
    void DamageZ(int wheelnum, fix64 amount);
    
    void Drive(IStage stage);
    void Collide(IInGameCar otherCar);
    void ResetPosition();
    
    void Fix();
}