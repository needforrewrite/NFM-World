using System.Runtime.InteropServices;
using Maxine.Extensions.Collections;
using NFMWorld.Audio.FAudioBindings;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;

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

    /// <summary>
    /// Decoded PCM for this clip, kept alive for the clip's lifetime: FAudio reads from it
    /// whenever a voice has it queued. A plain owned copy rather than the decoder's pooled
    /// segment, because <see cref="ISoundClip"/> has no Dispose - there is nowhere to
    /// return a pooled buffer to.
    /// </summary>
    private readonly byte[] _pcm;

    private readonly int _sampleRate;
    private readonly int _channels;

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

        // Read file from disk (direct path, matching old behavior)
        var stream = VFS.OpenRead(filePath);

        // Decode to PCM (stream formats only; SFX are not tracker formats)
        using var result = AudioDecoder.Decode(stream, extension);

        // Copy out of the decoder's pooled buffer: disposing `result` returns it to the pool, and
        // this clip outlives that scope. The old SoundEffect-based implementation copied too.
        _pcm = result.PcmData.AsSpan(0, result.PcmData.Count).ToArray();
        _sampleRate = result.SampleRate;
        _channels = (int)result.Channels;

        Pool.Add(this);
    }

    public void Play() => StartVoice(loop: false);

    public void Loop() => StartVoice(loop: true);

    private void StartVoice(bool loop)
    {
        // Voices are single-use once their buffer has played out, so every start builds a fresh
        // one - matching the old per-Play SoundEffectInstance.
        StopVoice();

        var engine = FaudioEngine.Instance;
        if (engine is null)
            return; // no audio device - play silently rather than failing

        _voice = engine.CreateSourceVoice(_sampleRate, _channels);
        if (_voice == IntPtr.Zero || !TrySubmitBuffer(loop))
        {
            StopVoice();
            return;
        }

        FAudio.FAudioVoice_SetVolume(_voice, IRadicalMusic.CurrentVolume, 0);
        FAudio.FAudioSourceVoice_Start(_voice, 0, 0);
    }

    /// <summary>Pins this clip's PCM and queues it on the current voice, optionally looping forever.</summary>
    private bool TrySubmitBuffer(bool loop)
    {
        if (_pcm.Length == 0)
            return false;

        _pcmHandle = GCHandle.Alloc(_pcm, GCHandleType.Pinned);

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

    private void StopVoice()
    {
        if (_voice == IntPtr.Zero)
            return;

        FAudio.FAudioSourceVoice_Stop(_voice, 0, 0);
        // Destroying the voice is what makes FAudio stop reading pAudioData, so the pin can only
        // be released after this call.
        FaudioEngine.Instance?.DestroyVoice(_voice);
        _voice = IntPtr.Zero;

        if (_pcmHandle.IsAllocated)
            _pcmHandle.Free();
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
            if (clip._voice != IntPtr.Zero)
                FAudio.FAudioVoice_SetVolume(clip._voice, vol, 0);
        }
    }
}
