using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

public interface ICollidable : ITransform
{
    IReadOnlyList<Rad3dBoxDef> Boxes { get; }
    int MaxRadius { get; }
    SrcRad3dCollisionMesh? CollisionMesh { get; }
    SrcRad3dCollisionHull? CollisionHull { get; }
}