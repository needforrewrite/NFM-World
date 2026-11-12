using System.Buffers;

namespace TightPack.Formatters;

/// <summary>Writes a <see cref="ReadOnlyMemory{T}"/> as a length-prefixed sequence of elements.</summary>
public sealed class ReadOnlyMemoryFormatter<T> : TightPackFormatter<ReadOnlyMemory<T>>
{
    public static readonly ReadOnlyMemoryFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyMemory<T> value
    )
    {
        ReadOnlySpan<T> span = value.Span;
        writer.WriteCollectionHeader(span.Length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < span.Length; i++)
        {
            T? item = span[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyMemory<T> value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        if (length == 0)
        {
            value = ReadOnlyMemory<T>.Empty;
            return;
        }

        var items = new T[length];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            items[i] = item!;
        }

        value = items;
    }
}

/// <summary>Writes a <see cref="Memory{T}"/> as a length-prefixed sequence of elements.</summary>
public sealed class MemoryFormatter<T> : TightPackFormatter<Memory<T>>
{
    public static readonly MemoryFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Memory<T> value)
    {
        Span<T> span = value.Span;
        writer.WriteCollectionHeader(span.Length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < span.Length; i++)
        {
            T? item = span[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref Memory<T> value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        if (length == 0)
        {
            value = Memory<T>.Empty;
            return;
        }

        var items = new T[length];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            items[i] = item!;
        }

        value = items;
    }
}

/// <summary>
/// Byte payloads are the common case, and the element-wise loop above costs a formatter call per
/// byte on a packet body. This copies the whole span in one go instead.
/// </summary>
public sealed class ReadOnlyMemoryByteFormatter : TightPackFormatter<ReadOnlyMemory<byte>>
{
    public static readonly ReadOnlyMemoryByteFormatter Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyMemory<byte> value
    )
    {
        writer.WriteCollectionHeader(value.Length);
        writer.WriteBytes(value.Span);
    }

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyMemory<byte> value) =>
        value = reader.ReadByteArray();
}

public sealed class MemoryByteFormatter : TightPackFormatter<Memory<byte>>
{
    public static readonly MemoryByteFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Memory<byte> value)
    {
        writer.WriteCollectionHeader(value.Length);
        writer.WriteBytes(value.Span);
    }

    public override void Deserialize(ref TightPackReader reader, ref Memory<byte> value) =>
        value = reader.ReadByteArray();
}

/// <summary>
/// Reads a byte payload into an array rented from <see cref="ArrayPool{T}.Shared"/>, so a
/// high-rate packet loop does not allocate per message. The caller owns the buffer and must
/// return it; the array is longer than the payload when the pool could not give an exact fit.
/// </summary>
public sealed class PooledReadOnlyMemoryByteFormatter : TightPackFormatter<ReadOnlyMemory<byte>>
{
    public static readonly PooledReadOnlyMemoryByteFormatter Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyMemory<byte> value
    )
    {
        writer.WriteCollectionHeader(value.Length);
        writer.WriteBytes(value.Span);
    }

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyMemory<byte> value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        if (length == 0)
        {
            value = ReadOnlyMemory<byte>.Empty;
            return;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        reader.ReadBytes(buffer.AsSpan(0, length));
        value = new ReadOnlyMemory<byte>(buffer, 0, length);
    }
}
