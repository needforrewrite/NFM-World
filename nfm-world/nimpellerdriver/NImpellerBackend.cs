using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NImpeller;
using Silk.NET.Maths;
using File = NFMWorld.Util.File;

namespace NFMWorld;

internal class NImpellerBackend : IBackend
{
    public NImpellerBackend()
    {
        Graphics = new NImpellerGraphics(this);
    }

    public ImpellerDisplayListBuilder? DrawListBuilder { get; set; }

    public Vector2D<float> Ratio
    {
        set => ((NImpellerGraphics)Graphics).Ratio = value;
    }

    public IRadicalMusic LoadMusic(File file)
    {
        return new RadicalMusic(file);
    }

    public IImage LoadImage(File file)
    {
        throw new NotImplementedException();
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        throw new NotImplementedException();
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}