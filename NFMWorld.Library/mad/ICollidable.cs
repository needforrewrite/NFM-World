using NFMWorldLibrary.Rad;

namespace NFMWorldLibrary;

public interface ICollidable : ITransform
{
    public Rad3dBoxDef[] Boxes { get; }
    public int MaxRadius { get; }
}