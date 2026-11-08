using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Ai.Classic;

public sealed partial class ClassicAi
{
    /// <summary>Re-rolls how this driver likes to drive, every so often while grounded.</summary>
    private void Rethink(BackendCar car)
    {
        ChooseAimLag(car);
        ChooseHoldBack(car);
        ChooseCornerCutting(car);
        ChooseRamps(car);
        ChooseTurnStyle();
        ChooseLandingCare(car);
        ChooseStuntCaution(car);
        ChooseLandingHabits(car);
        ChooseWhomToAttack(car);
        ChooseWhetherToFix(car);

        _driver.TicksSinceRethink = 0;
        _driver.RethinkAfter = (int)FixedMath.Floor((fix64)20 * Sim());
    }

    /// <summary>Aims at a position, refreshing only every <see cref="DriverState.AimLag"/> ticks.</summary>
    private void AimAtPathPoint(BackendCar car, int point)
    {
        if (_driver.TicksSinceAim > _driver.TicksToNextAim)
        {
            var path = Path;
            _driver.AimHeading = HeadingTo(car, path.X(point), path.Z(point));
            _driver.TicksSinceAim = 0;
            _driver.TicksToNextAim = (int)FixedMath.Floor(_driver.AimLag * Sim());
        }
        else if (_originalTick)
        {
            _driver.TicksSinceAim++;
        }
    }

    /// <summary>The point to drive at, after ramps and corner cutting.</summary>
    /// <remarks>
    /// The original branches here between this and a "bully" route that hunts the player;
    /// only the racing route survives, so the condition that picked it is always true.
    /// </remarks>
    private int ChoosePathPoint(BackendCar car, int point)
    {
        point = TakeOrAvoidRamps(point);
        return CutCorners(car, point);
    }

    private int TakeOrAvoidRamps(int point)
    {
        var path = Path;
        if (_driver.RampChoice == Ramps.Take && path.Typ(point) <= 0)
        {
            var next = point + 1;
            if (next >= path.Count)
                next = 0;
            if (path.Typ(next) == TrackPath.Ramp)
                point = next;
        }

        if (_driver.RampChoice == Ramps.Avoid && path.Typ(point) == TrackPath.Ramp)
        {
            point++;
            if (point >= path.Count)
                point = 0;
        }

        return point;
    }

    /// <summary>Skips turns and already-taken points, more often for a driver further back.</summary>
    private int CutCorners(BackendCar car, int point)
    {
        var path = Path;

        // Two independent draws, as the original: the first cuts corners wholesale (only
        // while the car is within its checkpoint), the second only steps over turns.
        if (Sim() <= _driver.CornerCutting)
        {
            if (Sim() > _driver.CornerCutting)
            {
                while (path.Typ(point) == TrackPath.Turn)
                {
                    point++;
                    if (point >= path.Count)
                        point = 0;
                }
            }

            return point;
        }

        var target = point;
        var notPastCheckpoint = false;
        if (path.Typ(target) > 0)
            notPastCheckpoint = car.TotalCheckpoint != CheckpointsBefore(target) + (car.CurrentLap * Path.CountCheckpoints());

        // Bounded by the path length, and never past the checkpoint being chased: on a stage
        // whose points are all road (or whose next checkpoint the car has not reached) the
        // original's loop walks the whole lap and wraps onto point 0 - behind the car - which
        // makes the driver double back on itself every few ticks.
        var limit = CheckpointAhead(point);
        for (var hop = 0; hop < path.Count; hop++)
        {
            if (path.Typ(target) is not (TrackPath.Road or TrackPath.Turn or TrackPath.FixRoadStart) && !notPastCheckpoint)
                break;
            if (limit >= 0 && point == limit)
                break;

            point = target;
            target++;
            if (target >= path.Count)
                target = 0;

            notPastCheckpoint = false;
            if (path.Typ(target) > 0)
                notPastCheckpoint = car.TotalCheckpoint != CheckpointsBefore(target) + (car.CurrentLap * Path.CountCheckpoints());
        }

        return point;
    }

    /// <summary>How many checkpoints come before a path point.</summary>
    /// <summary>The next checkpoint at or after <paramref name="point"/>, or -1 if none.</summary>
    private int CheckpointAhead(int point)
    {
        var path = Path;
        for (var i = 0; i < path.Count; i++)
        {
            var index = (point + i) % path.Count;
            if (path.Typ(index) > 0)
                return index;
        }

        return -1;
    }

    private int CheckpointsBefore(int point)
    {
        var path = Path;
        var count = 0;
        for (var i = 0; i < point && i < path.Count; i++)
        {
            if (path.Typ(i) > 0)
                count++;
        }

        return count;
    }

    /// <summary>Detours to a fix hoop when the car is damaged, and pulls in when close.</summary>
    private int HeadForFix(BackendCar car, int point)
    {
        if (_driver.Fixing == Fix.None)
            return point;
        if (_driver.Missed && !_driver.IgnoreMissed && _driver.Fixing != Fix.Jumping)
            return point;

        var path = Path;
        if (path.FixApproach.Length == 0)
            return point;

        if (_driver.Fixing == Fix.Nearest)
        {
            var nearest = fix64.Zero;
            var nearestFix = 0;
            for (var i = 0; i < path.FixApproach.Length; i++)
            {
                var approach = path.FixApproach[i];
                var d = DistanceSquared10(car.Position.X, car.Position.Z, path.X(approach), path.Z(approach));
                if (d < nearest || nearest == fix64.Zero)
                {
                    nearest = d;
                    nearestFix = i;
                }
            }

            point = path.FixApproach[nearestFix];
            _driver.IgnoreMissed = path.FixIsSpecial[nearestFix];
        }

        for (var i = 0; i < path.FixApproach.Length; i++)
        {
            var approach = path.FixApproach[i];
            if (DistanceSquared10(car.Position.X, car.Position.Z, path.X(approach), path.Z(approach)) < (fix64)2000)
            {
                _driver.IgnoreMissed = false;
                _driver.HoldBackNow = 0;
                _driver.HoldBack = 0;
                _driver.SharpTurn = SharpTurn.Handbrake;
                _driver.TicksToNextAim = -1;
                _driver.AimLag = 0;
                _driver.RampChoice = Ramps.Either;
                _driver.Fixing = Fix.Close;
            }
        }

        if (_driver.Fixing == Fix.Close)
            _nofocus = true;

        return point;
    }

    /// <summary>Drives at an opponent it has decided to attack.</summary>
    private void ChaseTarget(BackendCar car)
    {
        var control = car.Control;
        control.Up = true;

        if (CarAt(_driver.AttackedCar) is not { } target)
        {
            _driver.AttackTicks = 0;
            return;
        }

        var distance = Distance10(car.Position.X, car.Position.Z, target.Position.X, target.Position.Z);
        var lead = (distance / (fix64)2) * _driver.LeadAhead;
        var travel = Heading(target) * (FixedMath.PI / (fix64)180);
        var aimX = target.Position.X - (lead * FixedMath.Sin(travel));
        var aimZ = target.Position.Z + (lead * FixedMath.Cos(travel));

        _driver.AimHeading = HeadingTo(car, aimX, aimZ);

        if (_originalTick)
            _driver.AttackTicks--;

        if (_driver.AttackTicks <= 0)
            _driver.AttackTicks = 0;

        if (target.Wasted)
            _driver.AttackTicks = 0;
    }

    // ---- personality -----------------------------------------------------------------

    private fix64 Rank(BackendCar car)
    {
        var others = RacersWithCars();
        return others > 1 ? ((fix64)car.Placement * (fix64)6) / (fix64)(others - 1) : fix64.Zero;
    }

    /// <summary>
    /// Ranks the drivers by checkpoints passed, then by distance to the next one.
    /// </summary>
    /// <remarks>
    /// The same comparison as <see cref="CheckPointHelper.CalculatePositions"/>, kept here
    /// rather than called: a bot sharing the original would also be steering the HUD's
    /// standings, which it has no business doing. Every bot writing the same permutation is
    /// harmless; every bot overwriting the race's order with its own view would not be.
    /// </remarks>
    private void RankCars()
    {
        var stage = CurrentStage;
        var nsp = Path.CountCheckpoints();

        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is { } carA)
            {
                carA.Placement = 0;
            }
        }

        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is not { } carA)
                continue;

            for (var j = i + 1; j < Players.Count; j++)
            {
                if (Players[j].Car is not { } carB)
                    continue;

                // Dead cars rank behind every live one, so their progress cannot push a live car
                // down the order the HUD reads.
                if (carA.Wasted != carB.Wasted)
                {
                    if (carA.Wasted) carA.Placement++;
                    else carB.Placement++;
                    continue;
                }

                if (carA.TotalCheckpoint != carB.TotalCheckpoint)
                {
                    if (carA.TotalCheckpoint < carB.TotalCheckpoint)
                        carA.Placement++;
                    else
                        carB.Placement++;
                    continue;
                }

                if (nsp == 0)
                    continue;

                var next = carA.CurrentCheckpoint + 1;
                if (next >= nsp)
                    next = 0;
                var checkpoint = stage.Checkpoints[next];
                if (DistanceSquared(carA.Position.X, carA.Position.Z, checkpoint.Position.X, checkpoint.Position.Z)
                    > DistanceSquared(carB.Position.X, carB.Position.Z, checkpoint.Position.X, checkpoint.Position.Z))
                    carA.Placement++;
                else
                    carB.Placement++;
            }
        }
    }

    private void ChooseAimLag(BackendCar car)
    {
        var rival = Rank(RivalCar(car));
        var lag = (int)FixedMath.Floor(((fix64)7 - Rank(car)) * rival * ((fix64)6 - (fix64)(AiStage * 2)));
        _driver.AimLag = Math.Max(0, lag);
        _driver.SteerTolerance = (fix64)5;
    }

    private void ChooseHoldBack(BackendCar car)
    {
        var rival = Rank(RivalCar(car));
        var mine = Rank(car);
        var behind = rival - mine;
        var holdBack = (int)FixedMath.Floor(behind * behind * behind * HoldBackScale());
        _driver.HoldBack = Math.Min(80, holdBack);
        if (AiStage == 1 && _driver.HoldBack < 20)
            _driver.HoldBack = 20;
    }

    private static fix64 HoldBackScale() => AiStage switch
    {
        1 => (fix64)2,
        2 => (fix64)1.5f,
        3 => (fix64)0.5f,
        4 => (fix64)0.5f,
        _ => fix64.Zero
    };

    private void ChooseCornerCutting(BackendCar car)
    {
        var maxCutting = AiStage switch
        {
            1 or 2 => (fix64)1,
            3 or 4 => (fix64)0.5f,
            _ => (fix64)0.2f
        };

        if (car.TotalCheckpoint >= RivalCar(car).TotalCheckpoint - 1)
        {
            _driver.CornerCutting -= (fix64)0.1f;
            if (_driver.CornerCutting < fix64.Zero)
                _driver.CornerCutting = fix64.Zero;
        }
        else
        {
            _driver.CornerCutting += (fix64)0.2f;
            if (_driver.CornerCutting > maxCutting)
                _driver.CornerCutting = maxCutting;
        }
    }

    private void ChooseRamps(BackendCar car)
    {
        var mad = car.CarPhysics;
        _driver.RampChoice = (int)FixedMath.Floor((Sim() * (fix64)4) - (fix64)2);

        if (mad.Power == (fix64)98)
            _driver.RampChoice = Ramps.Avoid;
        if (mad.Power < (fix64)75 && _driver.RampChoice == Ramps.Avoid)
            _driver.RampChoice = Ramps.Either;
        if (mad.Power < (fix64)60)
            _driver.RampChoice = Ramps.Take;
    }

    private void ChooseTurnStyle()
    {
        if (_driver.TurnStyleTicks != 0)
        {
            _driver.TurnStyleTicks--;
            return;
        }

        _driver.GasInTurns = false;
        _driver.SharpTurn = (int)FixedMath.Floor(Sim() * (fix64)4);
        _driver.TurnStyleTicks = 5;

        if (_driver.AttackTicks != 0)
            _driver.SharpTurn = SharpTurn.Handbrake;
    }

    private void ChooseLandingCare(BackendCar car)
    {
        var power = car.CarPhysics.Power;
        var margin = (int)FixedMath.Floor(((fix64)98 - power) * (fix64)0.5f * ((Sim() * (fix64)0.5f) + (fix64)0.5f));
        _driver.LandingMargin = Math.Min(20, margin);

        var sloppiness = AiStage switch
        {
            1 => (fix64)0.9f,
            2 => (fix64)0.7f,
            3 => (fix64)0.4f,
            _ => fix64.Zero
        };

        _driver.LandingSloppiness = sloppiness + ((Sim() / (fix64)2) - (fix64)0.25f);

        if (power > (fix64)50)
        {
            if (car.TotalCheckpoint < RivalCar(car).TotalCheckpoint)
                _driver.LandingMargin *= AiStage switch { 1 => (fix64)5, 2 => (fix64)2, 3 => (fix64)1.5f, _ => fix64.One };
            else
                _driver.LandingSloppiness = fix64.Zero;
        }
        else
        {
            _driver.LandingSloppiness -= (fix64)0.5f;
        }
    }

    private void ChooseStuntCaution(BackendCar car)
    {
        var caution = (car.CarPhysics.Power - (fix64)38) / (fix64)50 - (Sim() * (fix64)0.5f);
        _driver.StuntCaution = car.CarPhysics.Power < (fix64)60 ? (fix64)(-1) : caution;
    }

    private void ChooseLandingHabits(BackendCar car)
    {
        var mad = car.CarPhysics;
        _driver.GroundByWheels = Sim() > mad.Power / (fix64)100;
        _driver.LandsPerfectly = !(Sim() > (fix64)mad.DamagePoints / (fix64)mad.Stat.Maxmag);
        if ((fix64)100 * mad.DamagePoints / (fix64)mad.Stat.Maxmag > (fix64)60)
            _driver.LandsPerfectly = true;
    }

    /// <summary>Decides whether to detour to a fix hoop, by how damaged the car is.</summary>
    private void ChooseWhetherToFix(BackendCar car)
    {
        var mad = car.CarPhysics;
        if (_driver.Fixing == Fix.Close)
        {
            _driver.HoldBack = 0;
            _driver.AimLag = 0;
            _driver.CornerCutting = (fix64)1;
            _driver.SteerTolerance = (fix64)2;
            return;
        }

        _driver.Fixing = Fix.None;
        var damage = (fix64)100 * mad.DamagePoints / (fix64)mad.Stat.Maxmag;
        if (damage > (fix64)50)
            _driver.Fixing = Fix.OnTheWay;
        if (damage > (fix64)80)
            _driver.Fixing = Fix.Nearest;
    }

    // ---- attacking -------------------------------------------------------------------

    private void ChooseWhomToAttack(BackendCar car)
    {
        if (_driver.AttackTicks != 0)
            return;

        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is not { } other || ReferenceEquals(other, car))
                continue;
            if (other.Wasted)
                continue;

            ConsiderAttacking(car, other);
        }
    }

    private void ConsiderAttacking(BackendCar car, BackendCar other)
    {
        // Only the player is fair game: the original zeroes the chance for any other car,
        // and outright for anyone who is not the player, so bots never turn on each other.
        if (!ReferenceEquals(other, RivalCar(car)))
            return;

        var heading = Heading(car);
        var headingToOther = Normalize((fix64)HeadingTo(car, other.Position.X, other.Position.Z));
        var angleOff = FixedMath.Abs(Normalize(heading - headingToOther));

        var attackRange = (fix64)2000 * (FixedMath.Abs(other.TotalCheckpoint - car.TotalCheckpoint) + 1);
        var sight = (fix64)85 + ((fix64)15 * (FixedMath.Abs(other.TotalCheckpoint - car.TotalCheckpoint) + 1));

        if (angleOff >= sight
            || DistanceSquared10(car.Position.X, car.Position.Z, other.Position.X, other.Position.Z) >= attackRange
            || !_driver.Afta
            || car.CarPhysics.Power <= PowerNeededToAttack)
            return;

        var closeness = (fix64)35 - (FixedMath.Abs(other.TotalCheckpoint - car.TotalCheckpoint) * (fix64)10);
        if (closeness < fix64.One)
            closeness = fix64.One;

        var chance = ((Rank(car) + 1) * ((fix64)7 - Rank(other))) / closeness;

        // A bot only bothers while it is behind: ahead of the player it just races.
        if (Rank(RivalCar(car)) < Rank(car))
            chance = fix64.Zero;
        if (chance > (fix64)0.7f)
            chance = (fix64)0.7f;

        if (Sim() < chance)
            StartAttack(car, other);
    }

    // The original raises the bar on some stages and for some car types; without either,
    // the baseline is the whole rule.
    private static readonly fix64 PowerNeededToAttack = (fix64)60;

    private void StartAttack(BackendCar car, BackendCar other)
    {
        _driver.AttackTicks = 40 * (Math.Abs(other.TotalCheckpoint - car.TotalCheckpoint) + 1);
        if (_driver.AttackTicks > 500)
            _driver.AttackTicks = 500;

        _driver.LeadAhead = Sim();
        _driver.AttackedCar = other.CarPhysics.Im;
        _driver.SharpTurn = (int)FixedMath.Floor(fix64.One + (Sim() * (fix64)2));
        if (_driver.SharpTurn == SharpTurn.LiftGas)
            _driver.SharpTurn = SharpTurn.Handbrake;
    }
}
