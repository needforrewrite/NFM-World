// Web Audio output for the game's decoded PCM.
//
// FAudio has no wasm build, so the browser plays through AudioBufferSourceNodes instead of source
// voices; everything above this - the decoders, the tempo logic, the callers - is unchanged. The
// same file compiles for the desktop head (this project is shared), where every call is a no-op and
// the FAudio classes are used, so the browser-only pieces sit behind WEB_AUDIO.

using NFMWorld.DriverInterface.DriverInterface;
#if WEB_AUDIO
using System.Runtime.InteropServices.JavaScript;
#endif

namespace NFMWorld.Audio;

#if !WEB_AUDIO
/// <summary>
/// Stand-in for the browser's audio backend on a desktop build, where <c>WebAudioSoundClip</c> and
/// <c>WebAudioMusic</c> are never constructed. Exists so the browser classes do not need their own
/// conditional compilation.
/// </summary>
public static partial class WebAudioBackend
{
    public static bool IsAvailable => false;

    internal static int CreateBuffer(ReadOnlySpan<byte> pcm16, int sampleRate, int channels) => 0;
    internal static int StartBuffer(int buffer, double volume, double rate, bool loop) => 0;
    internal static int StartBufferAt(int buffer, double volume, double rate, bool loop, double offset) => 0;
    internal static void StopSource(int source) { }
    internal static void SetSourceVolume(int source, double volume) { }
    internal static void SetSourceRate(int source, double rate) { }
    internal static void SetSourcePaused(int source, bool paused) { }
    internal static void ReleaseBuffer(int buffer) { }
    internal static void StopAll() { }
    public static void SetAllVolumes(float volume) { }
    public static void StopAllSounds() { }
}
#else
/// <summary>
/// <see cref="ISoundClip"/> and <see cref="IRadicalMusic"/> over the page's Web Audio API.
///
/// The decoded buffers are handed to JavaScript once per clip/track as a byte copy, so the game's
/// pooled arrays can be returned as soon as the AudioBuffer exists.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class WebAudioBackend
{
    private const string Module = "audio.js";

    /// <summary>Whether the page has an AudioContext. False when construction is blocked (no user
    /// gesture yet), in which case every call is a no-op - the game plays silently, as it does on a
    /// machine without an audio device.</summary>
    public static bool IsAvailable
    {
        get
        {
            try { return AudioAvailable(); }
            catch { return false; }
        }
    }

    [JSImport("audioAvailable", Module)]
    private static partial bool AudioAvailable();

    /// <summary>Copies PCM into an AudioBuffer and returns its handle, or 0 on failure.</summary>
    internal static int CreateBuffer(ReadOnlySpan<byte> pcm16, int sampleRate, int channels)
    {
        if (pcm16.IsEmpty) return 0;
        return CreateBufferNative(pcm16.ToArray(), sampleRate, channels);
    }

    [JSImport("createBuffer", Module)]
    private static partial int CreateBufferNative(byte[] pcm16, int sampleRate, int channels);

    [JSImport("startBuffer", Module)]
    internal static partial int StartBuffer(int buffer, double volume, double rate, bool loop);

    /// <summary>Starts <paramref name="buffer"/> after <paramref name="offset"/> seconds, so a
    /// looping track can be started at the point it should already be at.</summary>
    [JSImport("startBufferAt", Module)]
    internal static partial int StartBufferAt(int buffer, double volume, double rate, bool loop, double offset);

    [JSImport("stopSource", Module)]
    internal static partial void StopSource(int source);

    [JSImport("setSourceVolume", Module)]
    internal static partial void SetSourceVolume(int source, double volume);

    [JSImport("setSourceRate", Module)]
    internal static partial void SetSourceRate(int source, double rate);

    [JSImport("setSourcePaused", Module)]
    internal static partial void SetSourcePaused(int source, bool paused);

    [JSImport("releaseBuffer", Module)]
    internal static partial void ReleaseBuffer(int buffer);

    [JSImport("stopAll", Module)]
    private static partial void StopAll();

    /// <summary>Applies a volume to every sound effect currently playing.</summary>
    public static void SetAllVolumes(float volume)
    {
        foreach (var clip in WebAudioSoundClip.Active)
            clip.ApplyVolume(volume);
    }

    /// <summary>Stops every sound effect currently playing.</summary>
    public static void StopAllSounds() => StopAll();
}
#endif

/// <summary>A short sound effect played through Web Audio. Replaces <c>FaudioSoundClip</c>.</summary>
public sealed class WebAudioSoundClip : ISoundClip
{
    /// <summary>Clips started and not yet stopped, for the bulk volume/stop operations.</summary>
    internal static readonly List<WebAudioSoundClip> Active = [];

    private readonly int _buffer;
    private double _volume;
    private int _source;

    public WebAudioSoundClip(string filePath, double volume = 1.0)
    {
        _volume = volume;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        using var stream = NFMWorldLibrary.TheVFS.VFS.OpenRead(filePath);
        using var decoded = AudioDecoder.Decode(stream, extension);

        _buffer = WebAudioBackend.CreateBuffer(
            decoded.PcmData.AsSpan(0, decoded.PcmData.Count), decoded.SampleRate, (int)decoded.Channels);
    }

    public void Play() => Start(loop: false);

    public void Loop() => Start(loop: true);

    public void Stop()
    {
        Active.Remove(this);
        if (_source == 0) return;
        WebAudioBackend.StopSource(_source);
        _source = 0;
    }

    internal void ApplyVolume(double volume)
    {
        _volume = volume;
        if (_source != 0) WebAudioBackend.SetSourceVolume(_source, volume);
    }

    private void Start(bool loop)
    {
        Stop();
        if (_buffer == 0) return;

        var source = WebAudioBackend.StartBuffer(_buffer, _volume, 1.0, loop);
        if (source != 0)
        {
            _source = source;
            if (!Active.Contains(this)) Active.Add(this);
        }
    }
}

/// <summary>A music track played through Web Audio. Replaces <c>FaudioMusic</c>.</summary>
public sealed class WebAudioMusic : IRadicalMusic
{
    private readonly int _buffer;

    private int _source;
    private float _volume = 0.8f;
    private double _rate = 1.0;
    private bool _paused;
    private bool _disposed;

    /// <summary>
    /// Decodes a track from a stream the caller already opened - the music archive is fetched
    /// asynchronously, so the bytes are in hand before this is constructed.
    /// </summary>
    public WebAudioMusic(Stream stream, string extension, double tempomul)
    {
        using var decoded = AudioDecoder.DecodeTrack(stream, extension);

        // The tempo change is applied by the source node's rate, which shifts pitch as well. The
        // desktop backend time-stretches instead, but that would decode-and-process a minute-long
        // track on the page's only thread; every shipped track uses 1.0.
        _rate = tempomul;

        _buffer = WebAudioBackend.CreateBuffer(
            decoded.PcmData.AsSpan(0, decoded.PcmData.Count), decoded.SampleRate, (int)decoded.Channels);
    }

    /// <summary>An unplayable instance, for a track that could not be loaded - every call is a
    /// no-op, matching <c>FaudioMusic</c>'s failure state.</summary>
    public WebAudioMusic() => _disposed = true;

    public void Play()
    {
        if (_disposed || _buffer == 0) return;

        StopSource();
        _source = WebAudioBackend.StartBuffer(_buffer, _volume, _rate, loop: true);
        _paused = false;
    }

    public void SetPaused(bool paused)
    {
        if (_disposed || _source == 0 || _paused == paused) return;

        WebAudioBackend.SetSourcePaused(_source, paused);
        _paused = paused;
    }

    public void SetVolume(float vol)
    {
        _volume = vol;
        if (_source != 0) WebAudioBackend.SetSourceVolume(_source, vol);
    }

    public float GetVolume() => _volume;

    public void SetFreqMultiplier(double multiplier)
    {
        _rate = multiplier;
        if (_source != 0) WebAudioBackend.SetSourceRate(_source, multiplier);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopSource();
        if (_buffer != 0) WebAudioBackend.ReleaseBuffer(_buffer);
    }

    private void StopSource()
    {
        if (_source == 0) return;
        WebAudioBackend.StopSource(_source);
        _source = 0;
    }
}
