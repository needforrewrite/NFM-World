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
[UsedImplicitly]
public sealed partial class ClassicAi(ClientSidePlayer player, string aiId, BaseClientGamemode gamemode)
    : PluginAi(player, aiId, gamemode)
{
    // The original's difficulty knob, 1..5. Free play has no equivalent, so this is fixed at
    // the original's default.
    private const int AiStage = 1;

    private readonly DriverState _driver = new();
    private readonly DeterministicRandom _random = new(0x5EED_0000_0000_0001UL);

    // The original ticks at 21.4 Hz; this runs at 63. Only the bookkeeping counters are
    // gated, which is what the original's `tick.fraction >= tick.scale` did - the controls
    // themselves are written every tick in both.
    private int _tickCounter;

    private TrackPath? _path;
    private BackendStage? _pathStage;

    /// <summary>The checkpoint node last crossed, so a fresh one can release the path focus.</summary>
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
            _tickCounter = 0;

        var path = Path;
        if (path.Count == 0)
            return;

        // The standings are nobody's to own here: PvP never ranks the cars, so the bot does
        // it for the drivers that need it (itself and its rival) without touching the HUD.
        RankCars();

        var mad = car.CarPhysics;

        // Crossing a checkpoint releases the hold the path walk puts on the last one. Without
        // this the focus latches on the first checkpoint the bot ever falls behind, and it
        // just circles the thing forever.
        if (car.LastCheckpointNode != _lastCheckpointNode)
        {
            _lastCheckpointNode = car.LastCheckpointNode;
            _driver.Focus = -1;
        }

        var nofocus = _nofocus;
        _nofocus = false;
        var nearest = path.NearestPoint(car, nofocus);
        var cleared = path.PathIndex(car.LastCheckpointNode);
        _driver.Point = path.PointAhead(nearest, cleared, car, nofocus, ref _driver.Focus, ref _driver.Missed);

        if (mad.Mtouch)
        {
            if (_driver.TicksSinceRethink > _driver.RethinkAfter)
                Rethink(car);
            else if (_originalTick)
                _driver.TicksSinceRethink++;
        }

        // Mtouch only turns true after ten ticks of wheel contact, so a car that has never
        // settled reads as airborne - which would count as a take-off on the start line and
        // unlock attacking before the race has begun. Until it lands once, assume it is on
        // the ground, as the original does when it spawns its cars.
        if (mad.Mtouch)
            _settled = true;
        var ride = _driver.GroundByWheels ? mad.Wtouch : (mad.Mtouch || !_settled);
        if (ride)
        {
            if (_driver.StuntPhase != StuntPhase.Takeoff)
                _driver.StuntPhase = StuntPhase.Takeoff;

            if (_driver.Fixing is Fix.Nearest or Fix.Close)
                _driver.AttackTicks = 0;

            var point = _driver.Point;
            if (_driver.AttackTicks == 0)
            {
                WaitAtStart(control);
                point = ChoosePathPoint(car, point);
                point = HeadForFix(car, point);
                AimAtPathPoint(car, point);
            }
            else
            {
                ChaseTarget(car);
            }

            SteerToAim(car);
            BounceOffWalls(car);
        }
        else
        {
            FlyStunts(car);
        }
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
            }

            return _path;
        }
    }

    /// <summary>Set when the driver drops its focus on the path (repairing, most often).</summary>
    private bool _nofocus;

    // ---- shared helpers -------------------------------------------------------------

    private fix64 Sim() => _random.NextFixed6401();

    private static fix64 DistanceSquared(fix64 x1, fix64 z1, fix64 x2, fix64 z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return (dx * dx) + (dz * dz);
    }

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

    /// <summary>
    /// The heading that points from the car at (x, z), in the same convention as
    /// <see cref="Heading"/>.
    /// </summary>
    private static int HeadingTo(BackendCar car, fix64 x, fix64 z)
    {
        // The original's `90 + (dx >= 0 ? 180 : 0) + atan(dz / dx)` with an unguarded
        // division: the same angle, minus the division by zero when the target is straight
        // ahead. Matches the aim convention ElStupido uses.
        var angle = FixedMath.Atan2(z - car.Position.Z, x - car.Position.X) * ((fix64)180 / FixedMath.PI);
        return (int)FixedMath.Floor(angle - (fix64)90);
    }

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

    // ---- start line, steering, walls ------------------------------------------------

    /// <summary>Rolls the gas on a cycle at the start, and backs up while held back.</summary>
    private void WaitAtStart(Control control)
    {
        if (_driver.GasCycle < 30)
        {
            if (_driver.ReverseTicks <= 0)
            {
                control.Up = true;
            }
            else
            {
                control.Down = true;
                if (_originalTick)
                    _driver.ReverseTicks--;
            }
        }

        if (_driver.GasCycle < 25 + _driver.HoldBackNow)
        {
            if (_originalTick)
                _driver.GasCycle++;
        }
        else
        {
            _driver.GasCycle = 0;
            _driver.HoldBackNow = _driver.HoldBack;
        }
    }

    /// <summary>Turns toward the aim heading, shedding speed in turns that are too sharp.</summary>
    private void SteerToAim(BackendCar car)
    {
        var control = car.Control;
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

        if (abs > (fix64)50 && car.CarPhysics.Speed > car.CarPhysics.Stat.Swits.X
            && _driver.SharpTurn != SharpTurn.Steer)
        {
            switch (_driver.SharpTurn)
            {
                case SharpTurn.Brake:
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

    /// <summary>Turns away from a wall and freezes the steering for a moment.</summary>
    private void BounceOffWalls(BackendCar car)
    {
        var control = car.Control;
        if (control.Wall != -1)
        {
            if (_driver.LastWall != control.Wall)
            {
                if (_driver.LastTurnedLeft)
                    control.Left = true;
                else
                    control.Right = true;

                _driver.WallTurnLeft = _driver.LastTurnedLeft;
                _driver.LastWall = control.Wall;
            }
            else
            {
                if (_driver.WallTurnLeft)
                    control.Left = true;
                else
                    control.Right = true;
            }

            // shortcut: the original scales the freeze by the wall piece's damage rating; the
            // plugin only sees a placeholder index, so every wall is a light one.
            _driver.SteerFreeze += 1;
            if (_driver.SteerFreeze > 10)
                _driver.SteerFreeze = 10;
        }
        else
        {
            if (_driver.SteerFreeze != 0)
                _driver.SteerFreeze--;
        }
    }
}
