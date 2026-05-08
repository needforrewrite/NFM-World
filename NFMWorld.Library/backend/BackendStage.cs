using System.Runtime.InteropServices;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

public class BackendStage : IStage
{
    IReadOnlyList<ITransform> IStage.pieces => pieces;
    IReadOnlyList<IAiNode> IStage.nodes => nodes;
    IReadOnlyList<IAiNode> IStage.checkpoints => checkpoints;
    IReadOnlyList<IAiNode> IStage.fixHoops => fixHoops;

    public UnlimitedArray<ITransform> pieces { get; } = [];
    public UnlimitedArray<StageObject> nodes { get; } = [];
    public UnlimitedArray<StageObject> checkpoints { get; } = [];
    public UnlimitedArray<StageObject> fixHoops { get; } = [];
    public ushort nlaps { get; set; }

    public string Name = "hogan rewish";

    public readonly string Path;
    
    // left
    public int Sx;
    // top
    public int Sz;
    // width
    public int Ncx;
    // height
    public int Ncz;

    public int stagePartCount => pieces.Count;

    public readonly StageLoader stageLoader;

    protected BackendStage()
    {
        // Creates an empty stage for inheritance
        Path = "~empty~";
        stageLoader = new StageLoader();
    }

    public BackendStage(string stageName, StageLoader stageLoader)
    {
        Path = stageName;
        try
        {
            this.stageLoader = stageLoader;
            LoadStageInternal(stageLoader);
        }
        catch (StageLoadException exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}");
            Logging.Error($"At line: {exception.Line} (number {exception.LineNumber})");
            Logging.Error(exception.ToString());
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}");
            Logging.Error(exception.ToString());
        }
    }

    public BackendStage(string stageName)
    {
        Path = stageName;
        try
        {
            stageLoader = new StageLoader(stageName);

            LoadStageInternal(stageLoader);
        }
        catch (StageLoadException exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}");
            Logging.Error($"At line: {exception.Line} (number {exception.LineNumber})");
            Logging.Error(exception.ToString());
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}");
            Logging.Error(exception.ToString());
        }
    }

    private void LoadStageInternal(StageLoader stageLoader)
    {
        foreach (var piece in stageLoader.pieces)
        {
            switch (piece.Type)
            {
                case PiecePlacementType.CollisionObject:
                {
                    var obj = new StageObject(
                        piece.Object,
                        piece.Position,
                        piece.Rotation
                    );
                    pieces[stagePartCount] = obj;
                    if (piece.NodeKind is { } nodeKind)
                    {
                        nodes[nodes.Count] = obj;
                        obj.Kind = nodeKind;
                    }

                    break;
                }
                case PiecePlacementType.CheckPoint:
                {
                    var obj = new StageObject(
                        piece.Object,
                        piece.Position,
                        piece.Rotation
                    );
                    obj.Kind = AiNodeKind.CheckPoint;
                    pieces[stagePartCount] = obj;
                    nodes[nodes.Count] = obj;
                    checkpoints[checkpoints.Count] = obj;

                    break;
                }
                case PiecePlacementType.FixHoop:
                {
                    var fix = new StageObject(
                        piece.Object,
                        piece.Position,
                        piece.Rotation
                    );
                    fix.Kind = AiNodeKind.FixHoop;
                    pieces[stagePartCount] = fix;

                    fixHoops[fixHoops.Count] = fix;
                    nodes[nodes.Count] = fix;
                    if (piece.IsSpecial)
                    {
                        fix.IsSpecial = true;
                    }

                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(piece.Type), piece.Type, null);
                }
            }
        }

        nlaps = stageLoader.nlaps;
        Name = stageLoader.Name;
            
        // stage walls
        if (stageLoader.walls.Count > 0)
        {
            pieces[stagePartCount] = new WallCollision([..stageLoader.walls]);
        }

        SetBounds(stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb,
            stageLoader.maxt - stageLoader.maxb);
    }

    private void SetBounds(int sx, int ncx, int sz, int ncz)
    {
        Sx = sx;
        Sz = sz;
        Ncx = ncx;
        if (Ncx <= 0)
        {
            Ncx = 1;
        }
        Ncz = ncz;
        if (Ncz <= 0)
        {
            Ncz = 1;
        }
        
        CollisionQuadTree = new QuadTree<CollisionBoxRef>(sx, sz, ncx, ncz);
        foreach (var piece in pieces)
        {
            if (piece is ICollidable collidable)
            {
                AddToQuadTree(collidable);
            }
        }
        CollisionQuadTree.TrimExcess();
    }

    public ITransform CreateObject(string objectName, int x, int y, int z, int r)
    {
        var part = BackendGameSparker.GetStagePart(objectName);
        if (part.Rad == null)
        {
            Logging.Info($"Object '{objectName}' not found.");
            part = (-1, BackendGameSparker.error_mesh);
        }

        var mesh = new StageObject(
            part.Rad,
            new f64Vector3(x, 250 - y, z), 
            new f64Euler(f64AngleSingle.FromDegrees(r), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)
        );
        pieces[stagePartCount] = mesh;

        Logging.Info($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}");

        AddToQuadTree(mesh);

        return mesh;
    }
    
    private QuadTree<CollisionBoxRef> CollisionQuadTree = new(0,0,0,0);
    private int _quadTreeInsertionIndex = 0;

    private void AddToQuadTree(ICollidable mesh)
    {
        fix64 x = 0;
        fix64 y = 0;
        fix64 z = 0;
        fix64 xz = 0;
        if (mesh is ITransform transform)
        {
            x = transform.Position.X;
            y = transform.Position.Y;
            z = transform.Position.Z;
            xz = transform.Rotation.Xz.Degrees;
        }
        
        foreach (var box in mesh.Boxes)
        {
            var maxR = fix64.Max(
                mesh.MaxRadius,
                fix64.Max(
                    fix64.Max(
                        fix64.Abs(box.Translation.X) + fix64.Abs(box.Radius.X),
                        fix64.Abs(box.Translation.Z) + fix64.Abs(box.Radius.Z)
                    ),
                    fix64.Abs(box.Translation.Y) + fix64.Abs(box.Radius.Y)
                )
            );
            CollisionQuadTree.Insert(new CollisionBoxRef(
                gameObjectX: x,
                gameObjectY: y,
                gameObjectZ: z,
                gameObjectRotXz: xz,
                box: box,
                maxR,
                index: _quadTreeInsertionIndex++
            ));
        }
    }
    
    private List<CollisionBoxRef> _tempTrackers = new();

    public ReadOnlySpan<CollisionBoxRef> RetrievePointCollidables(fix64 x, fix64 z)
    {
        _tempTrackers.Clear();
        CollisionQuadTree.RetrievePoint(_tempTrackers, x, z);
        var span = CollectionsMarshal.AsSpan(_tempTrackers);
        span.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        return span;
    }
}

public class WallCollision : ITransform, ICollidable
{
    public IReadOnlyList<ITransform> ChildTransforms => [];
    public f64Vector3 Position { get; set; }
    public f64Euler Rotation { get; set; }
    public ITransform? Parent => null;
    public Rad3dBoxDef[] Boxes { get; }
    public int MaxRadius { get; }
    
    public WallCollision(Rad3dBoxDef[] boxes)
    {
        Boxes = boxes;
        
        int maxRadius = 0;
        foreach (var box in Boxes)
        {
            int boxMax = (int)fix64.Ceiling(fix64.Max(box.Radius.X, fix64.Max(box.Radius.Y, box.Radius.Z)));
            if (boxMax > maxRadius)
            {
                maxRadius = boxMax;
            }
        }
        MaxRadius = maxRadius;
    }
}

public class StageObject : ITransform, IAiNode, ICollidable
{
    public Rad3d Rad { get; }
    public IReadOnlyList<ITransform> ChildTransforms => [];
    public f64Vector3 Position { get; set; }
    public f64Euler Rotation { get; set; }
    public ITransform? Parent { get; set; }
    public AiNodeKind Kind { get; set; } = AiNodeKind.Auto;
    public bool IsSpecial { get; set; }
    public Rad3dBoxDef[] Boxes { get; }
    public int MaxRadius { get; }
    public string FileName => Rad.FileName;

    public StageObject(Rad3d rad)
    {
        Rad = rad;
        Boxes = rad.Boxes;
        MaxRadius = rad.MaxRadius;
    }

    public StageObject(Rad3d rad, f64Vector3 position, f64Euler rotation) : this(rad)
    {
        Position = position;
        Rotation = rotation;
    }

    public void GameTick()
    {
    }
}