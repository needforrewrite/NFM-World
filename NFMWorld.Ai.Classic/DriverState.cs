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

    public bool IgnoreMissed;

    /// <summary>Ticks it keeps its steering after hitting a wall.</summary>
    public int SteerFreeze;

    /// <summary>It decides anew how to drive after this many ticks on the ground.</summary>
    public int RethinkAfter;

    public int TicksSinceRethink;

    /// <summary>Degrees off its aim it does not steer for.</summary>
    public fix64 SteerTolerance = (fix64)4;

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

    public bool LastTurnedLeft;

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

    /// <summary>The path point it aims at.</summary>
    public int Point;

    /// <summary>The path point nearest the car, tracked between ticks.</summary>
    public int PathIndex = -1;

    /// <summary>Set when the next gate is behind the car: it drives back to it.</summary>
    public bool Missed;

    /// <summary>This tick's target is a fix approach/ramp, not the racing line.</summary>
    public bool TargetsFix;

    /// <summary>Whether the tracker was inside a fix lane on the previous tick.</summary>
    public bool WasInFixBlock;

    /// <summary>The driver deliberately entered a `)po` lane and is following it.</summary>
    public bool FixLaneActive;

    /// <summary>+1 follows a fix lane forward, -1 enters from the closing marker.</summary>
    public int FixDirection = 1;

    /// <summary>Original frames to wait after leaving a fix lane before seeking another.</summary>
    public int FixCooldown;

    /// <summary>Progress counters for the stuck check.</summary>
    public int SampleTicks;
    public int StuckTicks;
    public fix64 LastSampleX;
    public fix64 LastSampleZ;
    public fix64 LastHeading;

    /// <summary>Ticks left backing out of an obstacle, and which way it steers while doing so.</summary>
    public int UnstickTicks;
    public bool UnstickLeft;
    public int RecoveryAttempts;

    /// <summary>Samples of clean movement since the last stall; recovery attempts are forgotten after a while.</summary>
    public int GoodSamples;
}
