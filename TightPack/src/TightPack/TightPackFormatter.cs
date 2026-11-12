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
public sealed class TightPackableFormatter<T> : TightPackFormatter<T>, ITightPackableUntyped
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
    public void SerializeUntyped<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, object? value)
        where TBufferWriter : IBufferWriter<byte>
    {
        T typed = (T)value!;
        T.Serialize(ref writer, in typed);
    }
}

/// <summary>
/// Lets a caller that holds only a <see cref="Type"/> serialize through the generated code without
/// binding a generic method by reflection.
/// </summary>
public interface ITightPackableUntyped
{
    void SerializeUntyped<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, object? value)
        where TBufferWriter : IBufferWriter<byte>;
}

internal sealed class ErrorFormatter<T> : TightPackFormatter<T>
{
    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value) =>
        TightPackSerializationException.ThrowNotRegistered(typeof(T));

    public override void Deserialize(ref TightPackReader reader, ref T? value) =>
        TightPackSerializationException.ThrowNotRegistered(typeof(T));
}
