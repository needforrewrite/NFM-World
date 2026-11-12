namespace TightPack.Formatters;

/// <summary>
/// Writes a <see cref="Type"/> as <c>FullName, AssemblyName</c> and resolves it with
/// <see cref="Type.GetType(string, bool)"/>.
/// </summary>
/// <remarks>
/// The version, culture and public key token are deliberately omitted: a rebuild changes the version
/// of every assembly, and a type name that pins one can never be read back. That name cannot name a
/// type from a collectible plugin load context either - <see cref="Type.GetType(string, bool)"/>
/// only looks in the default context and the calling assembly, so a plugin type is refused rather
/// than resolving to the wrong assembly. Under NativeAOT the type must also be reachable from a
/// metadata root; a trimmed build cannot resolve a name it never saw.
/// </remarks>
public sealed class TypeFormatter : TightPackFormatter<Type>
{
    public static readonly TypeFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Type? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        // An open generic parameter or a by-ref has no FullName; AssemblyQualifiedName has one.
        string? fullName = value.FullName ?? value.AssemblyQualifiedName;
        string? assembly = value.Assembly.GetName().Name;

        if (fullName is null || assembly is null)
        {
            TightPackSerializationException.ThrowMessage($"Cannot name {value} for serialization.");
            return;
        }

        writer.WriteString($"{fullName}, {assembly}");
    }

    public override void Deserialize(ref TightPackReader reader, ref Type? value)
    {
        string? name = reader.ReadString();
        if (name is null)
        {
            value = null;
            return;
        }

        Type? resolved = Type.GetType(name, throwOnError: false);
        if (resolved is null)
        {
            TightPackSerializationException.ThrowMessage($"Cannot resolve the serialized type '{name}'.");
            return;
        }

        value = resolved;
    }
}
