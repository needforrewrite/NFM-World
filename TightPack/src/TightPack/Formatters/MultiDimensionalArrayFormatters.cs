namespace TightPack.Formatters;

// Multi-dimensional arrays carry their dimensions before the elements, because a flat element count
// is not enough to rebuild the shape. Each is written element by element: the array's memory image
// is contiguous, but only for a padding-free element type, and proving that per instantiation is
// not worth it for a type this rare.

public sealed class TwoDimensionalArrayFormatter<T> : TightPackFormatter<T[,]?>
{
    public static readonly TwoDimensionalArrayFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T[,]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        int rows = value.GetLength(0);
        int columns = value.GetLength(1);
        writer.WriteCollectionHeader(rows * columns);
        writer.WriteInt32(rows);
        writer.WriteInt32(columns);
        MultiDimensionalArrays.Write(ref writer, value.GetLength(0), value.GetLength(1), 0, 0, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref T[,]? value)
    {
        if (!reader.TryReadCollectionHeader(out _))
        {
            value = null;
            return;
        }

        int rows = reader.ReadInt32();
        int columns = reader.ReadInt32();
        if (rows < 0 || columns < 0)
        {
            TightPackSerializationException.ThrowMessage($"Invalid array dimensions {rows}x{columns}.");
        }

        var result = new T[rows, columns];
        MultiDimensionalArrays.Read(ref reader, result);
        value = result;
    }
}

public sealed class ThreeDimensionalArrayFormatter<T> : TightPackFormatter<T[,,]?>
{
    public static readonly ThreeDimensionalArrayFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T[,,]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        writer.WriteInt32(value.GetLength(0));
        writer.WriteInt32(value.GetLength(1));
        writer.WriteInt32(value.GetLength(2));

        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        foreach (T item in value)
        {
            T? current = item;
            formatter.Serialize(ref writer, in current);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref T[,,]? value)
    {
        if (!reader.TryReadCollectionHeader(out _))
        {
            value = null;
            return;
        }

        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        int z = reader.ReadInt32();
        if (x < 0 || y < 0 || z < 0)
        {
            TightPackSerializationException.ThrowMessage($"Invalid array dimensions {x}x{y}x{z}.");
        }

        var result = new T[x, y, z];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        int i = 0;
        int j = 0;
        int k = 0;
        for (int index = 0; index < result.Length; index++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result[i, j, k] = item!;

            if (++k < z)
            {
                continue;
            }

            k = 0;
            if (++j < y)
            {
                continue;
            }

            j = 0;
            i++;
        }

        value = result;
    }
}

public sealed class FourDimensionalArrayFormatter<T> : TightPackFormatter<T[,,,]?>
{
    public static readonly FourDimensionalArrayFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in T[,,,]? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Length);
        writer.WriteInt32(value.GetLength(0));
        writer.WriteInt32(value.GetLength(1));
        writer.WriteInt32(value.GetLength(2));
        writer.WriteInt32(value.GetLength(3));

        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        foreach (T item in value)
        {
            T? current = item;
            formatter.Serialize(ref writer, in current);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref T[,,,]? value)
    {
        if (!reader.TryReadCollectionHeader(out _))
        {
            value = null;
            return;
        }

        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        int z = reader.ReadInt32();
        int w = reader.ReadInt32();
        if (x < 0 || y < 0 || z < 0 || w < 0)
        {
            TightPackSerializationException.ThrowMessage($"Invalid array dimensions {x}x{y}x{z}x{w}.");
        }

        var result = new T[x, y, z, w];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        var indices = new int[4];
        for (int index = 0; index < result.Length; index++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result[indices[0], indices[1], indices[2], indices[3]] = item!;

            for (int axis = 3; axis >= 0; axis--)
            {
                int length = axis switch { 0 => x, 1 => y, 2 => z, _ => w };
                if (++indices[axis] < length)
                {
                    break;
                }

                indices[axis] = 0;
            }
        }

        value = result;
    }
}

internal static class MultiDimensionalArrays
{
    public static void Write<T, TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        int rows,
        int columns,
        int rowStart,
        int columnStart,
        T[,] value
    )
        where TBufferWriter : System.Buffers.IBufferWriter<byte>
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = rowStart; i < rows; i++)
        {
            for (int j = columnStart; j < columns; j++)
            {
                T? item = value[i, j];
                formatter.Serialize(ref writer, in item);
            }
        }
    }

    public static void Read<T>(ref TightPackReader reader, T[,] value)
    {
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        int rows = value.GetLength(0);
        int columns = value.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                T? item = default;
                formatter.Deserialize(ref reader, ref item);
                value[i, j] = item!;
            }
        }
    }
}
