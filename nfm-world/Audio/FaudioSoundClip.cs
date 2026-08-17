using Microsoft.Xna.Framework.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;

namespace NFMWorld.Audio;

/// <summary>
/// Implements <see cref="ISoundClip"/> using FNA's SoundEffect/SoundEffectInstance
/// for short sound effect playback.
/// Replaces the ManagedBass-based SoundClip.
/// </summary>
public sealed class FaudioSoundClip : ISoundClip
{
    /// <summary>
    /// Global pool of all active sound clips for bulk operations (stop all, set all volumes).
    /// Must be accessed from the main/game thread only (no locking).
    /// </summary>
    private static readonly List<FaudioSoundClip> Pool = [];

    private readonly SoundEffect _effect;
    private SoundEffectInstance? _instance;

    /// <summary>
    /// Loads a sound effect from a filesystem path.
    /// Throws on failure (matching the old SoundClip contract).
    /// </summary>
    /// <param name="filePath">Absolute or relative filesystem path to the audio file.</param>
    public FaudioSoundClip(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Read file from disk (direct path, matching old behavior)
        var stream = VFS.OpenRead(filePath);

        // Decode to PCM (stream formats only; SFX are not tracker formats)
        using var result = AudioDecoder.Decode(stream, extension);

        // Create SoundEffect (non-looped by default — loop flag set per-play)
        _effect = new SoundEffect(result.PcmData, result.SampleRate, result.Channels);

        Pool.Add(this);
    }

    public void Play()
    {
        // Create a fresh instance each time (Stop destroys the FAudio voice)
        _instance?.Dispose();
        _instance = _effect.CreateInstance();
        _instance.IsLooped = false;
        _instance.Volume = IRadicalMusic.CurrentVolume;
        _instance.Play();
    }

    public void Loop()
    {
        // Create a fresh instance for looping
        _instance?.Dispose();
        _instance = _effect.CreateInstance();
        _instance.IsLooped = true;
        _instance.Volume = IRadicalMusic.CurrentVolume;
        _instance.Play();
    }

    public void Stop()
    {
        _instance?.Stop();
        _instance = null;
    }

    /// <summary>
    /// Stops all sound effects in the pool immediately.
    /// </summary>
    public static void StopAll()
    {
        foreach (var clip in Pool)
        {
            clip.Stop();
        }
    }

    /// <summary>
    /// Sets the volume on all active sound effect instances in the pool.
    /// </summary>
    /// <param name="vol">Volume in range [0.0, 1.0].</param>
    public static void SetAllVolumes(float vol)
    {
        foreach (var clip in Pool)
        {
            clip._instance?.Volume = vol;
        }
    }
}
