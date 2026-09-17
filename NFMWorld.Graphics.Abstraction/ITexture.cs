namespace NFMWorld.Graphics;

public readonly record struct TextureDesc(
    int Width,
    int Height,
    TextureFormat Format,
    bool RenderTargetable = false,
    bool MipMapped = false);

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    TextureFormat Format { get; }
}
