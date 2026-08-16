using System.Runtime.CompilerServices;
using Maxine.Extensions.UnionGen;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary.Collision;

public class CollisionShapeUnion : IUnion
{
    private readonly sbyte _unionKind;
    private readonly ShapeRoad _union1ShapeRoad;

    public CollisionShapeUnion(ShapeRoad value)
    {
        _union1ShapeRoad = value;
        _unionKind = 1;
    }

    public bool TryGetValue(out ShapeRoad value)
    {
        if (_unionKind == 1)
        {
            value = _union1ShapeRoad;
            return true;
        }

        value = default!;
        return false;
    }

    private readonly ShapeWall _union2ShapeWall;

    public CollisionShapeUnion(ShapeWall value)
    {
        _union2ShapeWall = value;
        _unionKind = 2;
    }

    public bool TryGetValue(out ShapeWall value)
    {
        if (_unionKind == 2)
        {
            value = _union2ShapeWall;
            return true;
        }

        value = default!;
        return false;
    }

    private readonly ShapeRamp _union3ShapeRamp;

    public CollisionShapeUnion(ShapeRamp value)
    {
        _union3ShapeRamp = value;
        _unionKind = 3;
    }

    public bool TryGetValue(out ShapeRamp value)
    {
        if (_unionKind == 3)
        {
            value = _union3ShapeRamp;
            return true;
        }

        value = default!;
        return false;
    }

    private readonly ShapeMesh _union4ShapeMesh;

    public CollisionShapeUnion(ShapeMesh value)
    {
        _union4ShapeMesh = value;
        _unionKind = 4;
    }

    public bool TryGetValue(out ShapeMesh value)
    {
        if (_unionKind == 4)
        {
            value = _union4ShapeMesh;
            return true;
        }

        value = default!;
        return false;
    }

    private readonly ShapeHull _union5ShapeHull;

    public CollisionShapeUnion(ShapeHull value)
    {
        _union5ShapeHull = value;
        _unionKind = 5;
    }

    public bool TryGetValue(out ShapeHull value)
    {
        if (_unionKind == 5)
        {
            value = _union5ShapeHull;
            return true;
        }

        value = default!;
        return false;
    }

    public object? Value => _unionKind switch
    {
        1 => _union1ShapeRoad,
        2 => _union2ShapeWall,
        3 => _union3ShapeRamp,
        4 => _union4ShapeMesh,
        5 => _union5ShapeHull,
        _ => null
    };

    public bool HasValue => _unionKind != 0;
}

public readonly record struct ShapeMesh(f64Vector3 GameObjectPosition, fix64 GameObjectXz, SrcRad3dCollisionMesh CollisionMesh);
public readonly record struct ShapeHull(f64Vector3 GameObjectPosition, fix64 GameObjectXz, SrcRad3dCollisionHull CollisionHull);

public readonly struct CollisionShapeRef : IQuadObject
{
    public readonly int Index;

    public readonly SurfaceType SurfaceType;
    public readonly fix64 TractionMultiplier;
    public readonly int Damage;
    public readonly bool NotWall;
    public readonly Color3 DustColor;

    public readonly CollisionShapeUnion Box;
    
    public f64Bounds Bounds { get; }

    public CollisionShapeRef(
        fix64 gameObjectX,
        fix64 gameObjectY,
        fix64 gameObjectZ,
        fix64 gameObjectRotXz,
        SrcRad3dCollisionMesh colMesh,
        fix64 radius,
        int index)
    {
        Index = index;
        
        Box = new CollisionShapeUnion(new ShapeMesh(new f64Vector3(gameObjectX, gameObjectY, gameObjectZ), gameObjectRotXz, colMesh));
        
        Bounds = new f64Bounds(
            gameObjectX - radius,
            gameObjectZ - radius,
            radius * 2,
            radius * 2
        );
    }

    public CollisionShapeRef(
        fix64 gameObjectX,
        fix64 gameObjectY,
        fix64 gameObjectZ,
        fix64 gameObjectRotXz,
        SrcRad3dCollisionHull colHull,
        fix64 radius,
        int index)
    {
        Index = index;
        
        Box = new CollisionShapeUnion(new ShapeHull(new f64Vector3(gameObjectX, gameObjectY, gameObjectZ), gameObjectRotXz, colHull));
        
        Bounds = new f64Bounds(
            gameObjectX - radius,
            gameObjectZ - radius,
            radius * 2,
            radius * 2
        );
    }

    public CollisionShapeRef(
        fix64 gameObjectX,
        fix64 gameObjectY,
        fix64 gameObjectZ,
        fix64 gameObjectRotXz,
        Rad3dBoxDef box,
        fix64 radius,
        int index)
    {
        Index = index;
        var gameObjectPosition = new f64Vector3(gameObjectX, gameObjectY, gameObjectZ);

        SurfaceType = box.SurfaceType;
        Damage = box.Damage;
        NotWall = box.NotWall;
        DustColor = box.Color;
        TractionMultiplier = box.TractionMultiplier ?? fix64.One;

        var rad = box.Radius;
        var radFlipped = new f64Vector3(rad.Z, rad.Y, rad.X);
        var trackersPosition = box.Translation;

        if (box is { Xy: 0, Zy: 0 })
        {
            Box = new CollisionShapeUnion(new ShapeRoad(rad, trackersPosition, gameObjectRotXz, gameObjectPosition));
        }
        else if (box.Zy == 90 || box.Zy == -90 || box.Xy == 90 || box.Xy == -90)
        {
            if (box.Zy == -90)
            {
                Box = new CollisionShapeUnion(new ShapeWall(rad, 0, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
            else if (box.Xy == 90)
            {
                Box = new CollisionShapeUnion(new ShapeWall(radFlipped, 90, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
            else if (box.Zy == 90)
            {
                Box = new CollisionShapeUnion(new ShapeWall(rad, 180, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
            else
            {
                Box = new CollisionShapeUnion(new ShapeWall(radFlipped, -90, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
        }
        else if ((box.Zy != 0 && box.Zy != 90 && box.Zy != -90) || (box.Xy != 0 && box.Xy != 90 && box.Xy != -90))
        {
            if (box.Zy != 0)
            {
                Box = new CollisionShapeUnion(new ShapeRamp(rad, box.Zy, 0, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
            else
            {
                Box = new CollisionShapeUnion(new ShapeRamp(radFlipped, box.Xy, -90, trackersPosition, gameObjectRotXz, gameObjectPosition));
            }
        }

        // Compute world-space center of this box for tight quadtree bounds
        var worldBoxPos = trackersPosition.RotateXz(gameObjectRotXz) + gameObjectPosition;
        // Conservative AABB extent: max of all radius components covers any local rotation (Xy/Zy)
        var extent = fix64.Max(rad.X, fix64.Max(rad.Y, rad.Z));
        Bounds = new f64Bounds(
            worldBoxPos.X - extent,
            worldBoxPos.Z - extent,
            extent * 2,
            extent * 2
        );
    }

    public bool TryGetValue(out ShapeMesh collisionMesh) => Box.TryGetValue(out collisionMesh);
    public bool TryGetValue(out ShapeHull collisionHull) => Box.TryGetValue(out collisionHull);
    public bool TryGetValue(out ShapeRoad boxRoad) => Box.TryGetValue(out boxRoad);
    public bool TryGetValue(out ShapeRamp boxRamp) => Box.TryGetValue(out boxRamp);
    public bool TryGetValue(out ShapeWall boxWall) => Box.TryGetValue(out boxWall);
}