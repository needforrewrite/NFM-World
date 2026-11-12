namespace TightPack.Formatters;

/// <summary>
/// A union whose cases are declared at run time instead of with <c>[TightPackUnion]</c>. Writes a
/// tag chosen by the value's concrete type, then the value through the formatter of that type.
/// </summary>
/// <remarks>
/// Dispatch by <see cref="object"/> reaches <see cref="TightPackWriter{TBufferWriter}.WriteValue(Type, object?)"/>,
/// which resolves a formatter by <see cref="Type"/> at run time. That is a reflection-backed path,
/// so a case type must be reachable for AOT and must be loadable by
/// <see cref="Type.GetType(string, bool)"/> for a round trip through a stored name - prefer
/// <c>[TightPackUnion]</c> on the base type where the cases are known when the code is generated.
/// </remarks>
public sealed class DynamicUnionFormatter<T> : TightPackFormatter<T>
    where T : class
{
    private readonly Dictionary<Type, ushort> _typeToTag;
    private readonly Dictionary<ushort, Type> _tagToType;

    public DynamicUnionFormatter(params (ushort Tag, Type Type)[] unions)
    {
        ArgumentNullException.ThrowIfNull(unions);

        _typeToTag = new Dictionary<Type, ushort>(unions.Length);
        _tagToType = new Dictionary<ushort, Type>(unions.Length);
        foreach ((ushort tag, Type type) in unions)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!_typeToTag.TryAdd(type, tag))
            {
                TightPackSerializationException.ThrowMessage($"Type {type.FullName} is listed twice in the union.");
            }

            if (!_tagToType.TryAdd(tag, type))
            {
                TightPackSerializationException.ThrowMessage($"Union tag {tag} is listed twice.");
            }
        }
    }

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T? value)
    {
        if (value is null)
        {
            writer.WriteNullUnionHeader();
            return;
        }

        Type actual = value.GetType();
        if (!_typeToTag.TryGetValue(actual, out ushort tag))
        {
            TightPackSerializationException.ThrowUnionValueNotMapped(actual, typeof(T));
            return;
        }

        writer.WriteUnionHeader(tag);
        writer.WriteValue(actual, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref T? value)
    {
        if (!reader.TryReadUnionHeader(out ushort tag))
        {
            value = null;
            return;
        }

        if (!_tagToType.TryGetValue(tag, out Type? actual))
        {
            TightPackSerializationException.ThrowInvalidUnionTag(tag, typeof(T));
            return;
        }

        object? boxed = null;
        reader.ReadValue(actual, ref boxed);
        value = (T?)boxed;
    }
}
