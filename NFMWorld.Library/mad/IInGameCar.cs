using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Mad;

public interface IInGameCar : ICar
{
    Mad Mad { get; }
    Control Control { get; }
    ushort currentCheckpoint { get; set; }
    byte currentLap { get; set; } // mad.nlaps
    int totalCheckpoint { get; set; } // mad.clear
    int lastCheckpointNode { get; set; } // resets on new lap
    int placement { get; set; } // cp.pos
    bool Wasted { get; }
    BaseAi? Bot { get; set; }

    public event DamageFunc? DamagedX;
    public event RoofDamageFunc? DamagedY;
    public event DamageFunc? DamagedZ;
    public event SparkFunc? Sparked;
    public event DustFunc? Dusted;

    void AddDust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround);
    void Spark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround);
    void DamageX(CarStats stat, int wheelnum, fix64 amount);
    void DamageY(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash);
    void DamageZ(CarStats stat, int wheelnum, fix64 amount);
    
    void Drive(IStage stage);
    void Collide(IInGameCar otherCar);
    void ResetPosition();
}