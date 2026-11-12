using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using static TightPack.TightPackCode;

namespace TightPack;

/// <summary>
/// Reads the TightPack wire format from a contiguous buffer.
/// </summary>
/// <remarks>
/// Every read is bounded: <see cref="GetSpan"/> throws rather than letting a length from the payload
/// index past the end, and lengths are always checked against the remaining bytes before they are
/// used to size an allocation. Nothing here reinterprets memory except <see cref="ReadBlittable{T}"/>,
/// which is generated only for structs with a proven padding-free layout.
/// </remarks>
public ref struct TightPackReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;
    private int _depth;
    private TightPackReaderState? _state;

    public TightPackReader(ReadOnlySpan<byte> buffer, TightPackReaderState? state = null)
    {
        _buffer = buffer;
        _position = 0;
        _depth = 0;
        _state = state;
    }

    public int Consumed => _position;

    public int Remaining => _buffer.Length - _position;

    public TightPackReaderState State => _state ??= new TightPackReaderState();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> GetSpan(int sizeHint)
    {
        if ((uint)sizeHint > (uint)(_buffer.Length - _position))
        {
            TightPackSerializationException.ThrowOutOfRange(sizeHint, _buffer.Length - _position);
        }

        ReadOnlySpan<byte> span = _buffer.Slice(_position, sizeHint);
        _position += sizeHint;
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int count)
    {
        if ((uint)count > (uint)(_buffer.Length - _position))
        {
            TightPackSerializationException.ThrowOutOfRange(count, _buffer.Length - _position);
        }

        _position += count;
    }

    // ----- headers -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadObjectHeader(out byte memberCount)
    {
        memberCount = GetSpan(1)[0];
        return memberCount != NullObject;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadCollectionHeader(out int length)
    {
        length = BinaryPrimitives.ReadInt32LittleEndian(GetSpan(4));
        if (length == NullCollection)
        {
            return false;
        }

        if (length < 0)
        {
            TightPackSerializationException.ThrowMessage($"Invalid collection length {length}.");
        }

        // Each element costs at least one byte, so a length larger than the remaining buffer is
        // corrupt data and must not be used to size an allocation.
        if (length > Remaining)
        {
            TightPackSerializationException.ThrowInvalidCollection(length, Remaining);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadUnionHeader(out ushort tag)
    {
        byte first = GetSpan(1)[0];
        if (first < WideTag)
        {
            tag = first;
            return true;
        }

        if (first == WideTag)
        {
            tag = BinaryPrimitives.ReadUInt16LittleEndian(GetSpan(2));
            return true;
        }

        tag = 0;
        return false;
    }

    // ----- scalars -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBoolean() => GetSpan(1)[0] != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte() => GetSpan(1)[0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadSByte() => (sbyte)GetSpan(1)[0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public char ReadChar() => (char)BinaryPrimitives.ReadUInt16LittleEndian(GetSpan(2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(GetSpan(2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(GetSpan(2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(GetSpan(4));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(GetSpan(4));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(GetSpan(8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(GetSpan(8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle() => BinaryPrimitives.ReadSingleLittleEndian(GetSpan(4));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(GetSpan(8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Half ReadHalf() => BitConverter.Int16BitsToHalf(BinaryPrimitives.ReadInt16LittleEndian(GetSpan(2)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Int128 ReadInt128()
    {
        ReadOnlySpan<byte> span = GetSpan(16);
        ulong low = BinaryPrimitives.ReadUInt64LittleEndian(span);
        ulong high = BinaryPrimitives.ReadUInt64LittleEndian(span[8..]);
        return new Int128(high, low);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt128 ReadUInt128()
    {
        ReadOnlySpan<byte> span = GetSpan(16);
        ulong low = BinaryPrimitives.ReadUInt64LittleEndian(span);
        ulong high = BinaryPrimitives.ReadUInt64LittleEndian(span[8..]);
        return new UInt128(high, low);
    }

    public decimal ReadDecimal()
    {
        ReadOnlySpan<byte> span = GetSpan(16);
        int lo = BinaryPrimitives.ReadInt32LittleEndian(span);
        int mid = BinaryPrimitives.ReadInt32LittleEndian(span[4..]);
        int hi = BinaryPrimitives.ReadInt32LittleEndian(span[8..]);
        int flags = BinaryPrimitives.ReadInt32LittleEndian(span[12..]);
        return new decimal(lo, mid, hi, (flags & unchecked((int)0x80000000)) != 0, (byte)((flags >> 16) & 0xFF));
    }

    public Guid ReadGuid() => new(GetSpan(16));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime() => DateTime.FromBinary(ReadInt64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTimeOffset ReadDateTimeOffset()
    {
        long utcTicks = ReadInt64();
        int offsetMinutes = ReadInt32();
        if (offsetMinutes is < -14 * 60 or > 14 * 60)
        {
            TightPackSerializationException.ThrowMessage($"DateTimeOffset offset {offsetMinutes} minutes is out of range.");
        }

        try
        {
            return new DateTimeOffset(utcTicks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        }
        catch (ArgumentException e)
        {
            throw new TightPackSerializationException("DateTimeOffset payload is out of range.", e);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ReadTimeSpan() => new(ReadInt64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateOnly ReadDateOnly() => DateOnly.FromDayNumber(ReadInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeOnly ReadTimeOnly() => new(ReadInt64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rune ReadRune() => new(ReadInt32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEnum ReadEnum<TEnum>()
        where TEnum : struct, Enum
    {
        TEnum value = default;
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1:
            {
                byte raw = ReadByte();
                return Unsafe.As<byte, TEnum>(ref raw);
            }
            case 2:
            {
                ushort raw = ReadUInt16();
                return Unsafe.As<ushort, TEnum>(ref raw);
            }
            case 4:
            {
                uint raw = ReadUInt32();
                return Unsafe.As<uint, TEnum>(ref raw);
            }
            case 8:
            {
                ulong raw = ReadUInt64();
                return Unsafe.As<ulong, TEnum>(ref raw);
            }
            default:
                TightPackSerializationException.ThrowMessage($"Unsupported enum size for {typeof(TEnum).Name}.");
                return value;
        }
    }

    // ----- strings -----

    public string? ReadString()
    {
        if (!TryReadCollectionHeader(out int byteLength))
        {
            return null;
        }

        if (byteLength == 0)
        {
            return string.Empty;
        }

        ReadOnlySpan<byte> utf8 = GetSpan(byteLength);
        return Encoding.UTF8.GetString(utf8);
    }

    /// <summary>Reads a UTF-8 payload without decoding it.</summary>
    public byte[]? ReadUtf8Bytes()
    {
        if (!TryReadCollectionHeader(out int byteLength))
        {
            return null;
        }

        return GetSpan(byteLength).ToArray();
    }

    /// <summary>Reads a length-prefixed byte payload.</summary>
    public byte[]? ReadByteArray()
    {
        if (!TryReadCollectionHeader(out int byteLength))
        {
            return null;
        }

        if (byteLength == 0)
        {
            return [];
        }

        return GetSpan(byteLength).ToArray();
    }

    /// <summary>Reads a length-prefixed byte payload into <paramref name="destination"/>.</summary>
    /// <exception cref="TightPackSerializationException">The payload does not fit the destination.</exception>
    public void ReadBytes(Span<byte> destination)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(GetSpan(4));

        if (length < 0)
        {
            TightPackSerializationException.ThrowMessage($"Invalid byte payload length {length}.");
        }

        if (length > destination.Length)
        {
            TightPackSerializationException.ThrowInvalidCollection(length, destination.Length);
        }

        if (length != 0)
        {
            GetSpan(length).CopyTo(destination);
        }
    }

    // ----- blitting (only for generator-proven padding-free structs) -----

    /// <summary>
    /// Reads the raw bytes of <typeparamref name="T"/>, which the generator proved has no padding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ReadBlittable<T>()
        where T : unmanaged
    {
        int size = TightPackBlit.SizeOf<T>();
        ReadOnlySpan<byte> span = GetSpan(size);
        return MemoryMarshal.Read<T>(span);
    }

    public T[]? ReadBlittableArray<T>()
        where T : unmanaged
    {
        if (!TryReadCollectionHeader(out int length))
        {
            return null;
        }

        if (length == 0)
        {
            return [];
        }

        int byteCount = checked(length * TightPackBlit.SizeOf<T>());
        ReadOnlySpan<byte> source = GetSpan(byteCount);
        var values = new T[length];
        source.CopyTo(TightPackSpan.AsBytes(values.AsSpan()));
        return values;
    }

    /// <summary>Reads a padding-free struct's raw image of <paramref name="size"/> bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ReadBlittable<T>(int size)
        where T : unmanaged
    {
        ReadOnlySpan<byte> span = GetSpan(size);
        return MemoryMarshal.Read<T>(span);
    }

    // ----- generated-type dispatch -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPackable<T>(ref T? value)
        where T : ITightPackable<T>
    {
        EnsureDepth(typeof(T));
        T.Deserialize(ref this, ref value!);
        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadValue<T>(ref T? value)
    {
        EnsureDepth(typeof(T));
        TightPackFormatterProvider.GetFormatter<T>().Deserialize(ref this, ref value);
        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadValueWithFormatter<T>(TightPackFormatter<T> formatter, ref T? value)
    {
        EnsureDepth(typeof(T));
        formatter.Deserialize(ref this, ref value);
        _depth--;
    }

    public T?[]? ReadPackableArray<T>()
        where T : ITightPackable<T>
    {
        if (!TryReadCollectionHeader(out int length))
        {
            return null;
        }

        if (length == 0)
        {
            return [];
        }

        var values = new T?[length];
        EnsureDepth(typeof(T));
        for (int i = 0; i < length; i++)
        {
            T.Deserialize(ref this, ref values[i]!);
        }

        _depth--;
        return values;
    }

    public List<T>? ReadPackableList<T>()
        where T : ITightPackable<T>
    {
        if (!TryReadCollectionHeader(out int length))
        {
            return null;
        }

        var values = new List<T>(length);
        EnsureDepth(typeof(T));
        for (int i = 0; i < length; i++)
        {
            T value = default!;
            T.Deserialize(ref this, ref value);
            values.Add(value);
        }

        _depth--;
        return values;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureDepth(Type type)
    {
        if (++_depth >= DepthLimit)
        {
            TightPackSerializationException.ThrowDepthLimit(type);
        }
    }

    // ----- varint -----

    public uint ReadVarUInt32()
    {
        uint result = 0;
        int shift = 0;
        while (true)
        {
            if (shift == 35)
            {
                TightPackSerializationException.ThrowMessage("Variable-length integer is longer than 5 bytes.");
            }

            byte b = GetSpan(1)[0];
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }
    }

    // ----- circular reference support -----

    /// <summary>
    /// Reads the header of a circular-reference object. Returns false for a null payload; sets
    /// <paramref name="existing"/> and <paramref name="isReference"/> for a back-reference, which
    /// the caller must use instead of reading members.
    /// </summary>
    public bool TryReadObjectReferenceHeader(out byte memberCount, out object? existing, out bool isReference)
    {
        byte header = GetSpan(1)[0];
        memberCount = 0;
        existing = null;
        isReference = false;

        if (header == NullObject)
        {
            return false;
        }

        if (header == ReferenceId)
        {
            uint id = ReadVarUInt32();
            if (_state is null || !_state.TryGetObject(id, out existing))
            {
                TightPackSerializationException.ThrowMessage(
                    $"Reference id {id} was not written earlier in this stream."
                );
            }

            isReference = true;
            return true;
        }

        // The writer follows a fresh header with the id it assigned; ids are implicit in
        // registration order here, so the value is consumed and discarded.
        ReadVarUInt32();
        memberCount = header;
        return true;
    }
}
