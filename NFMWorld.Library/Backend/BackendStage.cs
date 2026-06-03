using System.Buffers;
using System.Runtime.InteropServices;
using FixedMathSharp;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
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

    public BackendStage(string stageName, StageLoader stageLoader) : this()
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
            Logging.Error($"Error in stage: {stageName}\nAt line: {exception.Line} (number {exception.LineNumber})\n{exception.ToString()}");
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}\n{exception.ToString()}");
        }
    }

    public BackendStage(string stageName) : this()
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
            Logging.Error($"Error in stage: {stageName}\nAt line: {exception.Line} (number {exception.LineNumber})\n{exception.ToString()}");
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {stageName}\n{exception.ToString()}");
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
                        piece.Rotation,
                        piece
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
                        piece.Rotation,
                        piece
                    )
                    {
                        Kind = AiNodeKind.CheckPoint
                    };
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
                        piece.Rotation,
                        piece
                    )
                    {
                        Kind = AiNodeKind.FixHoop
                    };
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

        SetBounds(stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb, stageLoader.maxt - stageLoader.maxb);
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
        
        CollisionQuadTree = new QuadTree<CollisionShapeRef>(sx, sz, ncx, ncz);
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

        var position = new f64Vector3(x, 250 - y, z);
        var rotation = new f64Euler(f64AngleSingle.FromDegrees(r), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle);
        var mesh = StageObject.CreateDefaultObject(part.Rad, position, rotation);
        pieces[stagePartCount] = mesh;

        Logging.Info($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}");

        AddToQuadTree(mesh);

        return mesh;
    }
    
    private QuadTree<CollisionShapeRef> CollisionQuadTree = new(0,0,0,0);
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
            xz = transform.EulerAngles.Xz.Degrees;
        }
        
        foreach (var box in mesh.Boxes)
        {
            CollisionQuadTree.Insert(new CollisionShapeRef(
                gameObjectX: x,
                gameObjectY: y,
                gameObjectZ: z,
                gameObjectRotXz: xz,
                box: box,
                radius: 0, // bounds are now computed from world-space box position inside the constructor
                index: _quadTreeInsertionIndex++
            ));
        }

        if (mesh.CollisionMesh is { } colMesh)
        {
            var maxR = mesh.MaxRadius;
            CollisionQuadTree.Insert(new CollisionShapeRef(
                gameObjectX: x,
                gameObjectY: y,
                gameObjectZ: z,
                gameObjectRotXz: xz,
                colMesh: colMesh,
                maxR,
                index: _quadTreeInsertionIndex++
            ));
        }

        if (mesh.CollisionHull is { } colHull)
        {
            var maxR = mesh.MaxRadius;
            CollisionQuadTree.Insert(new CollisionShapeRef(
                gameObjectX: x,
                gameObjectY: y,
                gameObjectZ: z,
                gameObjectRotXz: xz,
                colHull: colHull,
                maxR,
                index: _quadTreeInsertionIndex++
            ));
        }
    }
    
    private List<CollisionShapeRef> _tempTrackers = new();

    public ReadOnlySpan<CollisionShapeRef> RetrievePointCollidables(fix64 x, fix64 z)
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
    public FixedQuaternion Rotation { get; set; }

    public f64Euler EulerAngles
    {
        get => Rotation.ToEuler();
        set => Rotation = FixedQuaternion.FromEuler(value);
    }
    public ITransform? Parent => null;
    public Rad3dBoxDef[] Boxes { get; }
    public int MaxRadius { get; }
    public SrcRad3dCollisionMesh? CollisionMesh => null;
    public SrcRad3dCollisionHull? CollisionHull => null;

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

public class StageObject(Rad3d rad) : ITransform, IAiNode, ICollidable
{
    public PiecePlacement OriginalPlacement { get; set; }

    public Rad3d Rad { get; } = rad;
    public IReadOnlyList<ITransform> ChildTransforms => [];
    public f64Vector3 Position { get; set; }
    public FixedQuaternion Rotation { get; set; }
    public ITransform? Parent { get; set; }
    public AiNodeKind Kind { get; set; } = AiNodeKind.Auto;
    public bool IsSpecial { get; set; }
    public Rad3dBoxDef[] Boxes { get; } = rad.Boxes;
    public int MaxRadius { get; } = rad.MaxRadius;
    public string FileName => Rad.FileName;

    public f64Euler EulerAngles
    {
        get => Rotation.ToEuler();
        set => Rotation = FixedQuaternion.FromEuler(value);
    }

    public SrcRad3dCollisionMesh? CollisionMesh { get; set; } = rad.CollisionMesh;
    public SrcRad3dCollisionHull? CollisionHull { get; set; } = rad.CollisionHull;

    public StageObject(Rad3d rad, f64Vector3 position, f64Euler rotation, PiecePlacement originalPlacement) : this(rad)
    {
        Position = position;
        EulerAngles = rotation;
        OriginalPlacement = originalPlacement;
    }

    public static StageObject CreateDefaultObject(Rad3d rad, f64Vector3 position, f64Euler rotation, PiecePlacementType placementType = PiecePlacementType.CollisionObject, AiNodeKind? aiNodeKind = null, bool isSpecial = false, bool isWall = false)
    {
        return new StageObject(rad, position, rotation, new PiecePlacement(placementType, rad, position, rotation, aiNodeKind, isSpecial, isWall));
    }

    public void GameTick()
    {
    }
}