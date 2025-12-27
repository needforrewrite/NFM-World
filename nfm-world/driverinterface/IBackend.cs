using File = NFMWorld.Util.File;

namespace NFMWorld.DriverInterface;

public interface IBackend
{
    public static IBackend Backend { get; set; }

    Vector2 Viewport();
    IRadicalMusic LoadMusic(File file, double tempomul);
    IImage LoadImage(File file);
    IImage LoadImage(ReadOnlySpan<byte> file);
    void StopAllSounds();
    ISoundClip GetSound(string filePath);
    IGraphics Graphics { get; }
    void SetAllVolumes(float vol);
}