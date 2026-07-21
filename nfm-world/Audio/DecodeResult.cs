using System.Buffers;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework.Audio;

namespace NFMWorld.Audio;

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