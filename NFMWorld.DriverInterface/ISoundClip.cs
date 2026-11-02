namespace NFMWorld.DriverInterface.DriverInterface;

[ClientOnly]
public interface ISoundClip : IDisposable
{
    bool IsActive { get; }
    void Play();
    void Loop();
    void Stop();
    ISoundClip CreateSpatialInstance(bool followPosition = false);
    void SetPosition(System.Numerics.Vector3 position);
}
