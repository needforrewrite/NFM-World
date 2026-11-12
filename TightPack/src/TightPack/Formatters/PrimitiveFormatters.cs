using System.Globalization;
using System.Numerics;
using System.Text;

namespace TightPack.Formatters;

/// <summary>
/// Formatters for the BCL types that are not written inline by generated code. Each one writes
/// fields using the writer's fixed-width primitives, so a stray padding byte never reaches the wire.
/// </summary>
public static class BuiltinFormatters
{
    public static void RegisterAll()
    {
        Register(new BooleanFormatter());
        Register(new ByteFormatter());
        Register(new SByteFormatter());
        Register(new CharFormatter());
        Register(new Int16Formatter());
        Register(new UInt16Formatter());
        Register(new Int32Formatter());
        Register(new UInt32Formatter());
        Register(new Int64Formatter());
        Register(new UInt64Formatter());
        Register(new SingleFormatter());
        Register(new DoubleFormatter());
        Register(new HalfFormatter());
        Register(new Int128Formatter());
        Register(new UInt128Formatter());
        Register(new DecimalFormatter());
        Register(new StringFormatter());
        Register(new GuidFormatter());
        Register(new DateTimeFormatter());
        Register(new DateTimeOffsetFormatter());
        Register(new TimeSpanFormatter());
        Register(new DateOnlyFormatter());
        Register(new TimeOnlyFormatter());
        Register(new RuneFormatter());
        Register(new BigIntegerFormatter());
        Register(new VersionFormatter());
        Register(new UriFormatter());
        Register(new StringBuilderFormatter());
        Register(new CultureInfoFormatter());
        Register(new TimeZoneInfoFormatter());
        Register(new BitArrayFormatter());
        Register(new ScalarArrayFormatters.ByteArrayFormatter());
        Register(new Vector2Formatter());
        Register(new Vector3Formatter());
        Register(new Vector4Formatter());
        Register(new QuaternionFormatter());
        Register(new ReadOnlyMemoryByteFormatter());
        Register(new MemoryByteFormatter());
    }

    private static void Register<T>(TightPackFormatter<T> formatter) =>
        TightPackFormatterProvider.Register(formatter);
}

public sealed class BooleanFormatter : TightPackFormatter<bool>
{
    public static readonly BooleanFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in bool value) =>
        writer.WriteBoolean(value);

    public override void Deserialize(ref TightPackReader reader, ref bool value) => value = reader.ReadBoolean();
}

public sealed class ByteFormatter : TightPackFormatter<byte>
{
    public static readonly ByteFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in byte value) =>
        writer.WriteByte(value);

    public override void Deserialize(ref TightPackReader reader, ref byte value) => value = reader.ReadByte();
}

public sealed class SByteFormatter : TightPackFormatter<sbyte>
{
    public static readonly SByteFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in sbyte value) =>
        writer.WriteSByte(value);

    public override void Deserialize(ref TightPackReader reader, ref sbyte value) => value = reader.ReadSByte();
}

public sealed class CharFormatter : TightPackFormatter<char>
{
    public static readonly CharFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in char value) =>
        writer.WriteChar(value);

    public override void Deserialize(ref TightPackReader reader, ref char value) => value = reader.ReadChar();
}

public sealed class Int16Formatter : TightPackFormatter<short>
{
    public static readonly Int16Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in short value) =>
        writer.WriteInt16(value);

    public override void Deserialize(ref TightPackReader reader, ref short value) => value = reader.ReadInt16();
}

public sealed class UInt16Formatter : TightPackFormatter<ushort>
{
    public static readonly UInt16Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ushort value) =>
        writer.WriteUInt16(value);

    public override void Deserialize(ref TightPackReader reader, ref ushort value) => value = reader.ReadUInt16();
}

public sealed class Int32Formatter : TightPackFormatter<int>
{
    public static readonly Int32Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in int value) =>
        writer.WriteInt32(value);

    public override void Deserialize(ref TightPackReader reader, ref int value) => value = reader.ReadInt32();
}

public sealed class UInt32Formatter : TightPackFormatter<uint>
{
    public static readonly UInt32Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in uint value) =>
        writer.WriteUInt32(value);

    public override void Deserialize(ref TightPackReader reader, ref uint value) => value = reader.ReadUInt32();
}

public sealed class Int64Formatter : TightPackFormatter<long>
{
    public static readonly Int64Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in long value) =>
        writer.WriteInt64(value);

    public override void Deserialize(ref TightPackReader reader, ref long value) => value = reader.ReadInt64();
}

public sealed class UInt64Formatter : TightPackFormatter<ulong>
{
    public static readonly UInt64Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ulong value) =>
        writer.WriteUInt64(value);

    public override void Deserialize(ref TightPackReader reader, ref ulong value) => value = reader.ReadUInt64();
}

public sealed class SingleFormatter : TightPackFormatter<float>
{
    public static readonly SingleFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in float value) =>
        writer.WriteSingle(value);

    public override void Deserialize(ref TightPackReader reader, ref float value) => value = reader.ReadSingle();
}

public sealed class DoubleFormatter : TightPackFormatter<double>
{
    public static readonly DoubleFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in double value) =>
        writer.WriteDouble(value);

    public override void Deserialize(ref TightPackReader reader, ref double value) => value = reader.ReadDouble();
}

public sealed class HalfFormatter : TightPackFormatter<Half>
{
    public static readonly HalfFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Half value) =>
        writer.WriteHalf(value);

    public override void Deserialize(ref TightPackReader reader, ref Half value) => value = reader.ReadHalf();
}

public sealed class Int128Formatter : TightPackFormatter<Int128>
{
    public static readonly Int128Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Int128 value) =>
        writer.WriteInt128(value);

    public override void Deserialize(ref TightPackReader reader, ref Int128 value) => value = reader.ReadInt128();
}

public sealed class UInt128Formatter : TightPackFormatter<UInt128>
{
    public static readonly UInt128Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in UInt128 value) =>
        writer.WriteUInt128(value);

    public override void Deserialize(ref TightPackReader reader, ref UInt128 value) => value = reader.ReadUInt128();
}

public sealed class DecimalFormatter : TightPackFormatter<decimal>
{
    public static readonly DecimalFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in decimal value) =>
        writer.WriteDecimal(value);

    public override void Deserialize(ref TightPackReader reader, ref decimal value) => value = reader.ReadDecimal();
}

public sealed class StringFormatter : TightPackFormatter<string>
{
    public static readonly StringFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in string? value) =>
        writer.WriteString(value);

    public override void Deserialize(ref TightPackReader reader, ref string? value) => value = reader.ReadString();
}

public sealed class GuidFormatter : TightPackFormatter<Guid>
{
    public static readonly GuidFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Guid value) =>
        writer.WriteGuid(value);

    public override void Deserialize(ref TightPackReader reader, ref Guid value) => value = reader.ReadGuid();
}

public sealed class DateTimeFormatter : TightPackFormatter<DateTime>
{
    public static readonly DateTimeFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in DateTime value) =>
        writer.WriteDateTime(value);

    public override void Deserialize(ref TightPackReader reader, ref DateTime value) => value = reader.ReadDateTime();
}

public sealed class DateTimeOffsetFormatter : TightPackFormatter<DateTimeOffset>
{
    public static readonly DateTimeOffsetFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in DateTimeOffset value) =>
        writer.WriteDateTimeOffset(value);

    public override void Deserialize(ref TightPackReader reader, ref DateTimeOffset value) => value = reader.ReadDateTimeOffset();
}

public sealed class TimeSpanFormatter : TightPackFormatter<TimeSpan>
{
    public static readonly TimeSpanFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TimeSpan value) =>
        writer.WriteTimeSpan(value);

    public override void Deserialize(ref TightPackReader reader, ref TimeSpan value) => value = reader.ReadTimeSpan();
}

public sealed class DateOnlyFormatter : TightPackFormatter<DateOnly>
{
    public static readonly DateOnlyFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in DateOnly value) =>
        writer.WriteDateOnly(value);

    public override void Deserialize(ref TightPackReader reader, ref DateOnly value) => value = reader.ReadDateOnly();
}

public sealed class TimeOnlyFormatter : TightPackFormatter<TimeOnly>
{
    public static readonly TimeOnlyFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TimeOnly value) =>
        writer.WriteTimeOnly(value);

    public override void Deserialize(ref TightPackReader reader, ref TimeOnly value) => value = reader.ReadTimeOnly();
}

public sealed class RuneFormatter : TightPackFormatter<Rune>
{
    public static readonly RuneFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Rune value) =>
        writer.WriteRune(value);

    public override void Deserialize(ref TightPackReader reader, ref Rune value) => value = reader.ReadRune();
}

/// <summary>Writes enums through their underlying integer type, never as a padded struct image.</summary>
public sealed class EnumFormatter<TEnum> : TightPackFormatter<TEnum>
    where TEnum : struct, Enum
{
    public static readonly EnumFormatter<TEnum> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TEnum value) =>
        writer.WriteEnum(value);

    public override void Deserialize(ref TightPackReader reader, ref TEnum value) => value = reader.ReadEnum<TEnum>();
}

public sealed class BigIntegerFormatter : TightPackFormatter<BigInteger>
{
    public static readonly BigIntegerFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in BigInteger value)
    {
        Span<byte> scratch = stackalloc byte[64];
        if (value.TryWriteBytes(scratch, out int written, isUnsigned: false, isBigEndian: false))
        {
            writer.WriteCollectionHeader(written);
            writer.WriteBytes(scratch[..written]);
            return;
        }

        byte[] bytes = value.ToByteArray(isUnsigned: false, isBigEndian: false);
        writer.WriteCollectionHeader(bytes.Length);
        writer.WriteBytes(bytes);
    }

    public override void Deserialize(ref TightPackReader reader, ref BigInteger value)
    {
        byte[]? bytes = reader.ReadByteArray();
        value = bytes is null ? BigInteger.Zero : new BigInteger(bytes);
    }
}

public sealed class VersionFormatter : TightPackFormatter<Version>
{
    public static readonly VersionFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Version? value)
    {
        if (value is null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(4);
        writer.WriteInt32(value.Major);
        writer.WriteInt32(value.Minor);
        writer.WriteInt32(value.Build);
        writer.WriteInt32(value.Revision);
    }

    public override void Deserialize(ref TightPackReader reader, ref Version? value)
    {
        if (!reader.TryReadObjectHeader(out _))
        {
            value = null;
            return;
        }

        int major = reader.ReadInt32();
        int minor = reader.ReadInt32();
        int build = reader.ReadInt32();
        int revision = reader.ReadInt32();
        value = revision < 0
            ? new Version(major, minor, build)
            : new Version(major, minor, build, revision);
    }
}

public sealed class UriFormatter : TightPackFormatter<Uri>
{
    public static readonly UriFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Uri? value) =>
        writer.WriteString(value?.OriginalString);

    public override void Deserialize(ref TightPackReader reader, ref Uri? value)
    {
        string? text = reader.ReadString();
        value = text is null ? null : new Uri(text, UriKind.RelativeOrAbsolute);
    }
}

public sealed class StringBuilderFormatter : TightPackFormatter<StringBuilder>
{
    public static readonly StringBuilderFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in StringBuilder? value) =>
        writer.WriteString(value?.ToString());

    public override void Deserialize(ref TightPackReader reader, ref StringBuilder? value)
    {
        string? text = reader.ReadString();
        value = text is null ? null : new StringBuilder(text);
    }
}

public sealed class CultureInfoFormatter : TightPackFormatter<CultureInfo>
{
    public static readonly CultureInfoFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in CultureInfo? value) =>
        writer.WriteString(value?.Name);

    public override void Deserialize(ref TightPackReader reader, ref CultureInfo? value)
    {
        string? name = reader.ReadString();
        value = name is null ? null : CultureInfo.GetCultureInfo(name);
    }
}

public sealed class TimeZoneInfoFormatter : TightPackFormatter<TimeZoneInfo>
{
    public static readonly TimeZoneInfoFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in TimeZoneInfo? value) =>
        writer.WriteString(value?.Id);

    public override void Deserialize(ref TightPackReader reader, ref TimeZoneInfo? value)
    {
        string? id = reader.ReadString();
        value = id is null ? null : TimeZoneInfo.FindSystemTimeZoneById(id);
    }
}

/// <summary>Formatters that are the blit path for element types proven padding-free.</summary>
public static class ScalarArrayFormatters
{
    public sealed class ByteArrayFormatter : TightPackFormatter<byte[]>
    {
        public static readonly ByteArrayFormatter Default = new();

        public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in byte[]? value) =>
            writer.WriteBlittableArray(value);

        public override void Deserialize(ref TightPackReader reader, ref byte[]? value) =>
            value = reader.ReadBlittableArray<byte>();
    }
}
