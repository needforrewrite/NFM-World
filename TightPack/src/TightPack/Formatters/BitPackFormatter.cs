namespace TightPack.Formatters;

/// <summary>
/// Packs a <see cref="bool"/> array one bit per element: the count, then 32 bits per 4-byte group
/// with the unused tail bits left clear.
/// </summary>
/// <remarks>
/// This is a wire format of its own, not the one <see cref="ArrayFormatter{T}"/> writes for
/// <c>bool[]</c>, so a member has to opt in with <c>[TightPackCustomFormatter]</c> and the choice
/// becomes part of that member's format.
/// </remarks>
public sealed class BitPackFormatter : TightPackFormatter<bool[]>
{
    public static readonly BitPackFormatter Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in bool[]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        if (value.Length == 0)
        {
            return;
        }

        int packed = 0;
        int bit = 0;
        foreach (bool item in value)
        {
            if (item)
            {
                packed |= 1 << bit;
            }

            if (++bit == 32)
            {
                writer.WriteInt32(packed);
                packed = 0;
                bit = 0;
            }
        }

        if (bit != 0)
        {
            writer.WriteInt32(packed);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref bool[]? value)
    {
        if (!reader.TryReadPackedCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        if (length == 0)
        {
            value = [];
            return;
        }

        // Eight elements share one byte, so the count cannot be bounded by the remaining buffer.
        // The group count can: it must fit, or the payload is truncated.
        int groups = (length + 31) / 32;
        if (reader.Remaining < groups * sizeof(int))
        {
            TightPackSerializationException.ThrowInvalidCollection(groups * sizeof(int), reader.Remaining);
        }

        var result = new bool[length];
        int packed = 0;
        int bit = 0;
        for (int i = 0; i < length; i++)
        {
            if (bit == 0)
            {
                packed = reader.ReadInt32();
            }

            result[i] = (packed & (1 << bit)) != 0;
            if (++bit == 32)
            {
                bit = 0;
            }
        }

        value = result;
    }
}
