using WorldXaml.UI.Yoga;

namespace NFMWorld.DriverInterface;

public interface IBackend : IXamlGraphicsBackend
{
    public new static IBackend Backend
    {
        get
        {
            return field ?? ThrowNotInitialized();

            IBackend ThrowNotInitialized()
            {
                throw new InvalidOperationException(
                    $"{nameof(IBackend)}.{nameof(Backend)} needs to be set before it can be used.");
            }
        }
        set
        {
            field = value;
            IXamlGraphicsBackend.Backend = value;
        }
    }

    float IXamlGraphicsBackend.Scale => Scale;
    System.Numerics.Vector2 IXamlGraphicsBackend.Viewport => Viewport;
    IXamlGraphics IXamlGraphicsBackend.Graphics => Graphics;

    new float Scale { get; set; }
    new Vector2 Viewport { get; }
    IRadicalMusic LoadMusic(string file, double tempomul);
    IImage LoadImage(string file);
    IImage LoadCachedImage(string file);
    IImage LoadImage(ReadOnlySpan<byte> file);
    void StopAllSounds();
    ISoundClip GetSound(string filePath);
    new IGraphics Graphics { get; }
    void SetAllVolumes(float vol);
}