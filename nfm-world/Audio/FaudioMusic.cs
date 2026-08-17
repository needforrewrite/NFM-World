using System.Buffers;
using System.IO.Compression;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sentry;
using NFMWorldLibrary;

namespace NFMWorld.Audio;

/// <summary>
/// Implements <see cref="IRadicalMusic"/> using FNA's SoundEffect/SoundEffectInstance
/// for output, with NAudio and LibOpenMPT.NET for decoding.
/// Replaces the ManagedBass-based RadicalMusic.
/// </summary>
public sealed class FaudioMusic : IRadicalMusic
{
    private SoundEffect? _effect;
    private SoundEffectInstance? _instance;
    private bool _readable;

    // Stored for re-stretching if SetFreqMultiplier is called
    private ArraySegment<byte> OriginalPcm => _decoded.PcmData;
    private int SampleRate => _decoded.SampleRate;
    private int Channels => (int)_decoded.Channels;
    private double _currentTempoMultiplier = 1.0;
    private readonly DecodeResult _decoded;

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

            _currentTempoMultiplier = tempomul;

            DisposableArraySegment<byte> arrayToReturnToPool = default;

            try
            {
                // Apply tempo stretching if needed
                var finalPcm = Math.Abs(tempomul - 1.0) > 0.01
                    ? arrayToReturnToPool =
                        TempoStretcher.Process(result.PcmData, result.SampleRate, Channels, tempomul)
                    : result.PcmData;

                // Create SoundEffect with loop points (loop entire track)
                var totalSamples = finalPcm.Count / 2; // 16-bit = 2 bytes per sample
                var totalFrames = totalSamples / Channels; // samples per channel
                _effect = new SoundEffect(finalPcm, result.SampleRate, result.Channels, 0, totalFrames);
            }
            finally
            {
                arrayToReturnToPool.Dispose();
            }

            _readable = true;
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            Logging.Error($"Failed to load music '{file}': {e}");
            _readable = false;
        }
        finally
        {
            archive?.Dispose();
            entryStream?.Dispose();
        }
    }

    public void SetPaused(bool p0)
    {
        if (!_readable) return;

        if (p0)
            _instance?.Pause();
        else
            _instance?.Resume();
    }

    public void Dispose()
    {
        if (!_readable) return;

        _instance?.Stop();
        _instance?.Dispose();
        _instance = null;

        _effect?.Dispose();
        _effect = null;

        _readable = false;

        _decoded.Dispose();
    }

    public void Play()
    {
        if (!_readable || _effect == null) return;

        // Create a new instance each time Play is called
        // (SoundEffectInstance.Stop(true) destroys the FAudio voice)
        _instance?.Dispose();
        _instance = _effect.CreateInstance();
        _instance.IsLooped = true;
        _instance.Volume = IRadicalMusic.CurrentVolume;
        _instance.Play();
    }

    public void SetVolume(float vol)
    {
        IRadicalMusic.CurrentVolume = vol;

        if (!_readable) return;

        _instance?.Volume = vol;
    }

    public float GetVolume()
    {
        if (!_readable || _instance == null)
            return 0f;

        return _instance.Volume;
    }


    public void SetFreqMultiplier(double multiplier)
    {
        if (!_readable) return;

        multiplier = Math.Clamp(multiplier, 0.50, 2.0);

        // Apply directly to the active instance (if any).
        // New instances pick it up in Play().
        _instance?.Pitch = MathF.Log2((float)multiplier);
    }
}
