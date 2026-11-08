using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Ai.Classic;

public sealed partial class ClassicAi
{
    /// <summary>The original's 2000 in 1/100-unit squared distance, in <see cref="DistanceSquared10"/> units.</summary>
    private const int FixCloseSquared = 200_000;

    /// <summary>Distance a shortcut must save, in <see cref="Distance10"/> units (500 world units).</summary>
    private const int ShortcutMinSave = 50;

    /// <summary>How far (in 1000-unit steps) the skipped nodes may sit off the shortcut line.</summary>
    private static readonly fix64 ShortcutCorridor = (fix64)1.2f;

    /// <summary>Re-rolls how this driver likes to drive, every so often while grounded.</summary>
    private void Rethink(BackendCar car)
    {
        ChooseRamps(car);
        ChooseTurnStyle();
        ChooseLandingCare(car);
        ChooseStuntCaution(car);
        ChooseLandingHabits(car);
        ChooseWhomToAttack(car);
        ChooseWhetherToFix(car);

        _driver.SteerTolerance = (fix64)4;
        _driver.TicksSinceRethink = 0;
        _driver.RethinkAfter = (int)FixedMath.Floor((fix64)20 * Sim());
    }

    /// <summary>The point to drive at, after ramps and the next-gate clamp.</summary>
    private int ChoosePathPoint(BackendCar car, int point)
    {
        var path = Path;

        // On a fix lane, follow its nodes and take its ramps; skipping the entry/apex is
        // how the car ends up circling the `)po`.
        if (_driver.Fixing != Fix.None && path.InFixBlock(_driver.PathIndex))
        {
            _driver.TargetsFix = true;
            _driver.RampChoice = Ramps.Take;

            // While the entry node itself is not reached, aim at it. Aiming at the next
            // node makes the car orbit the next node and never get within capture of the
            // entry.
            var currentDistance = DistanceSquared10(
                car.Position.X,
                car.Position.Z,
                path.X(_driver.PathIndex),
                path.Z(_driver.PathIndex)
            );

            return currentDistance > (fix64)FixLaneCaptureSquared
                ? _driver.PathIndex
                : path.Step(_driver.PathIndex, _driver.FixDirection);
        }

        if (_driver.Missed)
        {
            return path.CheckpointCount > 0
                ? path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount)
                : point;
        }

        point = TakeOrAvoidRamps(car, point);
        point = ClampToCheckpoint(car, point);
        return TryShortcut(car, point);
    }

    /// <summary>
    /// Slightly straightens a gentle bend by aiming a node or two further along the path.
    /// </summary>
    /// <remarks>
    /// Conservative on purpose: the skipped nodes must lie within about a road-width of the
    /// new line, no gate may be skipped, the line must save real distance, and a damaged car
    /// keeps to the exact path. That skips redundant pieces on a sweep without cutting
    /// across the infield into a wall.
    /// </remarks>
    private int TryShortcut(BackendCar car, int point)
    {
        var path = Path;
        if (_driver.Missed || _driver.Fixing != Fix.None || path.Typ(point) > 0)
            return point;

        // Damage is the dynamic risk gate: a healthy car takes the cleaner line.
        if (car.CarPhysics.DamagePoints * 2 > car.CarPhysics.Stat.Maxmag)
            return point;

        var nearest = _driver.PathIndex;
        var gateLimit = path.CheckpointCount > 0
            ? path.ForwardDistance(nearest, path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount))
            : int.MaxValue;

        for (var skip = 3; skip >= 2; skip--)
        {
            // Never jump the checkpoint that still has to be cleared.
            if (skip >= gateLimit)
                continue;

            var candidate = path.AdvanceNormal(nearest, skip);
            if (path.Typ(candidate) > 0
                || path.Typ(candidate) == TrackPath.FixRoadStart
                || path.InFixBlock(candidate))
                continue;

            if (ShortcutClear(car, path, nearest, candidate, skip))
                return candidate;
        }

        return point;
    }

    private static bool ShortcutClear(BackendCar car, TrackPath path, int nearest, int candidate, int skip)
    {
        var headingOff = Normalize(Heading(car) - (fix64)HeadingTo(car, path.X(candidate), path.Z(candidate)));
        if (FixedMath.Abs(headingOff) > (fix64)90)
            return false;

        var first = path.AdvanceNormal(nearest, 1);
        var direct = Distance10(car.Position.X, car.Position.Z, path.X(candidate), path.Z(candidate));
        var via = Distance10(car.Position.X, car.Position.Z, path.X(first), path.Z(first));
        for (var i = 2; i <= skip; i++)
        {
            var a = path.AdvanceNormal(nearest, i - 1);
            var b = path.AdvanceNormal(nearest, i);
            via += Distance10(path.X(a), path.Z(a), path.X(b), path.Z(b));
        }

        if (via - direct < (fix64)ShortcutMinSave)
            return false;

        // Perpendicular distance of each skipped node from the new line. Scaled by 1000 so
        // the squares stay well inside fix64 on the widest stages.
        var ax = (path.X(candidate) - car.Position.X) / (fix64)1000;
        var az = (path.Z(candidate) - car.Position.Z) / (fix64)1000;
        var lengthSquared = (ax * ax) + (az * az);
        if (lengthSquared == fix64.Zero)
            return false;

        for (var i = 1; i < skip; i++)
        {
            var skipped = path.AdvanceNormal(nearest, i);
            if (path.Typ(skipped) > 0 || path.Typ(skipped) == TrackPath.FixRoadStart || path.InFixBlock(skipped))
                return false;

            var px = (path.X(skipped) - car.Position.X) / (fix64)1000;
            var pz = (path.Z(skipped) - car.Position.Z) / (fix64)1000;
            var t = ((px * ax) + (pz * az)) / lengthSquared;
            if (t < fix64.Zero) t = fix64.Zero;
            if (t > fix64.One) t = fix64.One;

            var dx = (ax * t) - px;
            var dz = (az * t) - pz;
            if ((dx * dx) + (dz * dz) > ShortcutCorridor * ShortcutCorridor)
                return false;
        }

        return true;
    }

    private int TakeOrAvoidRamps(BackendCar car, int point)
    {
        var path = Path;
        var normal = _driver.Fixing == Fix.None;
        if (_driver.RampChoice == Ramps.Take && path.Typ(point) <= 0)
        {
            var next = normal ? path.AdvanceNormal(point, 1) : path.Advance(point, 1);
            // A ramp right before a gate launches the car over the checkpoint box; the
            // driver then has to turn back and can orbit the gate. Stay on the ground.
            var skipsGate = path.CheckpointCount > 0
                            && path.ForwardDistance(next, path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount)) <= 2;

            if (path.Typ(next) == TrackPath.Ramp && !skipsGate)
                point = next;
        }

        if (_driver.RampChoice == Ramps.Avoid && path.Typ(point) == TrackPath.Ramp)
            point = normal ? path.AdvanceNormal(point, 1) : path.Advance(point, 1);

        return point;
    }

    /// <summary>Detours to a fix hoop when the car is damaged, and pulls in when close.</summary>
    private int HeadForFix(BackendCar car, int point)
    {
        if (_driver.Fixing == Fix.None)
            return point;

        var path = Path;

        // Already on the fix lane: let the normal path advance along it. Re-targeting the
        // `)po` entry every tick makes the car orbit the entry node forever. Only a lane
        // the driver actually chose counts; the initial nearest-point search can land on a
        // lane node from the main line.
        if (_driver.FixLaneActive && path.InFixBlock(_driver.PathIndex))
            return point;

        // A gate comes first. Detouring to a hoop on the approach to a checkpoint is how a
        // damaged driver loses the checkpoint it was about to clear.
        if (_driver.Missed && !_driver.IgnoreMissed && _driver.Fixing != Fix.Jumping)
            return point;
        if (NearNextCheckpoint(car) && _driver.Fixing != Fix.Jumping)
            return point;

        if (path.FixApproach.Length == 0)
            return point;

        if (_driver.Fixing == Fix.Nearest)
        {
            // A hoop is reachable from its ordinary approach and, on a `)po` lane, from the
            // closing marker driven backwards. Take whichever entry is closest.
            var nearest = fix64.Zero;
            var nearestFix = -1;
            var nearestApproach = -1;
            var nearestDirection = 1;
            var nearestRoad = false;
            var nearestRamp = false;

            void Consider(int index, int approach, int direction, bool road, bool ramp)
            {
                if (approach < 0)
                    return;

                // A hoop in the air with no ramp or explicit fix road cannot be reached.
                if (path.FixAerial[index] && !ramp && !road)
                    return;

                // A detached `)po` block can sit more than half the node list away while
                // still being the route to take. Only ordinary approaches need the
                // behind-the-car guard; explicit roads are protected by the fix cooldown
                // once the car has left them.
                if (!road && path.ForwardDistance(_driver.PathIndex, approach) > path.Count / 2)
                    return;

                // Critically damaged, an explicit `)po` route is worth going to even if the
                // array order puts it far ahead; otherwise keep the U-turn guard.
                if (!FixAhead(path, approach, road))
                    return;

                var d = DistanceSquared10(
                    car.Position.X,
                    car.Position.Z,
                    path.X(approach),
                    path.Z(approach)
                );

                if (nearestFix >= 0 && d >= nearest)
                    return;

                nearest = d;
                nearestFix = index;
                nearestApproach = approach;
                nearestDirection = direction;
                nearestRoad = road;
                nearestRamp = ramp;
            }

            for (var i = 0; i < path.FixApproach.Length; i++)
            {
                Consider(i, path.FixApproach[i], 1, path.FixRoad[i], path.FixRamp[i]);
                Consider(i, path.FixApproachEnd[i], -1, true, path.FixRamp[i]);
            }

            if (nearestFix >= 0)
            {
                point = nearestApproach;
                _driver.IgnoreMissed = path.FixIsSpecial[nearestFix];
                _driver.TargetsFix = true;
                _driver.FixDirection = nearestDirection;
                if (nearestRamp)
                    _driver.RampChoice = Ramps.Take;

                // Pin the tracker to an explicit `)po` entry. Otherwise a main-path node
                // that happens to be closer can win the nearest search and the car orbits
                // the entry without ever entering the fix block.
                if (nearestRoad)
                {
                    _driver.PathIndex = point;
                    _driver.FixLaneActive = true;
                }
            }
            else
            {
                // Nothing useful ahead: keep racing and repair only if the path happens to
                // take the car through a hoop.
                _driver.Fixing = Fix.OnTheWay;
            }
        }

        // A `)po` fix road is a branch: turn onto it while it is ahead, before the next
        // gate, so "repair on the way" actually enters the repair route. A `)pr` ramp is
        // on the line; take it while it is still ahead.
        if (_driver.Fixing != Fix.Nearest)
        {
            for (var i = 0; i < path.FixApproach.Length; i++)
            {
                if (!path.FixRoad[i] && !path.FixRamp[i])
                    continue;

                var approach = path.FixApproach[i];
                if (!FixAhead(path, approach, false))
                    continue;

                if (path.CheckpointCount > 0)
                {
                    var checkpoint = path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount);
                    if (path.ForwardDistance(_driver.PathIndex, approach)
                        >= path.ForwardDistance(_driver.PathIndex, checkpoint))
                        continue;
                }

                point = approach;
                _driver.TargetsFix = true;
                _driver.FixDirection = 1;
                if (path.FixRoad[i])
                {
                    _driver.PathIndex = approach;
                    _driver.FixLaneActive = true;
                }

                if (path.FixRamp[i])
                    _driver.RampChoice = Ramps.Take;
                break;
            }
        }

        for (var i = 0; i < path.FixApproach.Length; i++)
        {
            if (path.FixAerial[i] && !path.FixRamp[i] && !path.FixRoad[i])
                continue;

            var approach = path.FixApproach[i];
            if (!FixAhead(path, approach, false))
                continue;

            // The original measures this in 1/100-unit squared distance (2000 means ~4470
            // units); DistanceSquared10 is in 1/10 units, so the threshold scales by 100.
            if (DistanceSquared10(car.Position.X, car.Position.Z, path.X(approach), path.Z(approach))
                < (fix64)FixCloseSquared)
            {
                _driver.IgnoreMissed = false;
                _driver.SharpTurn = SharpTurn.Handbrake;
                _driver.RampChoice = path.FixRamp[i] ? Ramps.Take : Ramps.Either;
                _driver.SteerTolerance = (fix64)2;
                _driver.Fixing = Fix.Close;
            }
        }

        return point;
    }

    /// <summary>True when a fix approach is still ahead of the car in this lap.</summary>
    private bool FixAhead(TrackPath path, int approach, bool explicitRoad)
    {
        if (path.Count == 0 || _driver.PathIndex < 0)
            return false;

        // A critically damaged driver will go to a designed `)po` route wherever it sits in
        // the node list; a lap's array position does not describe the detour distance.
        if (explicitRoad && _driver.Fixing == Fix.Nearest)
            return true;

        var ahead = path.ForwardDistance(_driver.PathIndex, approach);

        // A ramp/hoop can legitimately sit a little further ahead than eight points; the
        // U-turn guard is the half-lap test, not a short window.
        return ahead <= Math.Min(16, path.Count / 2);
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
        var nsp = Path.CheckpointCount;

        for (var i = 0; i < Players.Count; i++)
        {
            if (Players[i].Car is { } carA)
                carA.Placement = 0;
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
                if (DistanceSquared10(carA.Position.X, carA.Position.Z, checkpoint.Position.X, checkpoint.Position.Z)
                    > DistanceSquared10(carB.Position.X, carB.Position.Z, checkpoint.Position.X, checkpoint.Position.Z))
                    carA.Placement++;
                else
                    carB.Placement++;
            }
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

        _driver.LandingSloppiness = (Sim() / (fix64)2) - (fix64)0.25f;

        if (power > (fix64)50)
        {
            if (car.TotalCheckpoint < RivalCar(car).TotalCheckpoint)
                _driver.LandingMargin *= 2;
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

        // Always recomputed: holding the "at the hoop" state would keep a repaired driver
        // circling it, because nothing else clears it.
        _driver.Fixing = Fix.None;

        // Do not immediately turn back into the lane the car just left.
        if (_driver.FixCooldown > 0)
            return;

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

        // Chasing a car takes the bot off the path. Never do it on the approach to a gate:
        // that is how a driver loses a checkpoint it was about to clear.
        if (_driver.Missed || NearNextCheckpoint(car))
            return;

        // A damaged car races for the line, it does not pick a fight.
        var mad = car.CarPhysics;
        if ((fix64)100 * mad.DamagePoints / (fix64)mad.Stat.Maxmag > (fix64)50)
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

    private bool NearNextCheckpoint(BackendCar car)
    {
        var path = Path;
        if (path.CheckpointCount == 0 || _driver.PathIndex < 0)
            return false;

        var checkpoint = path.CheckpointPath(car.CurrentCheckpoint % path.CheckpointCount);
        return path.ForwardDistance(_driver.PathIndex, checkpoint) <= 3;
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
