using NFMWorld.PluginSystem;

namespace NFMWorld.DriverInterface.DriverInterface;

/// <summary>
/// A loaded sound. Returned by <see cref="IBackend.GetSound"/>, which is plugin-visible, so the
/// type has to be too: the plugin holds, plays and disposes the returned handle.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
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
