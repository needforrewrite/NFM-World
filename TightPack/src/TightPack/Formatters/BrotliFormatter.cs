using System.IO.Compression;

namespace TightPack.Formatters;

/// <summary>
/// Compresses a <see cref="byte"/> payload with Brotli. Writes the uncompressed length, then the
/// compressed payload. The reader refuses a declared length above the configured limit, so a corrupt
/// header cannot ask for a large allocation.
/// </summary>
public sealed class BrotliFormatter : TightPackFormatter<byte[]>
{
    /// <summary>128 MB, matching the cap MemoryPack uses for the same formatter.</summary>
    public const int DefaultDecompressionSizeLimit = 128 * 1024 * 1024;

    public static readonly BrotliFormatter Default = new();

    private readonly CompressionLevel _compressionLevel;
    private readonly int _decompressionSizeLimit;

    public BrotliFormatter() : this(CompressionLevel.Fastest)
    {
    }

    public BrotliFormatter(CompressionLevel compressionLevel)
        : this(compressionLevel, DefaultDecompressionSizeLimit)
    {
    }

    public BrotliFormatter(CompressionLevel compressionLevel, int decompressionSizeLimit)
    {
        _compressionLevel = compressionLevel;
        _decompressionSizeLimit = decompressionSizeLimit;
    }

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in byte[]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        byte[] compressed = Compress(value, _compressionLevel);
        writer.WriteInt32(value.Length);
        writer.WriteUtf8(compressed);
    }

    public override void Deserialize(ref TightPackReader reader, ref byte[]? value)
    {
        // ReadInt32 rather than TryReadCollectionHeader: the declared length counts the *decompressed*
        // bytes, which is normally far more than remains in the buffer, so the header's own
        // "length <= remaining" guard does not apply.
        int uncompressedLength = reader.ReadInt32();
        if (uncompressedLength < 0)
        {
            value = null;
            return;
        }

        byte[]? compressed = reader.ReadByteArray();
        if (compressed is null)
        {
            value = null;
            return;
        }

        if (uncompressedLength > _decompressionSizeLimit)
        {
            TightPackSerializationException.ThrowMessage(
                $"Compressed payload declares {uncompressedLength} bytes, over the {_decompressionSizeLimit}-byte limit."
            );
        }

        if (uncompressedLength == 0)
        {
            value = [];
            return;
        }

        var result = new byte[uncompressedLength];
        Inflate(compressed, result, uncompressedLength);
        value = result;
    }

    internal static byte[] Compress(byte[] value, CompressionLevel level)
    {
        using var stream = new MemoryStream();
        using (var brotli = new BrotliStream(stream, level, leaveOpen: true))
        {
            brotli.Write(value);
        }

        return stream.ToArray();
    }

    /// <summary>Decompresses into an exactly sized buffer, so the output cannot exceed it.</summary>
    internal static void Inflate(byte[] compressed, byte[] destination, int expectedLength)
    {
        using var stream = new BrotliStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
        int read = 0;
        while (read < expectedLength)
        {
            int got = stream.Read(destination, read, expectedLength - read);
            if (got == 0)
            {
                TightPackSerializationException.ThrowMessage(
                    $"Compressed payload ended after {read} of {expectedLength} bytes."
                );
            }

            read += got;
        }
    }
}
