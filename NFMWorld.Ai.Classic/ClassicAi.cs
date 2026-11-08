using FixedMathSharp;
using JetBrains.Annotations;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Plugins;

[assembly: Ai(Name = "Classic", Description = "The original game's computer driver.")]

namespace NFMWorld.Ai.Classic;

/// <summary>
/// Port of the original game's driver AI (ezBeanie/need-for-madness <c>src/race/ai.js</c>),
/// with the per-stage and per-car branches left out: those are keyed on a campaign stage
/// number and a car type, neither of which free play has.
/// </summary>
/// <remarks>
/// The original's rubber-banding deliberately makes a leading bot drive badly: it coasts
/// with the gas off and stops re-aiming for seconds at a time. That is what a campaign
/// stage wants and what free play does not, so the generic driver here keeps the original's
/// ramps, repairs, stunts and corner braking but aims every tick, never coasts on purpose,
/// and backs out of anything it has been sitting against.
/// </remarks>
[UsedImplicitly]
public sealed partial class ClassicAi(ClientSidePlayer player, string aiId, BaseClientGamemode gamemode)
    : PluginAi(player, aiId, gamemode)
{
    /// <summary>How far ahead of its last point the nearest-point search runs.</summary>
    private const int PathWindow = 4;

    /// <summary>Distance at which a fix-lane node counts as reached (scaled by 10).</summary>
    private const int FixLaneCaptureSquared = 90_000;

    /// <summary>Distance at which the local tracker gives up and searches the whole path.</summary>
    private const int FarFromPathSquared = 250_000;

    /// <summary>Ticks between stuck samples, and samples without movement before backing out.</summary>
    private const int StuckSampleTicks = 15;

    private const int StuckSamplesToUnstick = 4;

    /// <summary>Distance the car must cover in one sample to count as moving (scaled by 10).</summary>
    private const int StuckMoveSquared = 64;

    private const int UnstickTicks = 40;

    private readonly DriverState _driver = new();
    private DeterministicRandom _random = new(0x5EED_0000_0000_0001UL);

    // The original ticks at 21.4 Hz; this runs at 63. Only the bookkeeping counters are
    // gated, which is what the original's `tick.fraction >= tick.scale` did - the controls
    // themselves are written every tick in both.
    private int _tickCounter;

    private TrackPath? _path;
    private BackendStage? _pathStage;

    /// <summary>The checkpoint node last crossed, so a fresh one releases a repair detour.</summary>
    private int _lastCheckpointNode = -1;

    /// <summary>Whether the car has settled on its wheels at least once since spawning.</summary>
    private bool _settled;

    /// <summary>True on the ticks the original would have advanced its per-tick counters.</summary>
    private bool _originalTick;

    public override void RunAi()
    {
        if (Car is not { } car)
            return;

        var control = car.Control;
        control.Up = false;
        control.Down = false;
        control.Left = false;
        control.Right = false;
        control.Handb = false;

        if (car.Wasted)
            return;

        _originalTick = ++_tickCounter >= Physics.OriginalTicksPerNewTick;
        if (_originalTick)
        {
            _tickCounter = 0;
            if (_driver.FixCooldown > 0)
                _driver.FixCooldown--;
        }

        var path = Path;
        if (path.Count == 0)
            return;

        var mad = car.CarPhysics;

        // The standings are nobody's to own here: PvP never ranks the cars, so the bot does
        // it for the drivers that need it (itself and its rival) without touching the HUD.
        if (_originalTick)
            RankCars();

        // A checkpoint crossing ends a repair detour and releases the "drive back to the
        // gate" state. Comparing the node rather than the ordinal keeps it correct across a
        // lap boundary, where the ordinal wraps to zero.
        if (car.LastCheckpointNode != _lastCheckpointNode)
        {
            _lastCheckpointNode = car.LastCheckpointNode;
            _driver.IgnoreMissed = false;
            _driver.Missed = false;
        }

        // Mtouch only turns true after ten ticks of wheel contact, so a car that has never
        // settled reads as airborne - which would count as a take-off on the start line and
        // unlock attacking before the race has begun. Until it lands once, assume it is on
        // the ground, as the original does when it spawns its cars.
        if (mad.Mtouch)
            _settled = true;

        var ride = _driver.GroundByWheels ? mad.Wtouch : (mad.Mtouch || !_settled);

        TrackPathPoint(car, path);

        if (!ride)
        {
            TrackStuck(car, mad, grounded: false);
            FlyStunts(car);
            return;
        }

        if (_driver.StuntPhase != StuntPhase.Takeoff)
            _driver.StuntPhase = StuntPhase.Takeoff;

        if (mad.Mtouch)
        {
            if (_driver.UnstickTicks > 0)
            {
                DriveUnstuck(car);
                return;
            }

            if (_driver.TicksSinceRethink > _driver.RethinkAfter)
                Rethink(car);
            else if (_originalTick)
                _driver.TicksSinceRethink++;

            TrackStuck(car, mad, grounded: true);
            if (_driver.UnstickTicks > 0)
            {
                DriveUnstuck(car);
                return;
            }
        }

        if (_driver.Fixing is Fix.Nearest or Fix.Close)
            _driver.AttackTicks = 0;

        _driver.TargetsFix = false;

        var point = _driver.Point;
        if (_driver.AttackTicks == 0)
        {
            point = ChoosePathPoint(car, point);
            point = HeadForFix(car, point);
            _driver.Point = point;
            AimAtPathPoint(car, point);
        }
        else
        {
            ChaseTarget(car);
        }

        control.Up = true;
        SteerToAim(car);
        BounceOffWalls(car);
    }

    /// <summary>The stage's path points, rebuilt when the gamemode swaps stages.</summary>
    private TrackPath Path
    {
        get
        {
            var stage = CurrentStage;
            if (_path is null || !ReferenceEquals(_pathStage, stage))
            {
                _path = new TrackPath(stage);
                _pathStage = stage;

                _driver.PathIndex = -1;
                _driver.Point = 0;
                _driver.Missed = false;
                _driver.UnstickTicks = 0;
                _driver.StuckTicks = 0;
                _driver.SampleTicks = 0;
                _driver.LastSampleX = fix64.Zero;
                _driver.LastSampleZ = fix64.Zero;
                _driver.LastHeading = fix64.Zero;
                _driver.WasInFixBlock = false;
                _driver.FixCooldown = 0;
                _driver.FixDirection = 1;
                _driver.FixLaneActive = false;
            }

            return _path;
        }
    }

    // ---- shared helpers -------------------------------------------------------------

    private fix64 Sim() => _random.NextFixed6401();

    private static fix64 DistanceSquared10(fix64 x1, fix64 z1, fix64 x2, fix64 z2)
    {
        var dx = (x1 - x2) / (fix64)10;
        var dz = (z1 - z2) / (fix64)10;
        return (dx * dx) + (dz * dz);
    }

    private static fix64 Distance10(fix64 x1, fix64 z1, fix64 x2, fix64 z2)
        => FixedMath.Sqrt(DistanceSquared10(x1, z1, x2, z2));

    /// <summary>The car's heading in degrees, with the inverted axis folded in.</summary>
    private static fix64 Heading(BackendCar car)
    {
        var heading = car.Rotation.Xz.Degrees;
        if (car.Control.Zyinv)
            heading += (fix64)180;
        return Normalize(heading);
    }

    /// <summary>Folds an angle into (-180, 180].</summary>
    private static fix64 Normalize(fix64 degrees)
    {
        while (degrees < (fix64)(-180))
            degrees += (fix64)360;
        while (degrees > (fix64)180)
            degrees -= (fix64)360;
        return degrees;
    }

    /// <summary>The angle of a direction vector, in the same convention as <see cref="Heading"/>.</summary>
    private static fix64 Direction(fix64 dx, fix64 dz)
        => FixedMath.Atan2(dz, dx) * ((fix64)180 / FixedMath.PI);

    /// <summary>
    /// The heading that points from the car at (x, z), in the same convention as
    /// <see cref="Heading"/>.
    /// </summary>
    private static int HeadingTo(BackendCar car, fix64 x, fix64 z)
        => (int)FixedMath.Floor(Direction(x - car.Position.X, z - car.Position.Z) - (fix64)90);

    /// <summary>The human the bot measures itself against, or itself if it is racing alone.</summary>
    private BackendCar RivalCar(BackendCar car)
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var candidate = Players[i];
            if (!candidate.Info.IsBot && !candidate.IsFake && candidate.Car is { } rivalCar)
                return rivalCar;
        }

        return car;
    }

    /// <summary>The car in slot <paramref name="im"/>, or null.</summary>
    private BackendCar? CarAt(int im)
    {
        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is { } candidate && candidate.CarPhysics.Im == im)
                return candidate;
        }

        return null;
    }

    private int RacersWithCars()
    {
        var count = 0;
        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is { })
                count++;
        }

        return count;
    }

    // ---- path tracking --------------------------------------------------------------

    /// <summary>Keeps the followed point on the piece of path the car is actually on.</summary>
    private void TrackPathPoint(BackendCar car, TrackPath path)
    {
        var inFix = _driver.Fixing != Fix.None && path.InFixBlock(_driver.PathIndex);

        if (_driver.PathIndex < 0 || _driver.PathIndex >= path.Count)
        {
            _driver.PathIndex = path.NearestAnywhere(car);
        }
        else
        {
            var nearest = path.NearestInWindow(car, _driver.PathIndex, PathWindow);

            if (inFix)
            {
                // Hold the current lane node until the car actually reaches it. Advancing
                // one index per tick from a forced jump raced through the whole lane in a
                // few ticks and then dropped the repair before the car got there.
                var currentDistance = DistanceSquared10(
                    car.Position.X,
                    car.Position.Z,
                    path.X(_driver.PathIndex),
                    path.Z(_driver.PathIndex)
                );

                nearest = currentDistance > (fix64)FixLaneCaptureSquared
                    ? _driver.PathIndex
                    : path.Step(_driver.PathIndex, _driver.FixDirection);
            }

            var distance = DistanceSquared10(car.Position.X, car.Position.Z, path.X(nearest), path.Z(nearest));
            _driver.PathIndex = inFix || distance <= (fix64)FarFromPathSquared
                ? nearest
                : path.NearestAnywhere(car);
        }

        // The tracked point being just past the gate in route order means the car physically
        // passed it without clearing it. Watching "distance to the gate" instead would call
        // a normal layout a miss whenever the next gate sits more than half a lap away, and
        // a car that has just gone past would look the same as one still approaching.
        //
        // Once set, `Missed` stays set until the gate actually clears, which is what lets
        // the driver start turning before the tracked point catches up again.
        if (path.CheckpointCount > 0 && !_driver.Missed)
        {
            var checkpoint = path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount);
            var past = path.ForwardDistance(checkpoint, _driver.PathIndex);
            var margin = Math.Max(1, Math.Min(3, path.Count / 4));
            if (past > 0 && past <= margin)
                _driver.Missed = true;
        }

        var nowInFix = _driver.Fixing != Fix.None && path.InFixBlock(_driver.PathIndex);
        if (_driver.WasInFixBlock && !nowInFix)
        {
            _driver.Fixing = Fix.None;
            _driver.FixCooldown = 120;
            _driver.FixDirection = 1;
            _driver.FixLaneActive = false;
        }

        _driver.WasInFixBlock = nowInFix;

        _driver.Point = nowInFix
            ? path.Step(_driver.PathIndex, _driver.FixDirection)
            : path.AdvanceNormal(_driver.PathIndex, 1);
    }

    /// <summary>Keeps a point on the near side of the next gate.</summary>
    private int ClampToCheckpoint(BackendCar car, int point)
    {
        var path = Path;
        if (_driver.Missed || path.CheckpointCount == 0)
            return point;

        var checkpoint = path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount);
        var toPoint = path.ForwardDistance(_driver.PathIndex, point);
        var toCheckpoint = path.ForwardDistance(_driver.PathIndex, checkpoint);
        return toPoint > toCheckpoint ? checkpoint : point;
    }

    /// <summary>Points the nose at a path point every tick, skipping one it is already on.</summary>
    private void AimAtPathPoint(BackendCar car, int point)
    {
        var path = Path;
        if (path.Count == 0)
            return;

        if (point < 0 || point >= path.Count)
            point = _driver.PathIndex;

        // Turn in before the point, not on top of it: at full speed the car needs most of a
        // second to change line, and the nodes are a whole stage piece apart. A checkpoint
        // is never skipped, though: on stages like Snake Dance the gate sits laterally off
        // the `)p` line, and aiming at the next road point drives straight past it. While
        // heading back to a missed gate the point must not be skipped either.
        var speed = FixedMath.Abs(car.CarPhysics.Speed);
        var turnIn = (fix64)1200 + (speed * (fix64)6);
        var turnInSquared = (turnIn / (fix64)10) * (turnIn / (fix64)10);
        if (!_driver.Missed
            && !_driver.TargetsFix
            && path.Typ(point) <= 0
            && path.Count > 1
            && DistanceSquared10(car.Position.X, car.Position.Z, path.X(point), path.Z(point)) < turnInSquared)
            point = path.Advance(point, 1);

        _driver.AimHeading = HeadingTo(car, path.X(point), path.Z(point));
    }

    // ---- stuck recovery -------------------------------------------------------------

    /// <summary>Watches the car's position and starts backing out when it stops moving.</summary>
    private void TrackStuck(BackendCar car, CarPhysics mad, bool grounded)
    {
        if (!grounded || mad.Wasted)
        {
            _driver.SampleTicks = 0;
            _driver.StuckTicks = 0;
            _driver.LastSampleX = car.Position.X;
            _driver.LastSampleZ = car.Position.Z;
            return;
        }

        if (++_driver.SampleTicks < StuckSampleTicks)
            return;

        _driver.SampleTicks = 0;
        var moved = DistanceSquared10(car.Position.X, car.Position.Z, _driver.LastSampleX, _driver.LastSampleZ);
        _driver.LastSampleX = car.Position.X;
        _driver.LastSampleZ = car.Position.Z;

        // A car swinging around (a missed gate, a U-turn) covers little ground while its
        // heading changes. That is not stuck, and reversing out of it is wrong.
        var heading = Heading(car);
        var turned = FixedMath.Abs(Normalize(heading - _driver.LastHeading));
        _driver.LastHeading = heading;

        if (moved >= (fix64)StuckMoveSquared || turned > (fix64)8)
        {
            _driver.StuckTicks = 0;

            // Only forget the attempts after a stretch of clean driving, so a car that
            // keeps returning to the same obstacle alternates its escape instead of
            // repeating the first one every time.
            if (++_driver.GoodSamples >= 8)
            {
                _driver.GoodSamples = 0;
                _driver.RecoveryAttempts = 0;
            }

            return;
        }

        _driver.GoodSamples = 0;
        if (++_driver.StuckTicks < StuckSamplesToUnstick)
            return;

        _driver.StuckTicks = 0;
        _driver.UnstickTicks = UnstickTicks + (_driver.RecoveryAttempts * 20);
        if (_driver.UnstickTicks > UnstickTicks * 3)
            _driver.UnstickTicks = UnstickTicks * 3;

        // Steer toward the aim if the nose is pointing away from it; otherwise alternate so
        // repeated attempts do not keep grinding into the same corner.
        var off = Normalize(Heading(car) - _driver.AimHeading);
        _driver.UnstickLeft = FixedMath.Abs(off) > (fix64)20 ? off < fix64.Zero : _driver.RecoveryAttempts % 2 == 0;
        _driver.RecoveryAttempts++;
    }

    /// <summary>Backs out of whatever the car is sitting against, then resumes the path.</summary>
    private void DriveUnstuck(BackendCar car)
    {
        var control = car.Control;
        control.Down = true;
        control.Up = false;

        if (_driver.UnstickLeft)
            control.Left = true;
        else
            control.Right = true;

        if (--_driver.UnstickTicks <= 0)
        {
            _driver.UnstickTicks = 0;
            _driver.StuckTicks = 0;
        }
    }

    // ---- steering, walls ------------------------------------------------------------

    /// <summary>Turns toward the aim heading, shedding speed for the corner ahead.</summary>
    private void SteerToAim(BackendCar car)
    {
        var control = car.Control;
        var mad = car.CarPhysics;
        var off = Normalize(Heading(car) - _driver.AimHeading);

        if (control.Wall != -1 && _driver.SteerFreeze == 0)
            _driver.SteerTolerance = fix64.Zero;

        var abs = FixedMath.Abs(off);
        if (_driver.SteerFreeze != 0 || abs < _driver.SteerTolerance)
            return;

        if (off < fix64.Zero)
        {
            control.Left = true;
            _driver.LastTurnedLeft = true;
        }
        else
        {
            control.Right = true;
            _driver.LastTurnedLeft = false;
        }

        if (abs > (fix64)50 && mad.Speed > mad.Stat.Swits.X && _driver.SharpTurn != SharpTurn.Steer)
        {
            switch (_driver.SharpTurn)
            {
                case SharpTurn.Brake:
                    // Full brake only for a hairpin. Dropping to walking pace at a right
                    // angle makes the car turn on the spot and then drive wide.
                    if (abs > (fix64)80)
                        control.Down = true;
                    break;
                case SharpTurn.Handbrake:
                    control.Handb = true;
                    break;
            }

            if (!_driver.GasInTurns)
                control.Up = false;
        }
    }

    /// <summary>Steers away from a wall, toward the aim when it can, and briefly freezes.</summary>
    private void BounceOffWalls(BackendCar car)
    {
        var control = car.Control;
        if (control.Wall == -1)
        {
            if (_driver.SteerFreeze != 0)
                _driver.SteerFreeze--;
            return;
        }

        // Steer toward the aim rather than blindly away from the wall: if the nose is
        // already off-line, the normal direction is what leaves the obstacle behind.
        var off = Normalize(Heading(car) - _driver.AimHeading);
        if (FixedMath.Abs(off) > _driver.SteerTolerance)
        {
            if (off < fix64.Zero)
            {
                control.Left = true;
                control.Right = false;
                _driver.LastTurnedLeft = true;
            }
            else
            {
                control.Right = true;
                control.Left = false;
                _driver.LastTurnedLeft = false;
            }
        }
        else if (_driver.LastTurnedLeft)
        {
            control.Left = true;
        }
        else
        {
            control.Right = true;
        }

        _driver.SteerFreeze++;
        if (_driver.SteerFreeze > 3)
            _driver.SteerFreeze = 3;
    }
}
