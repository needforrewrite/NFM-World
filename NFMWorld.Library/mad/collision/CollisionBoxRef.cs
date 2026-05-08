using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary.Collision;

// A struct for this would be ideal, but it's a very large object so it would cause enormous stack allocations
public class CollisionBoxRef : IQuadObject
{
    public readonly int Index;
    private readonly f64Bounds _bounds;
        
    // Box and GameObject position and rotation in world space
    public readonly f64Vector3 GameObjectPosition;
    public readonly fix64 GameObjectXz;
    public readonly Rad3dBoxDef Box;
        
    // Precomputed BoxRoad/BoxWall/BoxRamp for faster collision checks
    public readonly BoxRoad? BoxRoad;
    public readonly BoxWall? BoxWall;
    public readonly BoxRamp? BoxRamp;

    public CollisionBoxRef(
        fix64 gameObjectX,
        fix64 gameObjectY,
        fix64 gameObjectZ,
        fix64 gameObjectRotXz,
        Rad3dBoxDef box,
        fix64 radius,
        int index)
    {
        Index = index;
        GameObjectPosition = new f64Vector3(gameObjectX, gameObjectY, gameObjectZ);
        GameObjectXz = gameObjectRotXz;

        Box = box;
            
        var rad = box.Radius;
        var radFlipped = new f64Vector3(rad.Z, rad.Y, rad.X);
        var trackersPosition = box.Translation;

        if (box is { Xy: 0, Zy: 0 })
        {
            BoxRoad = new BoxRoad(rad, trackersPosition, gameObjectRotXz, GameObjectPosition);
        }
        else if (box.Zy == 90 || box.Zy == -90 || box.Xy == 90 || box.Xy == -90)
        {
            if (box.Zy == -90)
            {
                BoxWall = new BoxWall(rad, 0, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
            else if (box.Xy == 90)
            {
                BoxWall = new BoxWall(radFlipped, 90, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
            else if (box.Zy == 90)
            {
                BoxWall = new BoxWall(rad, 180, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
            else
            {
                BoxWall = new BoxWall(radFlipped, -90, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
        }
        else if ((box.Zy != 0 && box.Zy != 90 && box.Zy != -90) || (box.Xy != 0 && box.Xy != 90 && box.Xy != -90))
        {
            if (box.Zy != 0)
            {
                BoxRamp = new BoxRamp(rad, box.Zy, 0, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
            else
            {
                BoxRamp = new BoxRamp(radFlipped, box.Xy, -90, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
        }

        _bounds = new f64Bounds(
            gameObjectX - radius,
            gameObjectZ - radius,
            radius * 2,
            radius * 2
        );
    }

    public f64Bounds Bounds => _bounds;
}