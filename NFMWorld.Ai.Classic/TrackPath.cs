using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Ai.Classic;

/// <summary>
/// The stage's AI path as the classic driver sees it: a flat list of points with a type
/// per point, indexed by position along the lap.
/// </summary>
/// <remarks>
/// <see cref="BackendStage.Nodes"/> is the same list but includes the fix hoops, which the
/// original keeps out of the path array (they are pieces you drive through, not points you
/// steer at). Construction therefore filters them out and remembers the mapping, since
/// <see cref="BackendCar.LastCheckpointNode"/> is an index into <see cref="BackendStage.Nodes"/>.
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

    private readonly fix64[] _x;
    private readonly fix64[] _z;
    private readonly int[] _typ;

    /// <summary>Path index of each node, or -1 for a fix hoop.</summary>
    private readonly int[] _pathOfNode;

    /// <summary>Path point a few back from each fix hoop, where drivers head to repair.</summary>
    public int[] FixApproach { get; }

    public bool[] FixIsSpecial { get; }

    public int Count => _typ.Length;

    public fix64 X(int index) => _x[index];

    public fix64 Z(int index) => _z[index];

    public int Typ(int index) => _typ[index];

    public TrackPath(BackendStage stage)
    {
        var nodes = stage.Nodes;
        _pathOfNode = new int[nodes.Count];

        var points = new List<int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == AiNodeKind.FixHoop)
            {
                _pathOfNode[i] = -1;
                continue;
            }

            _pathOfNode[i] = points.Count;
            points.Add(i);
        }

        _x = new fix64[points.Count];
        _z = new fix64[points.Count];
        _typ = new int[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var node = nodes[points[i]];
            _x[i] = node.Position.X;
            _z[i] = node.Position.Z;
            _typ[i] = TypeOf(node.Kind);
        }

        var hoops = stage.FixHoops;
        FixApproach = new int[hoops.Count];
        FixIsSpecial = new bool[hoops.Count];
        for (var h = 0; h < hoops.Count; h++)
        {
            FixIsSpecial[h] = hoops[h].IsSpecial;
            FixApproach[h] = ApproachPoint(hoops[h].Position.X, hoops[h].Position.Z);
        }
    }

    private static int TypeOf(AiNodeKind kind) => kind switch
    {
        AiNodeKind.CheckPoint => Checkpoint,
        AiNodeKind.Turn => Turn,
        AiNodeKind.Ramp => Ramp,
        AiNodeKind.FixRoadStart => FixRoadStart,
        AiNodeKind.Halfpipe => Halfpipe,
        _ => Road
    };

    /// <summary>Path index of a node, or -1 when it is not a path point.</summary>
    public int PathIndex(int nodeIndex)
        => nodeIndex >= 0 && nodeIndex < _pathOfNode.Length ? _pathOfNode[nodeIndex] : -1;

    /// <summary>The path point four back from the one nearest a hoop, as the original does.</summary>
    private int ApproachPoint(fix64 x, fix64 z)
    {
        if (Count == 0)
            return 0;

        var nearest = 0;
        var closest = fix64.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            var dx = x - _x[i];
            var dz = z - _z[i];
            var d = (dx * dx) + (dz * dz);
            if (d >= closest)
                continue;

            closest = d;
            nearest = i;
        }

        nearest -= 4;
        if (nearest < 0)
            nearest += Count;
        return nearest;
    }

    /// <summary>
    /// The path point nearest the car, weighted toward the stretch at or after the next
    /// checkpoint (the original's <c>passCheckpoints</c>).
    /// </summary>
    public int NearestPoint(BackendCar car, bool nofocus)
    {
        if (Count == 0)
            return 0;

        var clear = car.TotalCheckpoint;
        var nlaps = car.CurrentLap;
        var nsp = CountCheckpoints();

        var weight = nofocus ? (fix64)1 : (fix64)7;
        var pnt = 0;
        var pyclos = fix64.Zero;
        var ic = 0;

        for (var i = 0; i < Count; i++)
        {
            if (_typ[i] > 0)
            {
                ic++;
                if (clear == ic + (nlaps * nsp))
                    weight = (fix64)1;
            }

            var dx = (car.Position.X - _x[i]) / (fix64)10;
            var dz = (car.Position.Z - _z[i]) / (fix64)10;
            var d = ((dx * dx) + (dz * dz)) * weight;
            if (d < pyclos || pyclos == fix64.Zero)
            {
                pnt = i;
                pyclos = d;
            }
        }

        return pnt;
    }

    /// <summary>Number of checkpoint points, i.e. the original's <c>track.nsp</c>.</summary>
    public int CountCheckpoints()
    {
        var count = 0;
        for (var i = 0; i < Count; i++)
        {
            if (_typ[i] > 0)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Advances the followed point from the one nearest the car (the original's
    /// <c>pathPointAhead</c>, bots only: the missed-checkpoint bookkeeping is the player's).
    /// </summary>
    /// <remarks>
    /// <paramref name="cleared"/> is <c>car.LastCheckpointNode</c> mapped through
    /// <see cref="PathIndex"/>, and -1 when the last checkpoint was the lap's final one.
    /// <paramref name="missed"/> mirrors the original's <c>missedCheckpoint</c>: the
    /// original only tracks it for the player, but a bot is "following" whenever it has no
    /// focus, and repair waits on that.
    /// </remarks>
    public int PointAhead(int pnt, int cleared, BackendCar car, bool nofocus, ref int focus, ref bool missed)
    {
        if (Count == 0)
            return 0;

        if (focus != -1)
        {
            pnt = focus;
            missed = true;
            if (nofocus)
                focus = -1;
            return pnt;
        }

        missed = false;
        pnt++;

        var nsp = CountCheckpoints();
        if (!nofocus && nsp > 0)
        {
            var ic = cleared >= 0 ? cleared + 1 : 0;
            if (ic >= Count)
                ic = 0;
            while (_typ[ic] <= 0)
            {
                ic++;
                if (ic >= Count)
                    ic = 0;
            }

            var lapped = car.TotalCheckpoint == car.CurrentLap * nsp;
            if (pnt > ic && (!lapped || pnt < cleared))
            {
                pnt = ic;
                focus = pnt;
            }
        }

        if (pnt >= Count)
            pnt -= Count;
        if (_typ[pnt] == FixRoadStart)
            pnt = 0;

        return pnt;
    }
}
