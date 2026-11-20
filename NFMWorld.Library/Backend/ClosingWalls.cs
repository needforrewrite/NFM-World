using NFMWorld.PluginSystem;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// phy-addons' converging walls (<c>Walls.java</c>): after a set number of minutes the four stage
/// boundary walls move towards a centre point, and a car left outside the shrinking rectangle takes
/// damage until it is wasted. A race opts in through the PvP <c>walls</c> config option.
/// </summary>
[PluginVisible]
public sealed class ClosingWalls
{
    /// <summary>The shortest side a wall may close to, in world units (phy's <c>MIN_DIST</c>).</summary>
    private const double MinSide = 3600.0;

    private readonly List<StageObject> _right = [];
    private readonly List<StageObject> _left = [];
    private readonly List<StageObject> _top = [];
    private readonly List<StageObject> _bottom = [];

    private readonly StageWall? _rightDef;
    private readonly StageWall? _leftDef;
    private readonly StageWall? _topDef;
    private readonly StageWall? _bottomDef;

    private double _startRight, _startLeft, _startTop, _startBottom;
    private double _rightEdge, _leftEdge, _topEdge, _bottomEdge;
    private double _centreX, _centreZ;
    private double _minutesToGo;

    [PluginVisible] public bool Enabled { get; private set; }

    /// <summary>True once the counting-down phase is over and the walls are moving.</summary>
    [PluginVisible]
    public bool Closing { get; private set; }

    [PluginVisible] public fix64 Right => (fix64)_rightEdge;
    [PluginVisible] public fix64 Left => (fix64)_leftEdge;
    [PluginVisible] public fix64 Top => (fix64)_topEdge;
    [PluginVisible] public fix64 Bottom => (fix64)_bottomEdge;

    internal ClosingWalls()
    {
    }

    internal ClosingWalls(StageLoader loader, IEnumerable<BackendGameObject> pieces)
    {
        foreach (var def in loader.wallDefs)
        {
            switch (def.Direction)
            {
                case WallDirection.Right: _rightDef = def; break;
                case WallDirection.Left: _leftDef = def; break;
                case WallDirection.Top: _topDef = def; break;
                case WallDirection.Bottom: _bottomDef = def; break;
            }
        }

        if (_rightDef is null && loader.maxr != 0)
            _rightDef = new StageWall(WallDirection.Right, 0, loader.maxr, 0);

        foreach (var piece in pieces)
        {
            if (piece is not StageObject { OriginalPlacement.IsWall: true } wall) continue;
            switch (wall.OriginalPlacement.WallSide)
            {
                case WallDirection.Right: _right.Add(wall); break;
                case WallDirection.Left: _left.Add(wall); break;
                case WallDirection.Top: _top.Add(wall); break;
                case WallDirection.Bottom: _bottom.Add(wall); break;
            }
        }

        if (_rightDef is null || _leftDef is null || _topDef is null || _bottomDef is null)
            return;

        _startRight = _rightEdge = _rightDef.Position;
        _startLeft = _leftEdge = _leftDef.Position;
        _startTop = _topEdge = _topDef.Position;
        _startBottom = _bottomEdge = _bottomDef.Position;
        Enabled = true;
    }

    /// <summary>
    /// Starts the countdown for this race. The centre is one of the stage's wall-option pieces when
    /// it has any; phy picks one at random, and a stable hash of the stage name gives every peer the
    /// same pick without carrying it on the wire.
    /// </summary>
    [PluginVisible]
    public void Begin(double minutesToGo, StageLoader loader)
    {
        if (!Enabled) return;

        _minutesToGo = Math.Max(0, minutesToGo);
        Closing = false;

        if (loader.WallCentres.Count > 0)
        {
            var target = TargetFor(loader.Name);
            var index = Math.Clamp((int)(target * loader.WallCentres.Count), 0, loader.WallCentres.Count - 1);
            _centreX = (double)loader.WallCentres[index].X;
            _centreZ = (double)loader.WallCentres[index].Z;
        }
        else
        {
            _centreX = (_startRight + _startLeft) / 2;
            _centreZ = (_startTop + _startBottom) / 2;
        }

        Apply(1.0, closing: false);
    }

    /// <summary>Advances the countdown/movement to a race elapsed time, in minutes.</summary>
    [PluginVisible]
    public void Update(double elapsedMinutes)
    {
        if (!Enabled) return;

        var closing = elapsedMinutes >= _minutesToGo;
        Closing = closing;

        // phy's areaMul: closed walls keep shrinking, halving the area every five minutes.
        var area = Math.Pow(0.5, Math.Max(((elapsedMinutes + 5.0 - _minutesToGo) / 5.0 - 1.0) * 3.0, 0.0));
        var mul = closing ? Math.Sqrt(area) : 1.0;
        Apply(mul, closing);
    }

    /// <summary>Minutes until the walls start moving; zero once they are.</summary>
    [PluginVisible]
    public double MinutesUntilClosing(double elapsedMinutes) => Math.Max(0, _minutesToGo - elapsedMinutes);

    [PluginVisible]
    public bool IsInBounds(fix64 x, fix64 z)
    {
        if (!Enabled || !Closing) return true;
        return x >= Left && x < Right && z >= Bottom && z < Top;
    }

    private void Apply(double mul, bool closing)
    {
        // Always from the stage's original rectangle: applying it to the previous frame's result
        // would compound the shrink and slam the walls in after a couple of ticks.
        var dr = _startRight - _centreX;
        var dl = _startLeft - _centreX;
        var dt = _startTop - _centreZ;
        var db = _startBottom - _centreZ;

        var right = _centreX + Math.Min(Math.Max(Math.Abs(mul * dr), MinSide) * Math.Sign(dr), dr);
        var left = _centreX + Math.Max(Math.Max(Math.Abs(mul * dl), MinSide) * Math.Sign(dl), dl);
        var top = _centreZ + Math.Min(Math.Max(Math.Abs(mul * dt), MinSide) * Math.Sign(dt), dt);
        var bottom = _centreZ + Math.Max(Math.Max(Math.Abs(mul * db), MinSide) * Math.Sign(db), db);

        var rightChanged = right != (double)Right;
        var leftChanged = left != (double)Left;
        var topChanged = top != (double)Top;
        var bottomChanged = bottom != (double)Bottom;
        if (!closing && !rightChanged && !leftChanged && !topChanged && !bottomChanged) return;

        // Phy moves the wall pieces themselves; the renderer re-reads StageObject positions each tick.
        if (rightChanged)
            foreach (var piece in _right) piece.Position = new f64Vector3((fix64)right, piece.Position.Y, piece.Position.Z);
        if (leftChanged)
            foreach (var piece in _left) piece.Position = new f64Vector3((fix64)left, piece.Position.Y, piece.Position.Z);
        if (topChanged)
            foreach (var piece in _top) piece.Position = new f64Vector3(piece.Position.X, piece.Position.Y, (fix64)top);
        if (bottomChanged)
            foreach (var piece in _bottom)
                piece.Position = new f64Vector3(piece.Position.X, piece.Position.Y, (fix64)bottom);

        _rightEdge = right;
        _leftEdge = left;
        _topEdge = top;
        _bottomEdge = bottom;
    }

    private static double TargetFor(string name)
    {
        // Stable across processes (string.GetHashCode is randomised per process), so a peer and the
        // game master agree on the centre while every stage still gets its own.
        var hash = 2166136261u;
        foreach (var c in name)
            hash = (hash ^ c) * 16777619u;
        return hash / (double)uint.MaxValue;
    }

    /// <summary>
    /// The four boundary boxes at their current positions, appended to a collision query the way
    /// the static wall boxes are. The quadtree is built once, so moving walls cannot live in it.
    /// </summary>
    internal void AppendCollidables(List<CollisionShapeRef> target, int indexBase)
    {
        if (!Enabled || !Closing) return;

        Append(_rightDef, indexBase, target);
        Append(_leftDef, indexBase + 1, target);
        Append(_topDef, indexBase + 2, target);
        Append(_bottomDef, indexBase + 3, target);
    }

    private void Append(StageWall? def, int index, List<CollisionShapeRef> target)
    {
        if (def is null) return;

        var length = def.Count * 2400.0;
        Rad3dBoxDef box;

        switch (def.Direction)
        {
            case WallDirection.Right:
                box = new Rad3dBoxDef(
                    Xy: 90,
                    Zy: 0,
                    Radius: new f64Vector3(600, 7100, length),
                    Translation: new f64Vector3((double)Right + 500, -5000, length + def.Offset - 2400),
                    SurfaceType: SurfaceType.Road,
                    Damage: 1,
                    NotWall: false,
                    Color: new Color3()
                );
                break;
            case WallDirection.Left:
                box = new Rad3dBoxDef(
                    Xy: -90,
                    Zy: 0,
                    Radius: new f64Vector3(600, 7100, length),
                    Translation: new f64Vector3((double)Left - 500, -5000, length + def.Offset - 2400),
                    SurfaceType: SurfaceType.Road,
                    Damage: 1,
                    NotWall: false,
                    Color: new Color3()
                );
                break;
            case WallDirection.Top:
                box = new Rad3dBoxDef(
                    Xy: 0,
                    Zy: 90,
                    Radius: new f64Vector3(length, 7100, 600),
                    Translation: new f64Vector3(length + def.Offset - 2400, -5000, (double)Top + 500),
                    SurfaceType: SurfaceType.Road,
                    Damage: 1,
                    NotWall: false,
                    Color: new Color3()
                );
                break;
            default:
                box = new Rad3dBoxDef(
                    Xy: 180,
                    Zy: -90,
                    Radius: new f64Vector3(length, 7100, 600),
                    Translation: new f64Vector3(length + def.Offset - 2400, -5000, (double)Bottom - 500),
                    SurfaceType: SurfaceType.Road,
                    Damage: 1,
                    NotWall: false,
                    Color: new Color3()
                );
                break;
        }

        // A high index keeps the moving walls after the static geometry in the sorted query.
        target.Add(new CollisionShapeRef(0, 0, 0, 0, box, 0, int.MaxValue - 4 + index));
    }
}
