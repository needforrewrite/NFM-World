namespace NFMWorld.DriverInterface;

public interface IBackend
{
    public static IBackend Backend { get; set; }

    float Scale { get; set; }
    Vector2 Viewport { get; }
    IRadicalMusic LoadMusic(string file, double tempomul);
    IImage LoadImage(string file);
    IImage LoadCachedImage(string file);
    IImage LoadImage(ReadOnlySpan<byte> file);
    void StopAllSounds();
    ISoundClip GetSound(string filePath);
    IGraphics Graphics { get; }
    void SetAllVolumes(float vol);
}