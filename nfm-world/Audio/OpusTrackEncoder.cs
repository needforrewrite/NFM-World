using System.Buffers.Binary;
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;

namespace NFMWorld.Audio;

/// <summary>Encodes decoded interleaved PCM as an Ogg Opus music track.</summary>
public static class OpusTrackEncoder
{
    private const int OpusSampleRate = 48000;

    /// <summary>
    /// Encodes a track returned by <see cref="AudioDecoder.DecodeTrack"/>. The decoded buffer remains
    /// owned by the caller. Bitrate is in bits per second; resampling is performed when necessary.
    /// </summary>
    public static byte[] Encode(DecodeResult decoded, int bitrate = 96000)
    {
        int channels = (int)decoded.Channels;

        if (channels is not (1 or 2) || decoded.SampleRate <= 0)

            throw new ArgumentException("Unsupported PCM format.", nameof(decoded));
        var pcm = decoded.PcmData.AsSpan(0, decoded.PcmData.Count);

        if (pcm.Length == 0 || pcm.Length % (channels * sizeof(short)) != 0)

            throw new ArgumentException("PCM must contain complete, non-empty 16-bit sample frames.", nameof(decoded));

        // Instantiate the managed encoder directly: the factory may probe a native library, which
        // is unnecessary on desktop and unavailable in the browser.
#pragma warning disable CS0618
        using var encoder = new OpusEncoder(OpusSampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO);
#pragma warning restore CS0618
        encoder.Bitrate = bitrate;
        using var output = new MemoryStream();
        var writer = new OpusOggWriteStream(encoder, output, inputSampleRate: decoded.SampleRate, leaveOpen: true)
        {
            MaxAudioLengthPerPage = TimeSpan.FromMilliseconds(20)
        };

        var samples = new short[4096 * channels];

        while (!pcm.IsEmpty)
        {
            int count = Math.Min(samples.Length, pcm.Length / sizeof(short));
            for (int i = 0; i < count; i++)
                samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * sizeof(short), sizeof(short)));
            writer.WriteSamples(samples, 0, count);
            pcm = pcm[(count * sizeof(short))..];
        }

        // Flush the encoder and resampler. The Ogg writer pads the last frame but does not record
        // the encoder delay or trim padding; correct those fields in the finished pages below.
        Array.Clear(samples);
        long silenceFrames = (long)decoded.SampleRate / 10 + 1;

        while (silenceFrames > 0)
        {
            int frames = (int)Math.Min(silenceFrames, samples.Length / channels);
            writer.WriteSamples(samples, 0, frames * channels);
            silenceFrames -= frames;
        }

        writer.Finish();

        long inputFrames = decoded.PcmData.Count / (channels * sizeof(short));
        long outputFrames = (inputFrames * OpusSampleRate + decoded.SampleRate / 2) / decoded.SampleRate;

        return TrimOgg(output, encoder.Lookahead, outputFrames);
    }

    private static byte[] TrimOgg(MemoryStream output, int preSkip, long outputFrames)
    {
        byte[] data = output.GetBuffer();
        int length = checked((int)output.Length);
        int offset = 0;
        int pageIndex = 0;
        long finalGranule = checked(outputFrames + preSkip);

        while (offset < length)
        {
            int segments = data[offset + 26];
            int headerLength = 27 + segments;
            int payloadLength = 0;
            for (int i = 0; i < segments; i++)
                payloadLength += data[offset + 27 + i];
            int pageLength = headerLength + payloadLength;
            var page = data.AsSpan(offset, pageLength);

            if (pageIndex == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(page.Slice(headerLength + 10, 2), checked((ushort)preSkip));
                UpdateChecksum(page);
            }
            else if (pageIndex >= 2 && BinaryPrimitives.ReadInt64LittleEndian(page.Slice(6, 8)) >= finalGranule)
            {
                // This page contains the last sample to keep. Discard later padding pages, mark
                // this one EOS, and set its exact end granule (always measured at 48 kHz).
                page[5] |= 4;
                BinaryPrimitives.WriteInt64LittleEndian(page.Slice(6, 8), finalGranule);
                UpdateChecksum(page);

                return data.AsSpan(0, offset + pageLength).ToArray();
            }

            offset += pageLength;
            pageIndex++;
        }

        throw new InvalidDataException("Opus writer did not produce enough audio to finish the track.");
    }

    private static void UpdateChecksum(Span<byte> page)
    {
        page.Slice(22, 4).Clear();
        uint crc = 0;

        foreach (byte value in page)
        {
            crc ^= (uint)value << 24;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04c11db7 : crc << 1;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(page.Slice(22, 4), crc);
    }
}
