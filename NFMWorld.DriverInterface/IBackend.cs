namespace NFMWorld.DriverInterface.DriverInterface;

public interface IBackend
{
    public static IBackend Backend
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
        set;
    }

    [ClientOnly]
    IRadicalMusic LoadMusic(string file, double tempomul = 1);
    
    [ClientOnly]
    void StopAllSounds();
    
    [ClientOnly]
    ISoundClip GetSound(string filePath);
    
    [ClientOnly]
    IGraphics Graphics { get; }
    
    [ClientOnly]
    void SetAllVolumes(float vol);

    [ClientOnly]
    Key GetKeyFromScancode(Key key);
}