using System.Buffers;
using Collections.Pooled;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using Microsoft.IO;
using NAudio.Wave;
using SoundTouch;
using SoundTouch.Net.NAudioSupport;

namespace NFMWorld.Audio;

/// <summary>
/// Offline audio time-stretching using SoundTouch.
/// Changes tempo without affecting pitch.
/// </summary>
public static class TempoStretcher
{
    /// <summary>
    /// Process 16-bit PCM data through SoundTouch to change tempo.
    /// </summary>
    /// <param name="pcmData">Input 16-bit stereo or mono PCM samples.</param>
    /// <param name="sampleRate">Sample rate in Hz (e.g., 44100).</param>
    /// <param name="channels">1 for mono, 2 for stereo.</param>
    /// <param name="tempoRatio">Tempo multiplier. 1.0 = normal, >1 = faster, &lt;1 = slower.</param>
    /// <returns>Pooled array containing time-stretched 16-bit PCM data.</returns>
    public static DisposableArraySegment<byte> Process(Memory<byte> pcmData, int sampleRate, int channels, double tempoRatio)
    {
        // Configure SoundTouch
        var processor = new SoundTouchProcessor
        {
            SampleRate = sampleRate,
            Channels = channels,
            Tempo = tempoRatio
        };

        using var inputFile = new RawSourceWaveStream(new MemoryStream2(pcmData), new WaveFormat(sampleRate, 16, channels));
        using var inputStream = new WaveChannel32(inputFile) { PadWithZeroes = false };
        using var processStream = new SoundTouchWaveStream(inputStream, processor);
        using var outputStream = new Wave32To16Stream(processStream);
        using var outputMemory = new RecyclableMemoryStream(MemoryManager.Manager, Guid.NewGuid(), "TempoStretcher Stream");
        
        outputStream.CopyTo(outputMemory);
        var resultPool = SafeArrayPool<byte>.Shared.Rent((int)outputMemory.Length);
        outputMemory.GetReadOnlySequence().CopyTo(resultPool);
        return resultPool;
    }
}
