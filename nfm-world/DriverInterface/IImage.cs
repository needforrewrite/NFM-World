namespace NFMWorld.DriverInterface;

public interface IImage
{
    public int Height { get; }
    public int Width { get; }
    
    public int GetHeight(object o) => Height;

    public int GetWidth(object o) => Width;
}