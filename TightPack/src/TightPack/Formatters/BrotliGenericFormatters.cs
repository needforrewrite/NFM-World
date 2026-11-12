using System.IO.Compression;
using System.Text;

namespace TightPack.Formatters;

/// <summary>
/// Compresses the serialized form of <typeparamref name="T"/> with Brotli. The inner payload is an
/// ordinary TightPack encoding, so a member type keeps its own formatter and this wrapper only
/// changes how the bytes are carried.
/// </summary>
public sealed class BrotliFormatter<T> : TightPackFormatter<T>
{
    public static readonly BrotliFormatter<T> Default = new();

    private readonly CompressionLevel _compressionLevel;

    public BrotliFormatter() : this(CompressionLevel.Fastest)
    {
    }

    public BrotliFormatter(CompressionLevel compressionLevel) => _compressionLevel = compressionLevel;

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value)
    {
        var inner = TightPackBufferWriter.Rent();
        try
        {
            var innerWriter = new TightPackWriter<TightPackBufferWriter>(ref inner);
            TightPackFormatterProvider.GetFormatter<T>().Serialize(ref innerWriter, in value);
            innerWriter.Flush();

            byte[] compressed = BrotliFormatter.Compress(inner.WrittenSpan.ToArray(), _compressionLevel);
            writer.WriteInt32(inner.WrittenCount);
            writer.WriteUtf8(compressed);
        }
        finally
        {
            TightPackBufferWriter.Return(inner);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref T? value)
    {
        int uncompressedLength = reader.ReadInt32();
        if (uncompressedLength < 0)
        {
            value = default;
            return;
        }

        byte[]? compressed = reader.ReadByteArray();
        if (compressed is null)
        {
            value = default;
            return;
        }

        if (uncompressedLength > BrotliFormatter.DefaultDecompressionSizeLimit)
        {
            TightPackSerializationException.ThrowMessage(
                $"Compressed payload declares {uncompressedLength} bytes, over the " +
                $"{BrotliFormatter.DefaultDecompressionSizeLimit}-byte limit."
            );
        }

        var buffer = new byte[uncompressedLength];
        BrotliFormatter.Inflate(compressed, buffer, uncompressedLength);

        var innerReader = new TightPackReader(buffer);
        TightPackFormatterProvider.GetFormatter<T>().Deserialize(ref innerReader, ref value);
    }
}

/// <summary>Compresses a string as UTF-8. Intended for a long, repetitive payload.</summary>
public sealed class BrotliStringFormatter : TightPackFormatter<string>
{
    /// <summary>128 MB, matching <see cref="BrotliFormatter"/>.</summary>
    public const int DefaultDecompressionSizeLimit = BrotliFormatter.DefaultDecompressionSizeLimit;

    public static readonly BrotliStringFormatter Default = new();

    private readonly CompressionLevel _compressionLevel;
    private readonly int _decompressionSizeLimit;

    public BrotliStringFormatter() : this(CompressionLevel.Fastest)
    {
    }

    public BrotliStringFormatter(CompressionLevel compressionLevel)
        : this(compressionLevel, DefaultDecompressionSizeLimit)
    {
    }

    public BrotliStringFormatter(CompressionLevel compressionLevel, int decompressionSizeLimit)
    {
        _compressionLevel = compressionLevel;
        _decompressionSizeLimit = decompressionSizeLimit;
    }

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in string? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        byte[] compressed = BrotliFormatter.Compress(utf8, _compressionLevel);
        writer.WriteInt32(utf8.Length);
        writer.WriteUtf8(compressed);
    }

    public override void Deserialize(ref TightPackReader reader, ref string? value)
    {
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
                $"Compressed string declares {uncompressedLength} bytes, over the {_decompressionSizeLimit}-byte limit."
            );
        }

        if (uncompressedLength == 0)
        {
            value = string.Empty;
            return;
        }

        var buffer = new byte[uncompressedLength];
        BrotliFormatter.Inflate(compressed, buffer, uncompressedLength);
        value = Encoding.UTF8.GetString(buffer);
    }
}
