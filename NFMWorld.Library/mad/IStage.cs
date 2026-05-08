using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Mad.Collision;

namespace NFMWorldLibrary.Mad;

public interface IStage
{
    ReadOnlySpan<CollisionBoxRef> RetrievePointCollidables(fix64 x, fix64 z);
    IReadOnlyList<ITransform> pieces { get; }
    IReadOnlyList<IAiNode> nodes { get; }
    IReadOnlyList<IAiNode> checkpoints { get; }
    IReadOnlyList<IAiNode> fixHoops { get; }
    ushort nlaps { get; }
    ITransform CreateObject(string objectName, int x, int y, int z, int xz);
}