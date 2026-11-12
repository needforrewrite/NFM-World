using System.Buffers;

namespace TightPack;

/// <summary>
/// Entry points for the TightPack wire format.
/// </summary>
/// <remarks>
/// The generic overloads constrain on <see cref="ITightPackable{TSelf}"/> so the common case is a
/// direct static call: no formatter lookup and no virtual dispatch. Use the
/// <see cref="TightPackFormatter{T}"/> overloads for types that are not <c>[TightPackable]</c>.
/// </remarks>
public static class TightPackSerializer
{
    /// <summary>Serializes <paramref name="value"/> into a new array.</summary>
    public static byte[] Serialize<T>(in T value)
        where T : ITightPackable<T>
    {
        var buffer = TightPackBufferWriter.Rent();
        try
        {
            var writer = new TightPackWriter<TightPackBufferWriter>(ref buffer);
            T.Serialize(ref writer, in value);
            writer.Flush();
            return buffer.ToArray();
        }
        finally
        {
            TightPackBufferWriter.Return(buffer);
        }
    }

    /// <summary>Serializes <paramref name="value"/> into a new array using an explicit formatter.</summary>
    public static byte[] Serialize<T>(in T? value, TightPackFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var buffer = TightPackBufferWriter.Rent();
        try
        {
            var writer = new TightPackWriter<TightPackBufferWriter>(ref buffer);
            formatter.Serialize(ref writer, in value);
            writer.Flush();
            return buffer.ToArray();
        }
        finally
        {
            TightPackBufferWriter.Return(buffer);
        }
    }

    /// <summary>Serializes <paramref name="value"/> into <paramref name="bufferWriter"/>.</summary>
    public static void Serialize<T, TBufferWriter>(ref TBufferWriter bufferWriter, in T value)
        where T : ITightPackable<T>
        where TBufferWriter : IBufferWriter<byte>
    {
        var writer = new TightPackWriter<TBufferWriter>(ref bufferWriter);
        T.Serialize(ref writer, in value);
        writer.Flush();
    }

    /// <summary>Serializes <paramref name="value"/> into <paramref name="bufferWriter"/>.</summary>
    public static void Serialize<T, TBufferWriter>(ref TBufferWriter bufferWriter, in T? value, TightPackFormatter<T> formatter)
        where TBufferWriter : IBufferWriter<byte>
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var writer = new TightPackWriter<TBufferWriter>(ref bufferWriter);
        formatter.Serialize(ref writer, in value);
        writer.Flush();
    }

    /// <summary>Deserializes a value from <paramref name="buffer"/>.</summary>
    public static T Deserialize<T>(ReadOnlySpan<byte> buffer)
        where T : ITightPackable<T>
    {
        T value = default!;
        var reader = new TightPackReader(buffer);
        T.Deserialize(ref reader, ref value);
        return value;
    }

    /// <summary>Deserializes into an existing value, returning the bytes consumed.</summary>
    public static int Deserialize<T>(ReadOnlySpan<byte> buffer, ref T? value)
        where T : ITightPackable<T>
    {
        var reader = new TightPackReader(buffer);
        T.Deserialize(ref reader, ref value!);
        return reader.Consumed;
    }

    public static T? Deserialize<T>(ReadOnlySpan<byte> buffer, TightPackFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        T? value = default;
        var reader = new TightPackReader(buffer);
        formatter.Deserialize(ref reader, ref value);
        return value;
    }

    public static int Deserialize<T>(ReadOnlySpan<byte> buffer, ref T? value, TightPackFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var reader = new TightPackReader(buffer);
        formatter.Deserialize(ref reader, ref value);
        return reader.Consumed;
    }

    /// <summary>
    /// Deserializes from a possibly segmented sequence. A multi-segment sequence is copied once into
    /// a pooled buffer, which keeps the reader on a single span.
    /// </summary>
    public static T Deserialize<T>(in ReadOnlySequence<byte> buffer)
        where T : ITightPackable<T>
    {
        if (buffer.IsSingleSegment)
        {
            return Deserialize<T>(buffer.FirstSpan);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent((int)buffer.Length);
        try
        {
            buffer.CopyTo(rented);
            return Deserialize<T>(rented.AsSpan(0, (int)buffer.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Serializes <paramref name="value"/> and writes the bytes to <paramref name="stream"/>.</summary>
    public static void Serialize<T>(Stream stream, in T value)
        where T : ITightPackable<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] bytes = Serialize(in value);
        stream.Write(bytes);
    }

    /// <summary>Reads every byte of <paramref name="stream"/> and deserializes it.</summary>
    public static T Deserialize<T>(Stream stream)
        where T : ITightPackable<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = TightPackBufferWriter.Rent();
        try
        {
            Span<byte> chunk = stackalloc byte[8192];
            int read;
            while ((read = stream.Read(chunk)) > 0)
            {
                chunk[..read].CopyTo(buffer.GetSpan(read));
                buffer.Advance(read);
            }

            return Deserialize<T>(buffer.WrittenSpan);
        }
        finally
        {
            TightPackBufferWriter.Return(buffer);
        }
    }

    /// <summary>
    /// Serializes a value reached only as <see cref="object"/> and <see cref="Type"/> - a plugin's
    /// event type obtained by reflection, for instance. Every other path is generic and never
    /// touches the provider's untyped lookup.
    /// </summary>
    public static byte[] SerializeUntyped(Type type, object? value)
    {
        ArgumentNullException.ThrowIfNull(type);

        ITightPackableUntyped formatter =
            TightPackFormatterProvider.GetFormatter(type) as ITightPackableUntyped
            ?? throw TightPackSerializationException.NotRegistered(type);

        var buffer = TightPackBufferWriter.Rent();
        try
        {
            var writer = new TightPackWriter<TightPackBufferWriter>(ref buffer);
            formatter.SerializeUntyped(ref writer, value);
            writer.Flush();
            return buffer.ToArray();
        }
        finally
        {
            TightPackBufferWriter.Return(buffer);
        }
    }
}
