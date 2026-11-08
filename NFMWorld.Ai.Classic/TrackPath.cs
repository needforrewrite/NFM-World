using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Ai.Classic;

/// <summary>
/// The stage's AI path as the classic driver sees it: a flat list of points with a type
/// per point, indexed by position along the lap.
/// </summary>
/// <remarks>
/// <see cref="BackendStage.Nodes"/> also contains the fix hoops, which are pieces you drive
/// through rather than points you steer at, so construction filters them out.
/// </remarks>
internal sealed class TrackPath
{
    /// <summary>Point types, matching the original's <c>track.typ</c>.</summary>
    public const int Road = 0;
    public const int Checkpoint = 1;
    public const int Turn = -1;
    public const int Ramp = -2;
    public const int FixRoadStart = -3;
    public const int Halfpipe = -4;
    public const int FixRoadEnd = -5;

    private readonly fix64[] _x;
    private readonly fix64[] _y;
    private readonly fix64[] _z;
    private readonly int[] _typ;

    /// <summary>
    /// For points inside a closed `)po` fix road, the index of the closing marker; -1 for
    /// normal lap points. Normal driving skips such a block, repairing follows it.
    /// </summary>
    private readonly int[] _fixBlockEnd;

    /// <summary>Path index of each checkpoint, in the order the race crosses them.</summary>
    private readonly int[] _checkpointPath;

    /// <summary>Path point a few back from each fix hoop, where drivers head to repair.</summary>
    public int[] FixApproach { get; }

    /// <summary>Second entry (the closing `)po` / lane end), driven in reverse, or -1.</summary>
    public int[] FixApproachEnd { get; }

    /// <summary>Whether the hoop is above/below the path enough to need a launch.</summary>
    public bool[] FixAerial { get; }

    /// <summary>Whether the approach point is a ramp on the way to the hoop.</summary>
    public bool[] FixRamp { get; }

    /// <summary>Whether the hoop has a strict `)po` fix-road start as its approach.</summary>
    public bool[] FixRoad { get; }

    public bool[] FixIsSpecial { get; }

    public int Count => _typ.Length;

    public int CheckpointCount => _checkpointPath.Length;

    public fix64 X(int index) => _x[index];

    public fix64 Y(int index) => _y[index];

    public fix64 Z(int index) => _z[index];

    public int Typ(int index) => _typ[index];

    /// <summary>True when a path point belongs to a closed `)po` fix road.</summary>
    public bool InFixBlock(int index) => index >= 0 && index < _fixBlockEnd.Length && _fixBlockEnd[index] >= 0;

    /// <summary>
    /// Steps along the racing lap, jumping over closed fix-road blocks. Repair routing uses
    /// <see cref="Advance"/> instead so it can follow the block.
    /// </summary>
    public int AdvanceNormal(int index, int steps)
    {
        if (Count == 0)
            return 0;

        var next = index;
        for (var i = 0; i < steps; i++)
        {
            next = Advance(next, 1);
            var end = _fixBlockEnd[next];
            if (end >= 0)
                next = Advance(end, 1);
        }

        return next;
    }

    public TrackPath(BackendStage stage)
    {
        var nodes = stage.Nodes;

        var points = new List<int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind != AiNodeKind.FixHoop)
                points.Add(i);
        }

        _x = new fix64[points.Count];
        _y = new fix64[points.Count];
        _z = new fix64[points.Count];
        _typ = new int[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var node = nodes[points[i]];
            _x[i] = node.Position.X;
            _y[i] = node.Position.Y;
            _z[i] = node.Position.Z;
            _typ[i] = TypeOf(node.Kind);
        }

        // A `)po` block is a fix road, not part of the lap. It may close with another `)po`
        // or `FixRoadEnd`, or (older tracks) simply run to the end of the node list when it
        // starts after the final checkpoint. Mark its points so normal driving can skip it
        // while a repair can follow it; unmatched inline markers are left alone.
        _fixBlockEnd = new int[points.Count];
        for (var i = 0; i < _fixBlockEnd.Length; i++)
            _fixBlockEnd[i] = -1;

        var lastCheckpoint = -1;
        for (var i = 0; i < _typ.Length; i++)
        {
            if (_typ[i] > 0)
                lastCheckpoint = i;
        }

        for (var i = 0; i < _typ.Length; i++)
        {
            if (_typ[i] != FixRoadStart)
                continue;

            var end = -1;
            for (var j = i + 1; j < _typ.Length; j++)
            {
                if (_typ[j] is FixRoadStart or FixRoadEnd)
                {
                    end = j;
                    break;
                }
            }

            if (end < 0)
            {
                if (i <= lastCheckpoint)
                    continue;

                end = _typ.Length - 1;
            }

            for (var k = i; k <= end; k++)
                _fixBlockEnd[k] = end;

            // Skip the closing marker so it is not opened as another block.
            i = end;
        }

        // Some stages append detached lanes (practice areas) with plain `)p` tags after the
        // lap. Only points after the final gate can belong to one: trimming by distance from
        // the first point alone would also cut a point-to-point stage like LongRun, whose
        // only gate sits at the far end. A closed `)po` fix block is kept: normal driving
        // skips it via AdvanceNormal, but a repair needs it.
        var mainCount = points.Count;
        while (mainCount > 1
               && mainCount - 1 > lastCheckpoint
               && _fixBlockEnd[mainCount - 1] < 0
               && DistanceSquared10(_x[mainCount - 1], _z[mainCount - 1], _x[0], _z[0]) > (fix64)640_000)
            mainCount--;

        if (mainCount < _x.Length)
        {
            // A manual copy: array ranges compile to RuntimeHelpers.GetSubArray, which the
            // plugin sandbox refuses.
            var x = new fix64[mainCount];
            var y = new fix64[mainCount];
            var z = new fix64[mainCount];
            var typ = new int[mainCount];
            var fixEnd = new int[mainCount];
            for (var i = 0; i < mainCount; i++)
            {
                x[i] = _x[i];
                y[i] = _y[i];
                z[i] = _z[i];
                typ[i] = _typ[i];
                fixEnd[i] = _fixBlockEnd[i];
            }

            _x = x;
            _y = y;
            _z = z;
            _typ = typ;
            _fixBlockEnd = fixEnd;
        }

        var checkpoints = new List<int>();
        for (var i = 0; i < _typ.Length; i++)
        {
            if (_typ[i] > 0)
                checkpoints.Add(i);
        }

        _checkpointPath = checkpoints.ToArray();

        var hoops = stage.FixHoops;
        FixApproach = new int[hoops.Count];
        FixApproachEnd = new int[hoops.Count];
        FixAerial = new bool[hoops.Count];
        FixRamp = new bool[hoops.Count];
        FixRoad = new bool[hoops.Count];
        FixIsSpecial = new bool[hoops.Count];
        for (var h = 0; h < hoops.Count; h++)
        {
            var hoop = hoops[h];
            FixIsSpecial[h] = hoop.IsSpecial;
            FixApproachEnd[h] = -1;

            var nearest = NearestPoint(hoop.Position.X, hoop.Position.Z);

            // A hoop far from the tracked path belongs to a fix lane the trim removed (or a
            // practice area); do not pair it with an unrelated ramp on the main line.
            if (DistanceSquared10(hoop.Position.X, hoop.Position.Z, _x[nearest], _z[nearest]) > (fix64)640_000)
            {
                FixApproach[h] = nearest;
                FixAerial[h] = true;
                FixRamp[h] = false;
                FixRoad[h] = false;
                continue;
            }

            FixAerial[h] = FixedMath.Abs(hoop.Position.Y - _y[nearest]) > (fix64)1500;

            // `)po` marks the start of a strict fix road: aim for the start, not a point
            // under the hoop, then follow the branch. Keep the furthest-back marker within
            // the window so a closing `)po` after the hoop is not mistaken for the start.
            var road = -1;
            var ramp = -1;
            for (var back = 0; back <= 12; back++)
            {
                var index = Advance(nearest, -back);
                if (_typ[index] == FixRoadStart)
                    road = index;
                if (ramp < 0 && _typ[index] == Ramp)
                    ramp = index;
            }

            if (road >= 0)
            {
                // Prefer a ramp on the fix road itself over one on the main line.
                var routeRamp = -1;
                var length = ForwardDistance(road, nearest);
                for (var step = 0; step <= length; step++)
                {
                    var index = Advance(road, step);
                    if (_typ[index] == Ramp)
                    {
                        routeRamp = index;
                        break;
                    }
                }

                FixRamp[h] = routeRamp >= 0;
                FixApproach[h] = road;

                // Entering from the closing marker drives the lane backwards through the
                // hoop; either side is valid.
                FixApproachEnd[h] = _fixBlockEnd[road] >= 0 ? _fixBlockEnd[road] : -1;
            }
            else
            {
                // No `)po` road and no ramp: aim at the path point nearest the hoop. A
                // ground hoop sitting on the line is repaired by driving through it; an
                // aerial one with no launch is skipped by HeadForFix.
                FixRamp[h] = ramp >= 0;
                FixApproach[h] = ramp >= 0 ? ramp : nearest;
            }

            FixRoad[h] = road >= 0;
        }
    }

    private static int TypeOf(AiNodeKind kind) => kind switch
    {
        AiNodeKind.CheckPoint => Checkpoint,
        AiNodeKind.Turn => Turn,
        AiNodeKind.Ramp => Ramp,
        AiNodeKind.FixRamp => Ramp,
        AiNodeKind.FixRoadStart => FixRoadStart,
        AiNodeKind.FixRoadEnd => FixRoadEnd,
        AiNodeKind.Halfpipe => Halfpipe,
        _ => Road
    };

    /// <summary>The path point of a checkpoint, by its order in the lap.</summary>
    public int CheckpointPath(int ordinal)
    {
        if (_checkpointPath.Length == 0)
            return 0;
        if (ordinal < 0 || ordinal >= _checkpointPath.Length)
            ordinal %= _checkpointPath.Length;
        if (ordinal < 0)
            ordinal += _checkpointPath.Length;
        return _checkpointPath[ordinal];
    }

    /// <summary>One step along a fix lane in the chosen direction.</summary>
    public int Step(int index, int direction) => Advance(index, direction < 0 ? -1 : 1);

    /// <summary>Steps along the lap, wrapping at either end.</summary>
    public int Advance(int index, int steps)
    {
        if (Count == 0)
            return 0;
        var next = (index + steps) % Count;
        if (next < 0)
            next += Count;
        return next;
    }

    /// <summary>Forward distance along the lap from one point to another.</summary>
    public int ForwardDistance(int from, int to)
    {
        if (Count == 0)
            return 0;
        var distance = (to - from) % Count;
        if (distance < 0)
            distance += Count;
        return distance;
    }

    /// <summary>
    /// The nearest path point at or after the last one, searched only that far ahead.
    /// </summary>
    /// <remarks>
    /// Forward-only on purpose. Track nodes are in driving order, not spatial order: stage 1
    /// puts a gate at z=28000 after the road node at z=29000. Allowing the tracker to step
    /// back picks the road node while the car is beside the gate, then aims at the gate
    /// behind it and the car oscillates. Searching only ahead also cannot jump to another
    /// branch where the path crosses itself.
    /// </remarks>
    public int NearestInWindow(BackendCar car, int around, int window)
    {
        if (Count == 0)
            return 0;
        if (around < 0 || around >= Count)
            return NearestAnywhere(car);

        var best = around;
        var bestDistance = fix64.MaxValue;
        for (var offset = 0; offset <= window; offset++)
        {
            var index = Advance(around, offset);
            var distance = DistanceSquared10(car.Position.X, car.Position.Z, _x[index], _z[index]);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = index;
        }

        return best;
    }

    /// <summary>The nearest point on the whole path, used to re-acquire it after a big knock.</summary>
    public int NearestAnywhere(BackendCar car)
    {
        if (Count == 0)
            return 0;

        var best = 0;
        var bestDistance = fix64.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            var distance = DistanceSquared10(car.Position.X, car.Position.Z, _x[i], _z[i]);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    private static fix64 DistanceSquared10(fix64 x1, fix64 z1, fix64 x2, fix64 z2)
    {
        var dx = (x1 - x2) / (fix64)10;
        var dz = (z1 - z2) / (fix64)10;
        return (dx * dx) + (dz * dz);
    }

    /// <summary>The path point nearest a world position.</summary>
    private int NearestPoint(fix64 x, fix64 z)
    {
        if (Count == 0)
            return 0;

        var nearest = 0;
        var closest = fix64.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            var d = DistanceSquared10(x, z, _x[i], _z[i]);
            if (d >= closest)
                continue;

            closest = d;
            nearest = i;
        }

        return nearest;
    }
}
