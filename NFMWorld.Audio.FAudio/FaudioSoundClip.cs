using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Audio.FAudio;

/// <summary>
/// Placeholder for the direct-FAudio-backed <see cref="ISoundClip"/> implementation.
/// See <see cref="FaudioMusic"/> for the migration this project performs in Milestone 4.
/// </summary>
public sealed class FaudioSoundClip : ISoundClip
{
    public void Play() => throw new NotImplementedException("Implemented in Milestone 4.");
    public void Loop() => throw new NotImplementedException("Implemented in Milestone 4.");
    public void Stop() => throw new NotImplementedException("Implemented in Milestone 4.");
}
