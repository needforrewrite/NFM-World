using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using Silk.NET.Maths;
using SkiaSharp;
using File = NFMWorld.Util.File;

namespace NFMWorld;

internal class SkiaSharpBackend(SKCanvas canvas) : IBackend
{
    public IRadicalMusic LoadMusic(File file)
    {
        return new RadicalMusic(file);
    }

    public IImage LoadImage(File file)
    {
        return new MadSharpSKImage(System.IO.File.ReadAllBytes(file.Path));
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        return new MadSharpSKImage(file);
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; } = new SkiaSharpGraphics(canvas);

    public Vector2D<float> Ratio
    {
        set => ((SkiaSharpGraphics)Graphics).Ratio = value;
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}