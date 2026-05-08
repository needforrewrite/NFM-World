using NFMWorldLibrary.Mad.Rad;

namespace NFMWorldLibrary.Mad;

public interface ICollidable : ITransform
{
    public Rad3dBoxDef[] Boxes { get; }
    public int MaxRadius { get; }
}