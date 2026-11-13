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
        for (int j = 0; j < 2; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray2<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 2; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray3Formatter<T> : TightPackFormatter<InlineArray3<T>>
{
    public static readonly InlineArray3Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray3<T> value)
    {
        InlineArray3<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 3; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray3<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 3; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray4Formatter<T> : TightPackFormatter<InlineArray4<T>>
{
    public static readonly InlineArray4Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray4<T> value)
    {
        InlineArray4<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 4; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray4<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 4; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray5Formatter<T> : TightPackFormatter<InlineArray5<T>>
{
    public static readonly InlineArray5Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray5<T> value)
    {
        InlineArray5<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 5; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray5<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 5; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray6Formatter<T> : TightPackFormatter<InlineArray6<T>>
{
    public static readonly InlineArray6Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray6<T> value)
    {
        InlineArray6<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 6; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray6<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 6; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray7Formatter<T> : TightPackFormatter<InlineArray7<T>>
{
    public static readonly InlineArray7Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray7<T> value)
    {
        InlineArray7<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 7; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray7<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 7; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray8Formatter<T> : TightPackFormatter<InlineArray8<T>>
{
    public static readonly InlineArray8Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray8<T> value)
    {
        InlineArray8<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 8; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray8<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 8; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray9Formatter<T> : TightPackFormatter<InlineArray9<T>>
{
    public static readonly InlineArray9Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray9<T> value)
    {
        InlineArray9<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 9; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray9<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 9; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray10Formatter<T> : TightPackFormatter<InlineArray10<T>>
{
    public static readonly InlineArray10Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray10<T> value)
    {
        InlineArray10<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 10; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray10<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 10; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray11Formatter<T> : TightPackFormatter<InlineArray11<T>>
{
    public static readonly InlineArray11Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray11<T> value)
    {
        InlineArray11<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 11; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray11<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 11; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray12Formatter<T> : TightPackFormatter<InlineArray12<T>>
{
    public static readonly InlineArray12Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray12<T> value)
    {
        InlineArray12<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 12; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray12<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 12; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray13Formatter<T> : TightPackFormatter<InlineArray13<T>>
{
    public static readonly InlineArray13Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray13<T> value)
    {
        InlineArray13<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 13; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray13<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 13; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray14Formatter<T> : TightPackFormatter<InlineArray14<T>>
{
    public static readonly InlineArray14Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray14<T> value)
    {
        InlineArray14<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 14; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray14<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 14; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray15Formatter<T> : TightPackFormatter<InlineArray15<T>>
{
    public static readonly InlineArray15Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray15<T> value)
    {
        InlineArray15<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 15; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray15<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 15; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}

/// <summary>
/// Element-wise formatters for BCL inline arrays. The element count is fixed by the type, so no
/// length is written; only the elements are.
/// </summary>
public sealed class InlineArray16Formatter<T> : TightPackFormatter<InlineArray16<T>>
{
    public static readonly InlineArray16Formatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in InlineArray16<T> value)
    {
        InlineArray16<T> copy = value;
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 16; j++)
        {
            T? item = copy[j];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref InlineArray16<T> value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int j = 0; j < 16; j++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            value[j] = item!;
        }
    }
}
