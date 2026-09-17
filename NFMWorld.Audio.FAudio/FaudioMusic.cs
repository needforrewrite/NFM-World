using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Audio.FAudio;

/// <summary>
/// Placeholder for the direct-FAudio-backed <see cref="IRadicalMusic"/> implementation.
/// Milestone 4 fills this in by vendoring FNA/lib/FAudio/csharp/FAudio.cs (a self-contained
/// P/Invoke class with no XNA dependency) and driving FAudioSourceVoice/FAudioBuffer directly,
/// replacing the current FNA SoundEffect/SoundEffectInstance-based implementation in
/// nfm-world/Audio/FaudioMusic.cs.
/// </summary>
public sealed class FaudioMusic : IRadicalMusic
{
    public void Dispose() => throw new NotImplementedException("Implemented in Milestone 4.");
    public void SetPaused(bool p0) => throw new NotImplementedException("Implemented in Milestone 4.");
    public void Play() => throw new NotImplementedException("Implemented in Milestone 4.");
    public void SetVolume(float vol) => throw new NotImplementedException("Implemented in Milestone 4.");
    public float GetVolume() => throw new NotImplementedException("Implemented in Milestone 4.");
    public void SetFreqMultiplier(double multiplier) => throw new NotImplementedException("Implemented in Milestone 4.");
}
