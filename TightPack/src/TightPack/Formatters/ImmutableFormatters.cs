using System.Collections.Immutable;

namespace TightPack.Formatters;

/// <summary>
/// Writes an <see cref="ImmutableArray{T}"/>. The default (uninitialised) array is distinguishable
/// from an empty one, so deserialization never silently turns one into the other.
/// </summary>
public sealed class ImmutableArrayFormatter<T> : TightPackFormatter<ImmutableArray<T>>
{
    public static readonly ImmutableArrayFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ImmutableArray<T> value
    )
    {
        if (value.IsDefault)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < value.Length; i++)
        {
            T item = value[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref ImmutableArray<T> value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        if (length == 0)
        {
            value = ImmutableArray<T>.Empty;
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

        value = ImmutableArray.Create(items);
    }
}
