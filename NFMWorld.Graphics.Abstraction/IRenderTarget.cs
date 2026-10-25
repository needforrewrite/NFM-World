namespace NFMWorld.Graphics;

public readonly record struct RenderTargetDesc(
    int Width,
    int Height,
    TextureFormat ColorFormat,
    bool HasDepthStencil = true,
    TextureFormat DepthStencilFormat = TextureFormat.Depth24Stencil8);

public interface IRenderTarget : IDisposable
{
    ITexture ColorTexture { get; }
    ITexture? DepthStencilTexture { get; }
}
