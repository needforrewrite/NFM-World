namespace NFMWorld.Ai.Classic;

/// <summary>How a driver treats the ramps on its path.</summary>
internal static class Ramps
{
    public const int Avoid = -1;
    public const int Either = 0;
    public const int Take = 1;
}

/// <summary>A damaged driver heads for a fix hoop.</summary>
internal static class Fix
{
    public const int None = 0;
    public const int OnTheWay = 1;
    public const int Nearest = 2;
    public const int Close = 3;
    public const int Jumping = 4;
}

/// <summary>Where a driver is in a jump.</summary>
internal static class StuntPhase
{
    public const int None = -1;
    public const int Takeoff = 0;
    public const int Flipping = 1;
    public const int Landing = 2;
}

/// <summary>What a driver does in a sharp turn besides steering.</summary>
internal static class SharpTurn
{
    public const int Steer = 0;
    public const int Brake = 1;
    public const int Handbrake = 2;
    public const int LiftGas = 3;
}

/// <summary>
/// One driver's memory: what it is aiming at and how it likes to drive. The original kept
/// one per car slot in a flat array; everything here is per instance.
/// </summary>
internal sealed class DriverState
{
    /// <summary>Heading to steer to, in degrees (see <see cref="ClassicAi.HeadingTo"/>).</summary>
    public fix64 AimHeading;

    public int AttackTicks;
    public int AttackedCar;
    public bool Afta;

    public int Fixing;

    /// <summary>Up to how many ticks between aiming anew; sloppier drivers aim less often.</summary>
    public int AimLag;

    /// <summary>Ticks per gas cycle it rolls without gas, letting the player catch up.</summary>
    public int HoldBack;

    public bool IgnoreMissed;

    /// <summary>Ticks it keeps its steering after hitting a wall.</summary>
    public int SteerFreeze;

    /// <summary>It decides anew how to drive after this many ticks on the ground.</summary>
    public int RethinkAfter;

    public int TicksSinceRethink;

    /// <summary>Degrees off its aim it does not steer for.</summary>
    public fix64 SteerTolerance = (fix64)5;

    /// <summary>The chance of skipping path points (0..1).</summary>
    public fix64 CornerCutting = (fix64)1;

    public int RampChoice;
    public int TurnStyleTicks;

    /// <summary>Keeps the gas in sharp turns.</summary>
    public bool GasInTurns;

    public int SharpTurn;
    public fix64 LandingMargin = (fix64)30;

    /// <summary>The chance of not correcting a landing.</summary>
    public fix64 LandingSloppiness = (fix64)0.5;

    /// <summary>Fewer and later stunts.</summary>
    public fix64 StuntCaution = (fix64)0.5;

    public bool GroundByWheels;
    public bool LandsPerfectly;

    /// <summary>How far ahead of its victim it aims, in half their distance.</summary>
    public fix64 LeadAhead;

    public int StuntPhase;

    /// <summary>Ticks into the gas cycle, and the hold back of this cycle.</summary>
    public int GasCycle;

    public int HoldBackNow;

    /// <summary>Ticks it backs up at the start.</summary>
    public int ReverseTicks;

    public int TicksToNextAim;
    public int TicksSinceAim;

    public bool LastTurnedLeft;
    public int LastWall = -1;
    public bool WallTurnLeft;

    /// <summary>A stunt: its angles at take-off, ticks in the air, flip and roll (-1, 0, 1).</summary>
    public fix64 TakeoffXy;
    public fix64 TakeoffZy;
    public int AirTicks;
    public int Flip;
    public int Roll;
    public bool FlipReverses;
    public bool RollReverses;
    public int FlipAfter;
    public int RollAfter;

    /// <summary>Straightening up to land: what it pressed, and whether pitch and roll are done.</summary>
    public bool PressedUp;
    public bool PressedDown;
    public bool PressedLeft;
    public bool PressedRight;
    public bool RollSettled;
    public bool PitchSettled;
    public bool StraightenPitch;
    public bool StraightenRoll;
    public int LandingStep;

    /// <summary>The path point it follows, or -1 while it is holding for a checkpoint.</summary>
    public int Point;

    /// <summary>Set when it drops behind its checkpoint gate; cleared when it passes one.</summary>
    public int Focus = -1;

    /// <summary>
    /// The original's <c>missedCheckpoint</c>. A bot only ever sees "none" from the physics,
    /// but the path walk writes "following" while it has no focus and "started" once it has
    /// one, and repair is gated on the former.
    /// </summary>
    public bool Missed;
}
