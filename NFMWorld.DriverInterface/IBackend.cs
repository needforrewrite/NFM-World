using NFMWorld.PluginSystem;

namespace NFMWorld.DriverInterface.DriverInterface;

[PluginVisible]
public interface IBackend
{
    public static IBackend Backend
    {
        [PluginVisible]
        get
        {
            return field ?? ThrowNotInitialized();

            IBackend ThrowNotInitialized()
            {
                throw new InvalidOperationException(
                    $"{nameof(IBackend)}.{nameof(Backend)} needs to be set before it can be used."
                );
            }
        }
        set;
    }

    [PluginVisible]
    [ClientOnly]
    IRadicalMusic LoadMusic(string file, double tempomul = 1);

    [ClientOnly]
    void StopAllSounds();

    [PluginVisible]
    [ClientOnly]
    ISoundClip GetSound(string filePath);

    [PluginVisible]
    [ClientOnly] IGraphics Graphics { get; }

    [PluginVisible]
    [ClientOnly] IGraphics3D Graphics3D { get; }

    [ClientOnly]
    void SetAllVolumes(float vol);

    [ClientOnly]
    Key GetKeyFromScancode(Key key);
}
