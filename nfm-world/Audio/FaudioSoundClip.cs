using System.Runtime.InteropServices;
using NFMWorld.Audio.FAudioBindings;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Audio;

/// <summary>
/// Implements <see cref="ISoundClip"/> by driving an FAudio source voice directly, for short
/// sound effect playback. Replaces the FNA <c>SoundEffect</c>/<c>SoundEffectInstance</c>-based
/// implementation.
/// </summary>
public sealed class FaudioSoundClip : ISoundClip
{
    /// <summary>
    /// Global pool of all active sound clips for bulk operations (stop all, set all volumes).
    /// Must be accessed from the main/game thread only (no locking).
    /// </summary>
    private static readonly List<FaudioSoundClip> Pool = [];
    private static bool _paused;

    /// <summary>
    /// Decoded PCM shared between the sample and its spatial instances.
    /// Each playing voice pins it until that voice is destroyed.
    /// </summary>
    private readonly byte[] _pcm;

    private readonly int _sampleRate;
    private readonly int _channels;
    private byte[]? _monoPcm;
    private readonly bool _spatial;
    private readonly bool _followPosition;
    private bool _loop;
    private bool _disposed;
    private Vector3 _position;
    private Vector3 _playPosition;

    private IntPtr _voice;
    private GCHandle _pcmHandle;

    /// <summary>
    /// Loads a sound effect from a filesystem path.
    /// Throws on failure (matching the old SoundClip contract).
    /// </summary>
    /// <param name="filePath">Absolute or relative filesystem path to the audio file.</param>
    public FaudioSoundClip(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        using var stream = VFS.OpenRead(filePath);

        // Decode to PCM (stream formats only; SFX are not tracker formats)
        using var result = AudioDecoder.Decode(stream, extension);

        // Copy out of the decoder's pooled buffer: disposing `result` returns it to the pool, and
        // this clip outlives that scope. The old SoundEffect-based implementation copied too.
        _pcm = result.PcmData.AsSpan(0, result.PcmData.Count).ToArray();
        _sampleRate = result.SampleRate;
        _channels = (int)result.Channels;
    }

    private FaudioSoundClip(FaudioSoundClip sample, bool followPosition)
    {
        _pcm = sample._channels == 1 ? sample._pcm : (sample._monoPcm ??= SpatialAudio.ToMono(sample._pcm, sample._channels));
        _sampleRate = sample._sampleRate;
        _channels = 1;
        _spatial = true;
        _followPosition = followPosition;
    }

    public ISoundClip CreateSpatialInstance(bool followPosition = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new FaudioSoundClip(this, followPosition);
    }

    public void SetPosition(Vector3 position)
    {
        if (!SpatialAudio.Finite(position)) return;
        _position = position;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopVoice();
    }

    public void Play() => StartVoice(loop: false);
    public bool IsActive => _voice != IntPtr.Zero;

    public void Loop() => StartVoice(loop: true);

    private void StartVoice(bool loop)
    {
        if (_disposed) return;
        // Voices are single-use once their buffer has played out, so every start builds a fresh
        // one - matching the old per-Play SoundEffectInstance.
        StopVoice();
        _loop = loop;
        _playPosition = _position;
        if (_spatial && !loop && SpatialAudio.Gain(_playPosition) <= 0) return;

        var engine = FaudioEngine.Instance;
        if (engine is null)
            return; // no audio device - play silently rather than failing

        _voice = engine.CreateSourceVoice(_sampleRate, _channels);
        if (_voice == IntPtr.Zero || _pcm.Length == 0)
        {
            StopVoice();
            return;
        }

        // Pinned per voice, not per clip: the previous voice still reads this array while it fades
        // out, so it keeps its own pin and this one can be freed when this voice is destroyed.
        _pcmHandle = GCHandle.Alloc(_pcm, GCHandleType.Pinned);
        if (!TrySubmitBuffer(loop))
        {
            StopVoice();
            return;
        }

        Pool.Add(this);
        ApplySpatial();
        if (!_paused)
            FAudio.FAudioSourceVoice_Start(_voice, 0, 0);
    }

    /// <summary>Releases the pin taken for a voice once that voice is destroyed.</summary>
    private static void ReleasePcm(GCHandle pin)
    {
        if (pin.IsAllocated)
            pin.Free();
    }

    /// <summary>Queues this clip's pinned PCM on the current voice, optionally looping forever.</summary>
    private bool TrySubmitBuffer(bool loop)
    {
        var buffer = new FAudio.FAudioBuffer
        {
            Flags = 0,
            AudioBytes = (uint)_pcm.Length,
            pAudioData = _pcmHandle.AddrOfPinnedObject(),
            PlayBegin = 0,
            PlayLength = 0, // 0 = to the end of the buffer
            LoopBegin = 0,
            LoopLength = 0, // 0 = to the end of the buffer
            LoopCount = loop ? FAudio.FAUDIO_LOOP_INFINITE : 0,
            pContext = IntPtr.Zero,
        };

        return FAudio.FAudioSourceVoice_SubmitSourceBuffer(_voice, ref buffer, IntPtr.Zero) == 0;
    }

    public void Stop() => StopVoice();

    private void StopVoice(bool fade = true)
    {
        Pool.Remove(this);
        if (_voice == IntPtr.Zero)
            return;

        var voice = _voice;
        var pin = _pcmHandle;
        _voice = IntPtr.Zero;
        _pcmHandle = default;

        // Destroying the voice is what makes FAudio stop reading pAudioData, so this voice's pin is
        // only released once it is gone. A restart may already own a new voice and pin by then.
        var engine = FaudioEngine.Instance;
        if (engine is not null && fade)
            engine.FadeOutAndDestroy(voice, () => ReleasePcm(pin));
        else
        {
            engine?.DestroyVoice(voice);
            ReleasePcm(pin);
        }
    }

    private void ApplySpatial(float? volume = null)
    {
        if (_voice == IntPtr.Zero) return;
        var position = _loop || _followPosition ? _position : _playPosition;
        if (_spatial)
            FaudioEngine.Instance?.ApplySpatial(_voice, SpatialAudio.ListenerPosition,
                SpatialAudio.ListenerForward, SpatialAudio.ListenerUp, position);
        FAudio.FAudioVoice_SetVolume(_voice, (volume ?? SoundEffects.Volume) * (_spatial ? SpatialAudio.Gain(position) : 1), 0);
    }

    public static void UpdateSpatialSounds()
    {
        for (var i = Pool.Count - 1; i >= 0; i--)
        {
            var clip = Pool[i];
            FAudio.FAudioSourceVoice_GetState(clip._voice, out var state, FAudio.FAUDIO_VOICE_NOSAMPLESPLAYED);
            if (state.BuffersQueued == 0)
                clip.StopVoice(fade: false);
            else if (clip._spatial)
                clip.ApplySpatial();
        }
    }

    /// <summary>
    /// Stops all sound effects in the pool. Each one fades out rather than cutting off.
    /// </summary>
    public static void StopAll()
    {
        foreach (var clip in Pool.ToArray())
        {
            clip.Stop();
        }
        _paused = false;
    }

    public static void SetAllPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        foreach (var clip in Pool)
        {
            if (clip._voice == IntPtr.Zero) continue;
            if (paused)
                FAudio.FAudioSourceVoice_Stop(clip._voice, 0, 0);
            else
                FAudio.FAudioSourceVoice_Start(clip._voice, 0, 0);
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
            if (clip._voice != IntPtr.Zero)
                clip.ApplySpatial(vol);
        }
    }
}
