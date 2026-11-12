using System.Collections;

namespace TightPack.Formatters;

/// <summary>Writes <c>Nullable&lt;T&gt;</c> as a presence byte plus the value.</summary>
public sealed class NullableFormatter<T> : TightPackFormatter<T?>
    where T : struct
{
    public static readonly NullableFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value)
    {
        if (!value.HasValue)
        {
            writer.WriteBoolean(false);
            return;
        }

        writer.WriteBoolean(true);
        writer.WriteValue(value.Value);
    }

    public override void Deserialize(ref TightPackReader reader, ref T? value)
    {
        if (!reader.ReadBoolean())
        {
            value = null;
            return;
        }

        T inner = default;
        reader.ReadValue(ref inner);
        value = inner;
    }
}

public sealed class ArrayFormatter<T> : TightPackFormatter<T?[]?>
{
    public static readonly ArrayFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T?[]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < value.Length; i++)
        {
            formatter.Serialize(ref writer, in value[i]);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref T?[]? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        if (length == 0)
        {
            value = [];
            return;
        }

        var result = new T?[length];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            formatter.Deserialize(ref reader, ref result[i]);
        }

        value = result;
    }
}

public sealed class ListFormatter<T> : TightPackFormatter<List<T>?>
{
    public static readonly ListFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in List<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Count);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < value.Count; i++)
        {
            T? item = value[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref List<T>? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        var result = new List<T>(length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result.Add(item!);
        }

        value = result;
    }
}

/// <summary>Adapts any <see cref="ICollection{T}"/>-like type with a parameterless constructor.</summary>
public sealed class GenericCollectionFormatter<TCollection, TElement> : TightPackFormatter<TCollection?>
    where TCollection : ICollection<TElement>, new()
{
    public static readonly GenericCollectionFormatter<TCollection, TElement> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TCollection? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Count);
        var formatter = TightPackFormatterProvider.GetFormatter<TElement>();
        foreach (TElement item in value)
        {
            TElement? element = item;
            formatter.Serialize(ref writer, in element);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref TCollection? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        var result = new TCollection();
        var formatter = TightPackFormatterProvider.GetFormatter<TElement>();
        for (int i = 0; i < length; i++)
        {
            TElement? item = default;
            formatter.Deserialize(ref reader, ref item);
            result.Add(item!);
        }

        value = result;
    }
}

public sealed class HashSetFormatter<T> : TightPackFormatter<HashSet<T>?>
{
    public static readonly HashSetFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in HashSet<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Count);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        foreach (T item in value)
        {
            T? element = item;
            formatter.Serialize(ref writer, in element);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref HashSet<T>? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        var result = new HashSet<T>(length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result.Add(item!);
        }

        value = result;
    }
}

public sealed class DictionaryFormatter<TKey, TValue> : TightPackFormatter<Dictionary<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly DictionaryFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Dictionary<TKey, TValue>? value)
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
            TKey key = pair.Key;
            TValue? item = pair.Value;
            keyFormatter.Serialize(ref writer, in key);
            valueFormatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref Dictionary<TKey, TValue>? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
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

        value = result;
    }
}

public sealed class KeyValuePairFormatter<TKey, TValue> : TightPackFormatter<KeyValuePair<TKey, TValue>>
{
    public static readonly KeyValuePairFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in KeyValuePair<TKey, TValue> value)
    {
        TKey key = value.Key;
        TValue item = value.Value;
        writer.WriteValue(in key);
        writer.WriteValue(in item);
    }

    public override void Deserialize(ref TightPackReader reader, ref KeyValuePair<TKey, TValue> value)
    {
        TKey? key = default;
        TValue? item = default;
        reader.ReadValue(ref key);
        reader.ReadValue(ref item);
        value = new KeyValuePair<TKey, TValue>(key!, item!);
    }
}

public sealed class BitArrayFormatter : TightPackFormatter<BitArray?>
{
    public static readonly BitArrayFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in BitArray? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        writer.WriteCollectionHeader(value.Count);
        var bytes = new byte[(value.Length + 7) / 8];
        value.CopyTo(bytes, 0);
        writer.WriteBlittableArray(bytes);
    }

    public override void Deserialize(ref TightPackReader reader, ref BitArray? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        byte[]? bytes = reader.ReadBlittableArray<byte>();
        var result = new BitArray(length);
        for (int i = 0; i < length; i++)
        {
            int index = i >> 3;
            result[i] = index < (bytes?.Length ?? 0) && (bytes![index] & (1 << (i & 7))) != 0;
        }

        value = result;
    }
}

/// <summary>
/// Boxes an <see cref="IEnumerable{T}"/> into a list. Registered only when a member opts in with
/// <see cref="TightPackAllowSerializeAttribute"/>.
/// </summary>
public sealed class EnumerableFormatter<T> : TightPackFormatter<IEnumerable<T>?>
{
    public static readonly EnumerableFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in IEnumerable<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        // The length header is a true count, so an unsized sequence is materialised once.
        IEnumerable<T> sequence = value;
        if (!value.TryGetNonEnumeratedCount(out int count))
        {
            var items = new List<T>(value);
            sequence = items;
            count = items.Count;
        }

        writer.WriteCollectionHeader(count);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        foreach (T item in sequence)
        {
            T? element = item;
            formatter.Serialize(ref writer, in element);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref IEnumerable<T>? value)
    {
        List<T>? list = null;
        TightPackFormatterProvider.GetFormatter<List<T>>().Deserialize(ref reader, ref list);
        value = list;
    }
}
