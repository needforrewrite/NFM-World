using System.Buffers;
using Maxine.Extensions.Collections;

namespace NFMWorld.Audio;

/// <summary>
/// Channel layout of decoded PCM - the game's own replacement for XNA's
/// <c>Microsoft.Xna.Framework.Audio.AudioChannels</c> (Milestone 7). Values match XNA's (mono 1,
/// stereo 2) because consumers cast straight to <see cref="int"/> for FAudio and the tempo
/// stretcher, so the numbers are part of the contract.
/// </summary>
public enum AudioChannels
{
    Mono = 1,
    Stereo = 2,
}

/// <summary>
/// Result of decoding a tracker module.
/// </summary>
public struct DecodeResult(DisposableArraySegment<byte> pcmData, int sampleRate, AudioChannels channels, bool pooled) : IDisposable
{
    public DisposableArraySegment<byte> PcmData = pcmData;
    public readonly int SampleRate = sampleRate;
    public readonly AudioChannels Channels = channels;

    public void Dispose()
    {
        if (pooled && PcmData.Array is {} arr)
        {
            PcmData.Dispose();
        }
    }
}