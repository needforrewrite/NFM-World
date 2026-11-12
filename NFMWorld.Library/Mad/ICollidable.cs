using NFMWorld.PluginSystem;
using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

[PluginVisible]
public interface ICollidable : ITransform
{
    [PluginVisible] IReadOnlyList<Rad3dBoxDef> Boxes { get; }
    [PluginVisible] int MaxRadius { get; }
    [PluginVisible] Rad3dCollisionMesh? CollisionMesh { get; }
    [PluginVisible] Rad3dCollisionHull? CollisionHull { get; }
}
