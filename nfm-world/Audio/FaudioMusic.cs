using System.IO.Compression;
using System.Runtime.InteropServices;
using Maxine.Extensions.Collections;
using NFMWorld.Audio.FAudioBindings;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sentry;
using NFMWorldLibrary;

namespace NFMWorld.Audio;

/// <summary>
/// Implements <see cref="IRadicalMusic"/> by driving an FAudio source voice directly, with
/// NAudio and LibOpenMPT.NET for decoding.
/// <para>
/// This replaces the FNA <c>SoundEffect</c>/<c>SoundEffectInstance</c>-based implementation:
/// playback, volume, pause and pitch are all FAudio calls now, and decoded PCM is submitted
/// straight to the source voice as an <c>FAudioBuffer</c> with no <c>SoundEffect</c> in between.
/// </para>
/// </summary>
public sealed class FaudioMusic : IRadicalMusic
{
    private readonly DecodeResult _decoded;

    /// <summary>
    /// Whether this instance holds decodable audio. Cleared by <see cref="Dispose"/> so that a
    /// second dispose is a no-op - <see cref="_decoded"/>'s pooled buffer is returned to the pool
    /// by <see cref="DecodeResult.Dispose"/>, and returning it twice would corrupt the pool.
    /// </summary>
    private bool _readable;

    /// <summary>
    /// Tempo-stretched PCM, produced at load time when the requested tempo multiplier is not 1.0.
    /// Held separately from <see cref="_decoded"/> because stretching returns a pooled buffer that
    /// this class owns. When empty, <see cref="_decoded"/>'s own PCM is played instead.
    /// </summary>
    private DisposableArraySegment<byte> _stretchedPcm;
    private readonly bool _hasStretchedPcm;

    private IntPtr _voice;

    /// <summary>
    /// Pins the PCM array FAudio reads from. The buffer stays queued on the voice for as long as
    /// it plays, so the array must not move or be freed until the voice is destroyed.
    /// </summary>
    private GCHandle _pcmHandle;

    /// <summary>
    /// Playback rate multiplier. Distinct from the constructor's tempo multiplier: that one is
    /// applied by time-stretching the PCM at load (preserving pitch), whereas this one is a
    /// resampling rate applied to the live voice (which shifts pitch with it).
    /// </summary>
    private double _frequencyRatio = 1.0;

    /// <summary>
    /// Creates an empty, unplayable music instance. All methods are no-ops.
    /// </summary>
    public FaudioMusic()
    {
        _readable = false;
    }

    /// <summary>
    /// Loads and decodes a music track from a VFS path.
    /// </summary>
    /// <param name="file">VFS path to the audio file.</param>
    /// <param name="tempomul">Initial tempo multiplier (1.0 = normal speed).</param>
    public FaudioMusic(string file, double tempomul)
    {
        ZipArchive? archive = null;
        Stream? entryStream = null;

        try
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();

            // Read file through VFS
            using var stream = VFS.OpenRead(file);
            // Handle ZIP-based containers
            if (extension is ".zip" or ".zipo" or ".radq")
            {
                archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault()
                            ?? throw new InvalidDataException($"ZIP container is empty: {file}");
                extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                entryStream = entry.Open();
            }

            // Decode to PCM
            DecodeResult result;

            if (TrackerDecoder.IsTrackerFormat(extension))
            {
                result = TrackerDecoder.Decode(entryStream ?? stream);
            }
            else
            {
                result = AudioDecoder.Decode(entryStream ?? stream, extension);
            }

            _decoded = result;

            // Time-stretch at load rather than resampling on the voice: the requested tempo
            // multiplier is a tempo change, and playing it back fast would transpose the music.
            if (Math.Abs(tempomul - 1.0) > 0.01)
            {
                _stretchedPcm = TempoStretcher.Process(result.PcmData, result.SampleRate, (int)result.Channels, tempomul);
                _hasStretchedPcm = true;
            }

            _readable = true;
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Logging.Error($"Failed to load music '{file}': {e}");
            _readable = false;

            // Dispose() short-circuits on !_readable, so a buffer stretched before the failure
            // has to be returned to the pool here or it leaks.
            if (_hasStretchedPcm)
            {
                _stretchedPcm.Dispose();
                _stretchedPcm = default;
            }
        }
        finally
        {
            archive?.Dispose();
            entryStream?.Dispose();
        }
    }

    public void SetPaused(bool p0)
    {
        if (!_readable || _voice == IntPtr.Zero) return;

        if (p0)
            FAudio.FAudioSourceVoice_Stop(_voice, 0, 0);
        else
            FAudio.FAudioSourceVoice_Start(_voice, 0, 0);
    }

    public void Dispose()
    {
        if (!_readable) return;
        _readable = false;

        DestroyVoice();

        if (_hasStretchedPcm)
        {
            _stretchedPcm.Dispose();
            _stretchedPcm = default;
        }

        _decoded.Dispose();
    }

    /// <summary>The PCM actually played: the stretched copy when one was produced, else the decoded original.</summary>
    private DisposableArraySegment<byte> PcmToPlay =>
        _hasStretchedPcm ? _stretchedPcm : _decoded.PcmData;

    public void Play()
    {
        if (!_readable) return;

        // A voice is single-use once its buffers are flushed, and the pitch/volume state from a
        // previous run must not leak into this one, so start from a fresh voice each Play -
        // mirroring what the old SoundEffectInstance-per-Play did.
        DestroyVoice();

        var engine = FaudioEngine.Instance;
        if (engine is null)
            return; // no audio device - play silently rather than failing

        _voice = engine.CreateSourceVoice(_decoded.SampleRate, (int)_decoded.Channels);
        if (_voice == IntPtr.Zero)
            return;

        if (!TrySubmitBuffer())
        {
            DestroyVoice();
            return;
        }

        FAudio.FAudioVoice_SetVolume(_voice, IRadicalMusic.CurrentVolume, 0);
        ApplyFreqMultiplier();

        FAudio.FAudioSourceVoice_Start(_voice, 0, 0);
    }

    /// <summary>
    /// Pins the decoded PCM and queues it on the current voice as an infinitely looping buffer.
    /// </summary>
    private bool TrySubmitBuffer()
    {
        var pcm = PcmToPlay;
        if (pcm.Array is not { } array || pcm.Count == 0)
            return false;

        _pcmHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

        var buffer = new FAudio.FAudioBuffer
        {
            Flags = 0,
            AudioBytes = (uint)pcm.Count,
            pAudioData = _pcmHandle.AddrOfPinnedObject() + pcm.Offset,
            PlayBegin = 0,
            PlayLength = 0, // 0 = to the end of the buffer
            LoopBegin = 0,
            LoopLength = 0, // 0 = to the end of the buffer
            LoopCount = FAudio.FAUDIO_LOOP_INFINITE,
            pContext = IntPtr.Zero,
        };

        return FAudio.FAudioSourceVoice_SubmitSourceBuffer(_voice, ref buffer, IntPtr.Zero) == 0;
    }

    private void DestroyVoice()
    {
        if (_voice != IntPtr.Zero)
        {
            FAudio.FAudioSourceVoice_Stop(_voice, 0, 0);
            // Destroying the voice is what makes FAudio stop reading pAudioData, so the pin can
            // only be released after this call.
            FaudioEngine.Instance?.DestroyVoice(_voice);
            _voice = IntPtr.Zero;
        }

        if (_pcmHandle.IsAllocated)
            _pcmHandle.Free();
    }

    public void SetVolume(float vol)
    {
        IRadicalMusic.CurrentVolume = vol;

        if (_voice == IntPtr.Zero) return;

        FAudio.FAudioVoice_SetVolume(_voice, vol, 0);
    }

    public float GetVolume()
    {
        if (_voice == IntPtr.Zero)
            return 0f;

        FAudio.FAudioVoice_GetVolume(_voice, out var volume);
        return volume;
    }

    /// <summary>
    /// Sets the playback rate multiplier. FAudio's frequency ratio is already linear, so the
    /// log2/semitone conversion the old XNA <c>Pitch</c>-based path needed is gone.
    /// </summary>
    public void SetFreqMultiplier(double multiplier)
    {
        _frequencyRatio = Math.Clamp(multiplier, 0.50, 2.0);

        // Apply to the live voice (if any); a later Play() picks it up from _frequencyRatio.
        if (_voice != IntPtr.Zero)
            ApplyFreqMultiplier();
    }

    private void ApplyFreqMultiplier() =>
        FAudio.FAudioSourceVoice_SetFrequencyRatio(_voice, (float)_frequencyRatio, 0);
}
