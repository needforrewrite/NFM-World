using NFMWorld.DriverInterface;
using SkiaSharp;

namespace NFMWorld;

internal class MadSharpSKImage : IImage
{
    internal readonly SKImage SkImage;

    public MadSharpSKImage(ReadOnlySpan<byte> file)
    {
        SkImage = SKImage.FromEncodedData(file);
    }

    public int Height => SkImage.Width;
    public int Width => SkImage.Height;
}