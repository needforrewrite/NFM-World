using System.Buffers;
using System.Runtime.InteropServices;
using nfm_world_library.Lua;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorldLibrary.Backend;

[LuaVisible]
public partial class BackendStage
{
    [LuaName] public LuaUnlimitedArray<BackendGameObject> Pieces { get; } = [];
    [LuaName] public LuaUnlimitedArray<StageObject> Nodes { get; } = [];
    [LuaName] public LuaUnlimitedArray<StageObject> Checkpoints { get; } = [];
    [LuaName] public LuaUnlimitedArray<StageObject> FixHoops { get; } = [];
    [LuaName] public ushort Nlaps { get; set; }

    [LuaName] public string Name = "hogan rewish";

    [LuaName] public readonly string Path;
    
    // left
    public int Sx;
    // top
    public int Sz;
    // width
    public int Ncx;
    // height
    public int Ncz;

    public int StagePartCount => Pieces.Count;

    [LuaName] public readonly StageLoader StageLoader;

    protected BackendStage()
    {
        // Creates an empty stage for inheritance
        Path = "~empty~";
        StageLoader = new StageLoader();
    }

    public BackendStage(string stageName, StageLoader? stageLoader = null) : this()
    {
        Path = stageName;
        try
        {
            this.StageLoader = stageLoader ?? new StageLoader(stageName);
            LoadStageInternal(this.StageLoader);
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
                    Pieces[StagePartCount] = obj;
                    if (piece.NodeKind is { } nodeKind)
                    {
                        Nodes[Nodes.Count] = obj;
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
                    Pieces[StagePartCount] = obj;
                    Nodes[Nodes.Count] = obj;
                    Checkpoints[Checkpoints.Count] = obj;

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
                    Pieces[StagePartCount] = fix;

                    FixHoops[FixHoops.Count] = fix;
                    Nodes[Nodes.Count] = fix;
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

        Nlaps = stageLoader.nlaps;
        Name = stageLoader.Name;
            
        // stage walls
        if (stageLoader.walls.Count > 0)
        {
            Pieces[StagePartCount] = new WallCollision([..stageLoader.walls]);
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
        
        _collisionQuadTree = new QuadTree<CollisionShapeRef>(sx, sz, ncx, ncz);
        foreach (var piece in Pieces)
        {
            if (piece is ICollidable collidable)
            {
                AddToQuadTree(collidable);
            }
        }
        _collisionQuadTree.TrimExcess();
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
        Pieces[StagePartCount] = mesh;

        Logging.Info($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}");

        AddToQuadTree(mesh);

        return mesh;
    }
    
    private QuadTree<CollisionShapeRef> _collisionQuadTree = new(0,0,0,0);
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
            _collisionQuadTree.Insert(new CollisionShapeRef(
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
            _collisionQuadTree.Insert(new CollisionShapeRef(
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
            _collisionQuadTree.Insert(new CollisionShapeRef(
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
    
    private readonly List<CollisionShapeRef> _tempTrackers = new();
    public ReadOnlySpan<CollisionShapeRef> RetrievePointCollidables(fix64 x, fix64 z)
    {
        _tempTrackers.Clear();
        _collisionQuadTree.RetrievePoint(_tempTrackers, x, z);
        var span = CollectionsMarshal.AsSpan(_tempTrackers);
        span.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        return span;
    }
}

[LuaVisible]
public partial class WallCollision : BackendGameObject, ICollidable
{
    [LuaName]
    public LuaArray<Rad3dBoxDef> Boxes { get; }
    
    IReadOnlyList<Rad3dBoxDef> ICollidable.Boxes => Boxes;
    
    [LuaName]
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

[LuaVisible]
public partial class StageObject(Rad3d rad) : BackendGameObject, IAiNode, ICollidable
{
    [LuaName("originalPlacement")]
    public PiecePlacement OriginalPlacement { get; set; }

    [LuaName("rad")]
    public Rad3d Rad { get; } = rad;
    
    [LuaName("nodeKind")]
    public AiNodeKind Kind { get; set; } = AiNodeKind.Auto;
    
    [LuaName]
    public bool IsSpecial { get; set; }
    
    [LuaName]
    public LuaArray<Rad3dBoxDef> Boxes { get; } = rad.Boxes;
    IReadOnlyList<Rad3dBoxDef> ICollidable.Boxes => Boxes;
    
    [LuaName]
    public int MaxRadius { get; } = rad.MaxRadius;
    
    [LuaName]
    public string FileName => Rad.FileName;

    public SrcRad3dCollisionMesh? CollisionMesh { get; set; } = rad.CollisionMesh;
    public SrcRad3dCollisionHull? CollisionHull { get; set; } = rad.CollisionHull;

    public StageObject(Rad3d rad, f64Vector3 position, f64Euler rotation, PiecePlacement originalPlacement) : this(rad)
    {
        Position = position;
        Rotation = rotation;
        OriginalPlacement = originalPlacement;
    }

    public static StageObject CreateDefaultObject(Rad3d rad, f64Vector3 position, f64Euler rotation, PiecePlacementType placementType = PiecePlacementType.CollisionObject, AiNodeKind? aiNodeKind = null, bool isSpecial = false, bool isWall = false)
    {
        return new StageObject(rad, position, rotation, new PiecePlacement(placementType, rad, position, rotation, aiNodeKind, isSpecial, isWall));
    }
}