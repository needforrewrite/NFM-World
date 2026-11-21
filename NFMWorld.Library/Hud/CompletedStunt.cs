using TightPack;

namespace NFMWorldLibrary.Hud;

/// <summary>The latest awarded stunt, retained after its one-tick physics counters clear.</summary>
[TightPackable]
[TightPackAllowSerialize]
public partial struct CompletedStunt
{
    public uint Sequence;
    public fix64 TotalStuntXz;
    public fix64 TotalStuntXy;
    public fix64 TotalStuntZy;
    public fix64 Powerup;
    public bool ForwardTabletop;
    public bool BackwardsTabletop;
    public bool RightTabletop;
    public bool AtMaxPower;

    internal static CompletedStunt Capture(CarPhysics physics, uint sequence = 0) => new()
    {
        Sequence = sequence,
        TotalStuntXz = physics.TotalStuntXz,
        TotalStuntXy = physics.TotalStuntXy,
        TotalStuntZy = physics.TotalStuntZy,
        Powerup = physics.Powerup,
        ForwardTabletop = physics.ForwardTabletop,
        BackwardsTabletop = physics.BackwardsTabletop,
        RightTabletop = physics.RightTabletop,
        AtMaxPower = physics.Power >= 98
    };
}
