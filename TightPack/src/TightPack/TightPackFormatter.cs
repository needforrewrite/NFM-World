using System.Buffers;

namespace TightPack;

/// <summary>
/// A hand-written formatter for a single type. Resolved either by the generator (member with a
/// <see cref="TightPackCustomFormatterAttribute"/>) or through <see cref="TightPackFormatterProvider"/>.
/// </summary>
public abstract class TightPackFormatter<T>
{
    public abstract void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value)
        where TBufferWriter : IBufferWriter<byte>;

    public abstract void Deserialize(ref TightPackReader reader, ref T? value);
}

/// <summary>Adapts a generated <see cref="ITightPackable{TSelf}"/> to the formatter API.</summary>
public sealed class TightPackableFormatter<T> : TightPackFormatter<T>, IUntypedValueFormatter
    where T : ITightPackable<T>
{
    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value)
    {
        T.Serialize(ref writer, in value!);
    }

    public override void Deserialize(ref TightPackReader reader, ref T? value)
    {
        T? local = value;
        T.Deserialize(ref reader, ref local!);
        value = local;
    }

    /// <inheritdoc/>
    public void WriteUntyped<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, object? value)
        where TBufferWriter : IBufferWriter<byte>
    {
        T typed = value is null ? default! : (T)value;
        T.Serialize(ref writer, in typed);
    }

    /// <inheritdoc/>
    public void ReadUntyped(ref TightPackReader reader, ref object? value)
    {
        T? typed = value is T existing ? existing : default;
        T.Deserialize(ref reader, ref typed!);
        value = typed;
    }
}

/// <summary>
/// Writes and reads a value that is only known as <see cref="object"/> and <see cref="Type"/>. The
/// buffer writer stays a generic parameter so the writer can remain a ref struct.
/// </summary>
public interface IUntypedValueFormatter
{
    void WriteUntyped<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, object? value)
        where TBufferWriter : IBufferWriter<byte>;

    void ReadUntyped(ref TightPackReader reader, ref object? value);
}

/// <summary>
/// Bridges a formatter of any <typeparamref name="T"/> to <see cref="IUntypedValueFormatter"/>. The
/// formatter is resolved on every call, so a registration that lands later is never masked.
/// </summary>
internal sealed class UntypedFormatterAdapter<T> : IUntypedValueFormatter
{
    public void WriteUntyped<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, object? value)
        where TBufferWriter : IBufferWriter<byte>
    {
        TightPackFormatter<T> formatter = TightPackFormatterProvider.GetFormatter<T>();
        T typed = value is null ? default! : (T)value;
        formatter.Serialize(ref writer, in typed);
    }

    public void ReadUntyped(ref TightPackReader reader, ref object? value)
    {
        TightPackFormatter<T> formatter = TightPackFormatterProvider.GetFormatter<T>();
        T? typed = default;
        formatter.Deserialize(ref reader, ref typed);
        value = typed;
    }
}

internal sealed class ErrorFormatter<T> : TightPackFormatter<T>
{
    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value) =>
        TightPackSerializationException.ThrowNotRegistered(typeof(T));

    public override void Deserialize(ref TightPackReader reader, ref T? value) =>
        TightPackSerializationException.ThrowNotRegistered(typeof(T));
}
