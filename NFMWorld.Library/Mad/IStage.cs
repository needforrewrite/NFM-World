using nfm_world_library.Lua;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

[LuaVisible]
public partial interface IStage
{
    [LuaHidden]
    ReadOnlySpan<CollisionShapeRef> RetrievePointCollidables(fix64 x, fix64 z);
    IReadOnlyList<ITransform> pieces { get; }
    IReadOnlyList<IAiNode> nodes { get; }
    IReadOnlyList<IAiNode> checkpoints { get; }
    IReadOnlyList<IAiNode> fixHoops { get; }
    ushort nlaps { get; }
    ITransform CreateObject(string objectName, int x, int y, int z, int xz); 
}