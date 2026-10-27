using System.Buffers.Binary;
using System.Text;
using NAudio.Wave;
using Maxine.Extensions.Collections;

namespace NFMWorld.Audio;

/// <summary>
/// A plain RIFF/WAVE reader for the 16-bit and 8-bit PCM files the game ships.
///
/// The libsndfile-backed reader is unavailable on wasm, and every WAV in the data tree is PCM
/// (mono/stereo, 8 or 16 bit), so this covers the format without a native dependency. Anything it
/// does not recognise (compressed chunks, 24/32-bit, float) is refused rather than misread - the
/// decoder then reports the format as unsupported, which is how the game already treats a codec it
/// cannot play.
/// </summary>
public sealed class PcmWavReader : IWaveProvider
{
    private readonly Stream _stream;
    private readonly WaveFormat _format;

    private long _dataStart;
    private long _dataLength;
    private long _position;

    /// <summary>Set when the source is 8-bit: WAV's 8-bit samples are unsigned, NAudio's float
    /// conversion expects signed, so the whole block is shifted on the way out.</summary>
    private readonly bool _unsigned8Bit;

    public PcmWavReader(Stream stream)
    {
        _stream = stream;

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        ushort channels = 0, bitsPerSample = 0, formatTag = 0;
        var sampleRate = 0;

        // Chunks may appear in any order and several are irrelevant; walk them until both fmt and
        // data have been seen.
        while (true)
        {
            var id = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            var next = stream.Position + size + (size % 2);

            switch (id)
            {
                case "fmt ":
                    formatTag = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadUInt16(); // block align
                    bitsPerSample = reader.ReadUInt16();
                    break;

                case "data":
                    if (formatTag != 1)
                        throw new InvalidDataException($"Unsupported WAVE encoding {formatTag}; only PCM is handled.");
                    if (bitsPerSample != 8 && bitsPerSample != 16)
                        throw new InvalidDataException($"Unsupported WAVE sample size {bitsPerSample}; only 8 and 16 bit are handled.");

                    _dataStart = stream.Position;
                    _dataLength = size;
                    _format = new WaveFormat(sampleRate, bitsPerSample, channels);
                    _unsigned8Bit = bitsPerSample == 8;

                    // The stream stays where the data begins: reads serve the data chunk directly.
                    return;
            }

            if (next > stream.Length)
                throw new InvalidDataException("WAVE file has no data chunk.");
            stream.Position = next;
        }
    }

    public WaveFormat WaveFormat => _format;

    public int Read(Span<byte> buffer) => ReadCore(buffer);

    public int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));

    private int ReadCore(Span<byte> buffer)
    {
        var remaining = _dataLength - _position;
        if (remaining <= 0) return 0;

        var toRead = (int)Math.Min(remaining, buffer.Length);
        _stream.Position = _dataStart + _position;
        var read = _stream.Read(buffer[..toRead]);
        _position += read;

        if (_unsigned8Bit)
        {
            for (var i = 0; i < read; i++)
                buffer[i] = (byte)(buffer[i] - 128);
        }

        return read;
    }
}
