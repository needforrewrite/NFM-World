using System.Numerics;
using System.Runtime.CompilerServices;
using Maxine.Extensions;
using Maxine.Extensions.Mathematics;
using NFMWorldLibrary.FixedMath;
using TightPack;

namespace NFMWorldLibrary.Serialization;

// Formatters for the value types NFMWorld serializes that live in assemblies which do not reference
// TightPack, so they cannot carry [TightPackable] themselves.

/// <summary>Writes <c>fix64</c> as its raw 64-bit value; the type is a single <c>long</c>.</summary>
public sealed class Fixed64Formatter : TightPackFormatter<fix64>
{
    public static readonly Fixed64Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in fix64 value) =>
        writer.WriteInt64(value.rawValue);

    public override void Deserialize(ref TightPackReader reader, ref fix64 value) =>
        value = fix64.FromRaw(reader.ReadInt64());
}

/// <summary>Writes <c>f64Vector3</c> as three raw fixed-point values.</summary>
public sealed class Vector3dFormatter : TightPackFormatter<f64Vector3>
{
    public static readonly Vector3dFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in f64Vector3 value)
    {
        writer.WriteInt64(value.X.rawValue);
        writer.WriteInt64(value.Y.rawValue);
        writer.WriteInt64(value.Z.rawValue);
    }

    public override void Deserialize(ref TightPackReader reader, ref f64Vector3 value)
    {
        fix64 x = fix64.FromRaw(reader.ReadInt64());
        fix64 y = fix64.FromRaw(reader.ReadInt64());
        fix64 z = fix64.FromRaw(reader.ReadInt64());
        value = new f64Vector3(x, y, z);
    }
}

/// <summary>Writes a 3-component integer vector.</summary>
public sealed class Int3Formatter : TightPackFormatter<Int3>
{
    public static readonly Int3Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Int3 value)
    {
        writer.WriteInt32(value.X);
        writer.WriteInt32(value.Y);
        writer.WriteInt32(value.Z);
    }

    public override void Deserialize(ref TightPackReader reader, ref Int3 value) =>
        value = new Int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
}

/// <summary>Writes a bit container as its single underlying integer.</summary>
public sealed class NibbleFormatter<T> : TightPackFormatter<Nibble<T>>
    where T : IBinaryInteger<T>, IMinMaxValue<T>, IConvertible
{
    public static readonly NibbleFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Nibble<T> value)
    {
        T raw = value.Value;
        writer.WriteValue(in raw);
    }

    public override void Deserialize(ref TightPackReader reader, ref Nibble<T> value)
    {
        T? raw = default;
        reader.ReadValue(ref raw);
        value = new Nibble<T>(raw!);
    }
}

/// <summary>
/// Reads a byte payload into a pooled array. The packet that owns it is disposable, which is why
/// this is a hand-written formatter rather than the plain one.
/// </summary>
public sealed class PooledPayloadFormatter : TightPackFormatter<ReadOnlyMemory<byte>>
{
    public static readonly PooledPayloadFormatter Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyMemory<byte> value
    ) =>
        TightPack.Formatters.PooledReadOnlyMemoryByteFormatter.Default.Serialize(ref writer, in value);

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyMemory<byte> value) =>
        TightPack.Formatters.PooledReadOnlyMemoryByteFormatter.Default.Deserialize(ref reader, ref value);
}

internal static class TightPackRegistration
{
    /// <summary>
    /// Runs before any type that could ask for one of these formatters, which is what lets a
    /// member carrying [TightPackAllowSerialize] resolve without reflection.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        TightPackFormatterProvider.Register(Fixed64Formatter.Default);
        TightPackFormatterProvider.Register(Vector3dFormatter.Default);
        TightPackFormatterProvider.Register(Int3Formatter.Default);
        TightPackFormatterProvider.Register(F64EulerFormatter.Default);
        TightPackFormatterProvider.Register(NibbleFormatter<uint>.Default);
        TightPackFormatterProvider.Register(new TightPack.Formatters.InlineArray4Formatter<fix64>());
    }
}

/// <summary>Writes an Euler rotation as three degree values.</summary>
public sealed class F64EulerFormatter : TightPackFormatter<f64Euler>
{
    public static readonly F64EulerFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in f64Euler value)
    {
        writer.WriteInt64(value.Yaw.Degrees.rawValue);
        writer.WriteInt64(value.Pitch.Degrees.rawValue);
        writer.WriteInt64(value.Roll.Degrees.rawValue);
    }

    public override void Deserialize(ref TightPackReader reader, ref f64Euler value)
    {
        fix64 yaw = fix64.FromRaw(reader.ReadInt64());
        fix64 pitch = fix64.FromRaw(reader.ReadInt64());
        fix64 roll = fix64.FromRaw(reader.ReadInt64());
        value = new f64Euler
        {
            Yaw = new f64AngleSingle { Degrees = yaw },
            Pitch = new f64AngleSingle { Degrees = pitch },
            Roll = new f64AngleSingle { Degrees = roll },
        };
    }
}
