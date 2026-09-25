using NFMWorld.Graphics;
using StbImageSharp;

namespace NFMWorld.Util;

public static class TextureLoader
{
    public static ITexture LoadFromStream(IGraphicsDevice device, Stream stream)
    {
        var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        
        return device.CreateTexture(new TextureDesc(result.Width, result.Height, TextureFormat.Rgba8), result.Data);
    }
}
