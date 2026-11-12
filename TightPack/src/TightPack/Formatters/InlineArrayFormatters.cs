using System.Runtime.CompilerServices;

namespace TightPack.Formatters;

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray2Formatter<T> : TightPackFormatter<InlineArray2<T>>
{
    public static readonly InlineArray2Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray2<T> value)
    {
        InlineArray2<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 2; i++)
        {
            T? item = copy[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray2<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 2; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[i] = item!;
        }
    }
}

public sealed class InlineArray3Formatter<T> : TightPackFormatter<InlineArray3<T>>
{
    public static readonly InlineArray3Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray3<T> value)
    {
        InlineArray3<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 3; i++)
        {
            T? item = copy[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray3<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 3; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[i] = item!;
        }
    }
}

public sealed class InlineArray4Formatter<T> : TightPackFormatter<InlineArray4<T>>
{
    public static readonly InlineArray4Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray4<T> value)
    {
        InlineArray4<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 4; i++)
        {
            T? item = copy[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray4<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < 4; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[i] = item!;
        }
    }
}
