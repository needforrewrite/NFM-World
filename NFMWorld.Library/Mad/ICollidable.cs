using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

public interface ICollidable : ITransform
{
    IReadOnlyList<Rad3dBoxDef> Boxes { get; }
    int MaxRadius { get; }
    Rad3dCollisionMesh? CollisionMesh { get; }
    Rad3dCollisionHull? CollisionHull { get; }
}