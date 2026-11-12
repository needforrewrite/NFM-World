using System.Numerics;

namespace TightPack.Formatters;

/// <summary>
/// Formatters for <see cref="System.Numerics"/> vectors. Every component is written with the
/// single-precision scalar writer, so the SIMD width the runtime happens to pick never reaches the wire.
/// </summary>
public sealed class Vector2Formatter : TightPackFormatter<Vector2>
{
    public static readonly Vector2Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Vector2 value)
    {
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
    }

    public override void Deserialize(ref TightPackReader reader, ref Vector2 value) =>
        value = new Vector2(reader.ReadSingle(), reader.ReadSingle());
}

public sealed class Vector3Formatter : TightPackFormatter<Vector3>
{
    public static readonly Vector3Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Vector3 value)
    {
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
        writer.WriteSingle(value.Z);
    }

    public override void Deserialize(ref TightPackReader reader, ref Vector3 value) =>
        value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}

public sealed class Vector4Formatter : TightPackFormatter<Vector4>
{
    public static readonly Vector4Formatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Vector4 value)
    {
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
        writer.WriteSingle(value.Z);
        writer.WriteSingle(value.W);
    }

    public override void Deserialize(ref TightPackReader reader, ref Vector4 value) =>
        value = new Vector4(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
}

public sealed class QuaternionFormatter : TightPackFormatter<Quaternion>
{
    public static readonly QuaternionFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Quaternion value)
    {
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
        writer.WriteSingle(value.Z);
        writer.WriteSingle(value.W);
    }

    public override void Deserialize(ref TightPackReader reader, ref Quaternion value) =>
        value = new Quaternion(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle()
        );
}
