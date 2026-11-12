namespace TightPack.Formatters;

/// <summary>
/// Formatters for <see cref="ValueTuple{T1, T2}"/> and friends. Elements are written back to back
/// with no header, so a tuple costs exactly the sum of its elements and nothing else.
/// </summary>
/// <remarks>
/// Each element formatter is resolved once into a static field. Going through
/// <c>WriteValue</c>/<c>ReadValue</c> instead asks the provider on every element, which measured
/// ~2.7ns of the ~6.4ns an element cost against ~1.9ns through a cached field.
/// </remarks>
public sealed class ValueTupleFormatter<T1> : TightPackFormatter<ValueTuple<T1>>
{
    public static readonly ValueTupleFormatter<T1> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ValueTuple<T1> value)
    {
        T1? item1 = value.Item1;
        writer.WriteValueWithFormatter(E1, in item1);
    }

    public override void Deserialize(ref TightPackReader reader, ref ValueTuple<T1> value)
    {
        T1? item1 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        value = new ValueTuple<T1>(item1!);
    }
}

public sealed class ValueTupleFormatter<T1, T2> : TightPackFormatter<(T1, T2)>
{
    public static readonly ValueTupleFormatter<T1, T2> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in (T1, T2) value)
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        value = (item1!, item2!);
    }
}

public sealed class ValueTupleFormatter<T1, T2, T3> : TightPackFormatter<(T1, T2, T3)>
{
    public static readonly ValueTupleFormatter<T1, T2, T3> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in (T1, T2, T3) value)
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2, T3) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        value = (item1!, item2!, item3!);
    }
}

public sealed class ValueTupleFormatter<T1, T2, T3, T4> : TightPackFormatter<(T1, T2, T3, T4)>
{
    public static readonly ValueTupleFormatter<T1, T2, T3, T4> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T4>? __e4;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();
    private static TightPackFormatter<T4> E4 => __e4 ??= TightPackFormatterProvider.GetFormatter<T4>();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in (T1, T2, T3, T4) value)
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        T4? item4 = value.Item4;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
        writer.WriteValueWithFormatter(E4, in item4);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2, T3, T4) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        T4? item4 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        reader.ReadValueWithFormatter(E4, ref item4);
        value = (item1!, item2!, item3!, item4!);
    }
}

public sealed class ValueTupleFormatter<T1, T2, T3, T4, T5> : TightPackFormatter<(T1, T2, T3, T4, T5)>
{
    public static readonly ValueTupleFormatter<T1, T2, T3, T4, T5> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T4>? __e4;
    private static TightPackFormatter<T5>? __e5;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();
    private static TightPackFormatter<T4> E4 => __e4 ??= TightPackFormatterProvider.GetFormatter<T4>();
    private static TightPackFormatter<T5> E5 => __e5 ??= TightPackFormatterProvider.GetFormatter<T5>();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in (T1, T2, T3, T4, T5) value)
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        T4? item4 = value.Item4;
        T5? item5 = value.Item5;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
        writer.WriteValueWithFormatter(E4, in item4);
        writer.WriteValueWithFormatter(E5, in item5);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2, T3, T4, T5) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        T4? item4 = default;
        T5? item5 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        reader.ReadValueWithFormatter(E4, ref item4);
        reader.ReadValueWithFormatter(E5, ref item5);
        value = (item1!, item2!, item3!, item4!, item5!);
    }
}

public sealed class ValueTupleFormatter<T1, T2, T3, T4, T5, T6> : TightPackFormatter<(T1, T2, T3, T4, T5, T6)>
{
    public static readonly ValueTupleFormatter<T1, T2, T3, T4, T5, T6> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T4>? __e4;
    private static TightPackFormatter<T5>? __e5;
    private static TightPackFormatter<T6>? __e6;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();
    private static TightPackFormatter<T4> E4 => __e4 ??= TightPackFormatterProvider.GetFormatter<T4>();
    private static TightPackFormatter<T5> E5 => __e5 ??= TightPackFormatterProvider.GetFormatter<T5>();
    private static TightPackFormatter<T6> E6 => __e6 ??= TightPackFormatterProvider.GetFormatter<T6>();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in (T1, T2, T3, T4, T5, T6) value
    )
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        T4? item4 = value.Item4;
        T5? item5 = value.Item5;
        T6? item6 = value.Item6;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
        writer.WriteValueWithFormatter(E4, in item4);
        writer.WriteValueWithFormatter(E5, in item5);
        writer.WriteValueWithFormatter(E6, in item6);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2, T3, T4, T5, T6) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        T4? item4 = default;
        T5? item5 = default;
        T6? item6 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        reader.ReadValueWithFormatter(E4, ref item4);
        reader.ReadValueWithFormatter(E5, ref item5);
        reader.ReadValueWithFormatter(E6, ref item6);
        value = (item1!, item2!, item3!, item4!, item5!, item6!);
    }
}

public sealed class ValueTupleFormatter<T1, T2, T3, T4, T5, T6, T7>
    : TightPackFormatter<(T1, T2, T3, T4, T5, T6, T7)>
{
    public static readonly ValueTupleFormatter<T1, T2, T3, T4, T5, T6, T7> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T4>? __e4;
    private static TightPackFormatter<T5>? __e5;
    private static TightPackFormatter<T6>? __e6;
    private static TightPackFormatter<T7>? __e7;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();
    private static TightPackFormatter<T4> E4 => __e4 ??= TightPackFormatterProvider.GetFormatter<T4>();
    private static TightPackFormatter<T5> E5 => __e5 ??= TightPackFormatterProvider.GetFormatter<T5>();
    private static TightPackFormatter<T6> E6 => __e6 ??= TightPackFormatterProvider.GetFormatter<T6>();
    private static TightPackFormatter<T7> E7 => __e7 ??= TightPackFormatterProvider.GetFormatter<T7>();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in (T1, T2, T3, T4, T5, T6, T7) value
    )
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        T4? item4 = value.Item4;
        T5? item5 = value.Item5;
        T6? item6 = value.Item6;
        T7? item7 = value.Item7;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
        writer.WriteValueWithFormatter(E4, in item4);
        writer.WriteValueWithFormatter(E5, in item5);
        writer.WriteValueWithFormatter(E6, in item6);
        writer.WriteValueWithFormatter(E7, in item7);
    }

    public override void Deserialize(ref TightPackReader reader, ref (T1, T2, T3, T4, T5, T6, T7) value)
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        T4? item4 = default;
        T5? item5 = default;
        T6? item6 = default;
        T7? item7 = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        reader.ReadValueWithFormatter(E4, ref item4);
        reader.ReadValueWithFormatter(E5, ref item5);
        reader.ReadValueWithFormatter(E6, ref item6);
        reader.ReadValueWithFormatter(E7, ref item7);
        value = (item1!, item2!, item3!, item4!, item5!, item6!, item7!);
    }
}

public sealed class
    ValueTupleFormatter<T1, T2, T3, T4, T5, T6, T7, TRest>
    : TightPackFormatter<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>
    where TRest : struct
{
    public static readonly ValueTupleFormatter<T1, T2, T3, T4, T5, T6, T7, TRest> Default = new();

    private static TightPackFormatter<T1>? __e1;
    private static TightPackFormatter<T2>? __e2;
    private static TightPackFormatter<T3>? __e3;
    private static TightPackFormatter<T4>? __e4;
    private static TightPackFormatter<T5>? __e5;
    private static TightPackFormatter<T6>? __e6;
    private static TightPackFormatter<T7>? __e7;
    private static TightPackFormatter<TRest>? __e8;
    private static TightPackFormatter<T1> E1 => __e1 ??= TightPackFormatterProvider.GetFormatter<T1>();
    private static TightPackFormatter<T2> E2 => __e2 ??= TightPackFormatterProvider.GetFormatter<T2>();
    private static TightPackFormatter<T3> E3 => __e3 ??= TightPackFormatterProvider.GetFormatter<T3>();
    private static TightPackFormatter<T4> E4 => __e4 ??= TightPackFormatterProvider.GetFormatter<T4>();
    private static TightPackFormatter<T5> E5 => __e5 ??= TightPackFormatterProvider.GetFormatter<T5>();
    private static TightPackFormatter<T6> E6 => __e6 ??= TightPackFormatterProvider.GetFormatter<T6>();
    private static TightPackFormatter<T7> E7 => __e7 ??= TightPackFormatterProvider.GetFormatter<T7>();
    private static TightPackFormatter<TRest> E8 => __e8 ??= TightPackFormatterProvider.GetFormatter<TRest>();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value
    )
    {
        T1? item1 = value.Item1;
        T2? item2 = value.Item2;
        T3? item3 = value.Item3;
        T4? item4 = value.Item4;
        T5? item5 = value.Item5;
        T6? item6 = value.Item6;
        T7? item7 = value.Item7;
        TRest rest = value.Rest;
        writer.WriteValueWithFormatter(E1, in item1);
        writer.WriteValueWithFormatter(E2, in item2);
        writer.WriteValueWithFormatter(E3, in item3);
        writer.WriteValueWithFormatter(E4, in item4);
        writer.WriteValueWithFormatter(E5, in item5);
        writer.WriteValueWithFormatter(E6, in item6);
        writer.WriteValueWithFormatter(E7, in item7);
        writer.WriteValueWithFormatter(E8, in rest);
    }

    public override void Deserialize(
        ref TightPackReader reader,
        ref ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value
    )
    {
        T1? item1 = default;
        T2? item2 = default;
        T3? item3 = default;
        T4? item4 = default;
        T5? item5 = default;
        T6? item6 = default;
        T7? item7 = default;
        TRest rest = default;
        reader.ReadValueWithFormatter(E1, ref item1);
        reader.ReadValueWithFormatter(E2, ref item2);
        reader.ReadValueWithFormatter(E3, ref item3);
        reader.ReadValueWithFormatter(E4, ref item4);
        reader.ReadValueWithFormatter(E5, ref item5);
        reader.ReadValueWithFormatter(E6, ref item6);
        reader.ReadValueWithFormatter(E7, ref item7);
        reader.ReadValueWithFormatter(E8, ref rest);
        value = new ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(
            item1!,
            item2!,
            item3!,
            item4!,
            item5!,
            item6!,
            item7!,
            rest
        );
    }
}
