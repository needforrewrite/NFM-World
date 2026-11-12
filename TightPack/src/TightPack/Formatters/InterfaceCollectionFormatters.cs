namespace TightPack.Formatters;

// Interface-typed members are written by the interface's formatter and rebuilt as the concrete type
// that backs it. A caller that needs a specific collection type must declare that type instead.

internal static class InterfaceCollections
{
    public static void WriteSequence<TBufferWriter, T>(
        ref TightPackWriter<TBufferWriter> writer,
        int count,
        IEnumerable<T> items
    )
        where TBufferWriter : System.Buffers.IBufferWriter<byte>
    {
        writer.WriteCollectionHeader(count);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        foreach (T item in items)
        {
            T? value = item;
            formatter.Serialize(ref writer, in value);
        }
    }

    public static List<T>? ReadSequence<T>(ref TightPackReader reader)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            return null;
        }

        var result = new List<T>(length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result.Add(item!);
        }

        return result;
    }

    /// <summary>Reads a sequence into a set, which is what the set interface formatters rebuild.</summary>
    public static HashSet<T>? ReadSet<T>(ref TightPackReader reader)
    {
        List<T>? items = ReadSequence<T>(ref reader);
        return items is null ? null : new HashSet<T>(items);
    }

    public static Dictionary<TKey, TValue>? ReadMap<TKey, TValue>(ref TightPackReader reader)
        where TKey : notnull
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            return null;
        }

        var result = new Dictionary<TKey, TValue>(length);
        var keyFormatter = TightPackFormatterProvider.GetFormatter<TKey>();
        var valueFormatter = TightPackFormatterProvider.GetFormatter<TValue>();
        for (int i = 0; i < length; i++)
        {
            TKey? key = default;
            TValue? item = default;
            keyFormatter.Deserialize(ref reader, ref key);
            valueFormatter.Deserialize(ref reader, ref item);
            result.Add(key!, item!);
        }

        return result;
    }

    public static void WriteMap<TBufferWriter, TKey, TValue>(
        ref TightPackWriter<TBufferWriter> writer,
        int count,
        IDictionary<TKey, TValue> value
    )
        where TBufferWriter : System.Buffers.IBufferWriter<byte>
        where TKey : notnull
    {
        writer.WriteCollectionHeader(count);
        var keyFormatter = TightPackFormatterProvider.GetFormatter<TKey>();
        var valueFormatter = TightPackFormatterProvider.GetFormatter<TValue>();
        foreach (var pair in value)
        {
            TKey? key = pair.Key;
            TValue? item = pair.Value;
            keyFormatter.Serialize(ref writer, in key);
            valueFormatter.Serialize(ref writer, in item);
        }
    }
}

public sealed class InterfaceListFormatter<T> : TightPackFormatter<IList<T>?>
{
    public static readonly InterfaceListFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in IList<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref IList<T>? value) =>
        value = InterfaceCollections.ReadSequence<T>(ref reader);
}

public sealed class IReadOnlyListFormatter<T> : TightPackFormatter<IReadOnlyList<T>?>
{
    public static readonly IReadOnlyListFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in IReadOnlyList<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref IReadOnlyList<T>? value) =>
        value = InterfaceCollections.ReadSequence<T>(ref reader);
}

public sealed class InterfaceCollectionFormatter<T> : TightPackFormatter<ICollection<T>?>
{
    public static readonly InterfaceCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ICollection<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ICollection<T>? value) =>
        value = InterfaceCollections.ReadSequence<T>(ref reader);
}

public sealed class IReadOnlyCollectionFormatter<T> : TightPackFormatter<IReadOnlyCollection<T>?>
{
    public static readonly IReadOnlyCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in IReadOnlyCollection<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref IReadOnlyCollection<T>? value) =>
        value = InterfaceCollections.ReadSequence<T>(ref reader);
}

public sealed class InterfaceDictionaryFormatter<TKey, TValue> : TightPackFormatter<IDictionary<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly InterfaceDictionaryFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in IDictionary<TKey, TValue>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteMap<TBufferWriter, TKey, TValue>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref IDictionary<TKey, TValue>? value) =>
        value = InterfaceCollections.ReadMap<TKey, TValue>(ref reader);
}

public sealed class IReadOnlyDictionaryFormatter<TKey, TValue>
    : TightPackFormatter<IReadOnlyDictionary<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly IReadOnlyDictionaryFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in IReadOnlyDictionary<TKey, TValue>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Count);
        var keyFormatter = TightPackFormatterProvider.GetFormatter<TKey>();
        var valueFormatter = TightPackFormatterProvider.GetFormatter<TValue>();
        foreach (var pair in value)
        {
            TKey? key = pair.Key;
            TValue? item = pair.Value;
            keyFormatter.Serialize(ref writer, in key);
            valueFormatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref IReadOnlyDictionary<TKey, TValue>? value) =>
        value = InterfaceCollections.ReadMap<TKey, TValue>(ref reader);
}

public sealed class InterfaceSetFormatter<T> : TightPackFormatter<ISet<T>?>
{
    public static readonly InterfaceSetFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ISet<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ISet<T>? value) =>
        value = InterfaceCollections.ReadSet<T>(ref reader);
}

public sealed class IReadOnlySetFormatter<T> : TightPackFormatter<IReadOnlySet<T>?>
{
    public static readonly IReadOnlySetFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in IReadOnlySet<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref IReadOnlySet<T>? value) =>
        value = InterfaceCollections.ReadSet<T>(ref reader);
}
