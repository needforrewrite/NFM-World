namespace TightPack.Formatters;

/// <summary>
/// Writes the string through <see cref="TightPackWriter{TBufferWriter}.WriteString"/>, which is
/// already UTF-8. Registered only on request: <see cref="StringFormatter"/> owns <c>string</c> by
/// default, so this is for a member that opts in with <c>[TightPackCustomFormatter]</c>.
/// </summary>
public sealed class Utf8StringFormatter : TightPackFormatter<string>
{
    public static readonly Utf8StringFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in string? value) =>
        writer.WriteString(value);

    public override void Deserialize(ref TightPackReader reader, ref string? value) => value = reader.ReadString();
}

/// <summary>
/// Writes the string as UTF-16 code units. Unlike UTF-8 it cannot reject a lone surrogate, so it is
/// the choice for a string that may hold one.
/// </summary>
public sealed class Utf16StringFormatter : TightPackFormatter<string>
{
    public static readonly Utf16StringFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in string? value) =>
        writer.WriteUtf16(value);

    public override void Deserialize(ref TightPackReader reader, ref string? value) => value = reader.ReadUtf16String();
}

/// <summary>Writes like <see cref="StringFormatter"/> and interns the value it reads back.</summary>
public sealed class InternStringFormatter : TightPackFormatter<string>
{
    public static readonly InternStringFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in string? value) =>
        writer.WriteString(value);

    public override void Deserialize(ref TightPackReader reader, ref string? value)
    {
        string? text = reader.ReadString();
        value = text is null ? null : string.Intern(text);
    }
}
