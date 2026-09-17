namespace NFMWorld.Graphics;

public interface ISwapchain
{
    int Width { get; }
    int Height { get; }
    void Present();
}
