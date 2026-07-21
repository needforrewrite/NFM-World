using System.Buffers;
using System.IO.Compression;
using Collections.Pooled;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework.Audio;
using NAudio.Flac;
using NAudio.SoundFile;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NFMWorldLibrary;
using NLayer.NAudioSupport;

namespace NFMWorld.Audio;

/// <summary>
/// Decodes stream audio formats (WAV, MP3, OGG, FLAC, OPUS, AIFF) to 16-bit PCM
/// using NAudio v3. All decoding is offline (full file to memory).
/// </summary>
public static class AudioDecoder
{
    /// <summary>
    /// Decode audio data from a byte array, dispatching by file extension.
    /// Returns raw 16-bit signed PCM suitable for SoundEffect construction.
    /// </summary>
    public static DecodeResult Decode(Stream stream, string extension)
    {
        // Normalize extension: remove leading dot, lowercase
        var ext = extension.TrimStart('.').ToLowerInvariant();

        return ext switch
        {
            "wav" or "wave" => DecodeWav(stream),
            "mp3" or "mpga" => DecodeMp3(stream),
            "ogg" or "oga" => DecodeOgg(stream),
            "flac" => DecodeFlac(stream),
            "opus" => DecodeOpus(stream),
            "aiff" or "aif" or "aifc" => DecodeAiff(stream),
            _ => throw new NotSupportedException($"Unsupported audio format: .{ext}")
        };
    }

    /// <summary>
    /// Decode audio from a VFS path. Handles ZIP containers (*.zip, *.zipo, *.radq)
    /// by extracting the first entry.
    /// </summary>
    public static DecodeResult DecodeFromVfs(string vfsPath)
    {
        var extension = Path.GetExtension(vfsPath).ToLowerInvariant();

        using var stream = VFS.OpenRead(vfsPath);

        // Handle ZIP-based containers
        if (extension is ".zip" or ".zipo" or ".radq")
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault()
                        ?? throw new InvalidDataException($"ZIP container is empty: {vfsPath}");
            var innerExt = Path.GetExtension(entry.Name);
            using var entryStream = entry.Open();
            using var entryMs = new MemoryStream();
            entryStream.CopyTo(entryMs);
            return Decode(entry.Open(), innerExt);
        }

        return Decode(stream, extension);
    }

    private static DecodeResult DecodeWav(Stream stream)
    {
        using var reader = new WaveFileReader(stream);
        return ReadToPcm16(reader);
    }

    private static DecodeResult DecodeMp3(Stream stream)
    {
        using var reader = new Mp3FileReaderBase(stream, wf => new Mp3FrameDecompressor(wf));
        return ReadToPcm16(reader);
    }

    private static DecodeResult DecodeOgg(Stream stream)
    {
        using var reader = new SoundFileReader(stream);
        return ReadToPcm16(reader);
    }

    private static DecodeResult DecodeFlac(Stream stream)
    {
        using var reader = new FlacReader(stream);
        return ReadToPcm16(reader);
    }

    private static DecodeResult DecodeOpus(Stream stream)
    {
        using var reader = new SoundFileReader(stream);
        return ReadToPcm16(reader);
    }

    private static DecodeResult DecodeAiff(Stream stream)
    {
        using var reader = new AiffFileReader(stream);
        return ReadToPcm16(reader);
    }

    /// <summary>
    /// Reads all samples from an IWaveProvider and converts to 16-bit PCM.
    /// </summary>
    private static DecodeResult ReadToPcm16(IWaveProvider waveProvider)
    {
        var waveFormat = waveProvider.WaveFormat;

        // Convert to ISampleProvider (float samples) for uniform processing
        var sampleProvider = waveProvider.ToSampleProvider();

        // Read all float samples
        using var allFloats = new PooledList<float>();
        using (var floatBuffer = SafeArrayPool<float>.Shared.Rent(4096))
        {
            int samplesRead;
            while ((samplesRead = sampleProvider.Read(floatBuffer)) > 0)
            {
                allFloats.AddRange(floatBuffer.AsSpan(0, samplesRead));
            }
        }

        // Convert float [-1.0, 1.0] to 16-bit PCM
        var floatSamples = allFloats.ToArray();
        var pcmData = SafeArrayPool<byte>.Shared.Rent(floatSamples.Length * 2);
        try
        {
            for (int i = 0; i < floatSamples.Length; i++)
            {
                // Clamp and convert to int16
                var sample = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
                var int16Sample = (short)(sample * short.MaxValue);
                pcmData[i * 2] = (byte)(int16Sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((int16Sample >> 8) & 0xFF);
            }

            var channels = waveFormat.Channels switch
            {
                1 => AudioChannels.Mono,
                2 => AudioChannels.Stereo,
                _ => throw new NotSupportedException($"Unsupported channel count: {waveFormat.Channels}")
            };

            return new DecodeResult(pcmData, waveFormat.SampleRate, channels, true);
        }
        catch
        {
            pcmData.Dispose();
            throw;
        }
    }
}
