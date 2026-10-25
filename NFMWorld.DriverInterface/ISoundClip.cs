namespace NFMWorld.DriverInterface.DriverInterface;

[ClientOnly]
public interface ISoundClip
{
    void Play();
    void Loop();
    void Stop();
}