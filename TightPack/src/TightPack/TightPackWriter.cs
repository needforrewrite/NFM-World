using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using static TightPack.TightPackCode;

namespace TightPack;

/// <summary>
/// Writes the TightPack wire format into an <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// Every primitive is written with a fixed-width little-endian encoding, never as padding or as part
/// of a raw struct image, except for structs the generator has proven to be padding-free (see
/// <see cref="WriteBlittable{T}"/>). All buffer access goes through <see cref="Span{T}"/>, so a
/// miscounted length throws instead of corrupting memory.
/// </remarks>
public ref struct TightPackWriter<TBufferWriter>
    where TBufferWriter : IBufferWriter<byte>
{
    private ref TBufferWriter _writer;
    private Span<byte> _buffer; // remaining writable bytes of the current span
    private int _advanced; // bytes claimed in _buffer but not yet reported to _writer
    private int _written;
    private int _depth;
    private TightPackWriterState? _state;

    public TightPackWriter(ref TBufferWriter writer, TightPackWriterState? state = null, int depth = 0)
    {
        _writer = ref writer;
        _buffer = default;
        _advanced = 0;
        _written = 0;
        _depth = depth;
        // Only a CircularReference type touches it, so it is created lazily; otherwise every
        // serialization would allocate one for nothing.
        _state = state;
    }

    /// <summary>Total bytes handed to the underlying writer so far.</summary>
    public int WrittenCount => _written;

    public TightPackWriterState State => _state ??= new TightPackWriterState();

    /// <summary>Current object nesting depth, carried into a nested writer.</summary>
    public int Depth => _depth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> GetSpan(int sizeHint)
    {
        if (_buffer.Length < sizeHint)
        {
            Grow(sizeHint);
        }

        return _buffer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        Commit();
        Span<byte> span = _writer.GetSpan(sizeHint);
        if (span.Length < sizeHint)
        {
            TightPackSerializationException.ThrowBufferTooSmall(sizeHint, span.Length);
        }

        _buffer = span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance(int count)
    {
        _buffer = _buffer[count..];
        _advanced += count;
        _written += count;
    }

    private void Commit()
    {
        if (_advanced != 0)
        {
            _writer.Advance(_advanced);
            _advanced = 0;
        }
    }

    /// <summary>Reports everything written so far to the underlying writer.</summary>
    public void Flush()
    {
        Commit();
        _buffer = default;
    }

    // ----- headers -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteObjectHeader(byte memberCount)
    {
        if (memberCount >= ReferenceId)
        {
            TightPackSerializationException.ThrowMessage(
                $"A TightPackable type may declare at most {ReferenceId - 1} members but this one declares {memberCount}."
            );
        }

        GetSpan(1)[0] = memberCount;
        Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullObjectHeader()
    {
        GetSpan(1)[0] = NullObject;
        Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCollectionHeader(int length)
    {
        if (length < 0)
        {
            TightPackSerializationException.ThrowMessage($"Collection length must be non-negative, was {length}.");
        }

        BinaryPrimitives.WriteInt32LittleEndian(GetSpan(4), length);
        Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullCollectionHeader()
    {
        BinaryPrimitives.WriteInt32LittleEndian(GetSpan(4), NullCollection);
        Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUnionHeader(ushort tag)
    {
        if (tag < WideTag)
        {
            GetSpan(1)[0] = (byte)tag;
            Advance(1);
            return;
        }

        Span<byte> span = GetSpan(3);
        span[0] = WideTag;
        BinaryPrimitives.WriteUInt16LittleEndian(span[1..], tag);
        Advance(3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullUnionHeader() => WriteNullObjectHeader();

    // ----- scalars -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value)
    {
        GetSpan(1)[0] = value ? (byte)1 : (byte)0;
        Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value)
    {
        GetSpan(1)[0] = value;
        Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSByte(sbyte value)
    {
        GetSpan(1)[0] = (byte)value;
        Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChar(char value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(GetSpan(2), value);
        Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(GetSpan(2), value);
        Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(GetSpan(2), value);
        Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(GetSpan(4), value);
        Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(GetSpan(4), value);
        Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(GetSpan(8), value);
        Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(GetSpan(8), value);
        Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSingle(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(GetSpan(4), value);
        Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        BinaryPrimitives.WriteDoubleLittleEndian(GetSpan(8), value);
        Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHalf(Half value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(GetSpan(2), BitConverter.HalfToInt16Bits(value));
        Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt128(Int128 value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(GetSpan(16), (long)(ulong)value);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer[8..], (long)(ulong)(value >> 64));
        Advance(16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt128(UInt128 value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(GetSpan(16), (ulong)value);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer[8..], (ulong)(value >> 64));
        Advance(16);
    }

    public void WriteDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        Span<byte> span = GetSpan(16);
        BinaryPrimitives.WriteInt32LittleEndian(span, bits[0]);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], bits[1]);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], bits[2]);
        BinaryPrimitives.WriteInt32LittleEndian(span[12..], bits[3]);
        Advance(16);
    }

    public void WriteGuid(Guid value)
    {
        Span<byte> span = GetSpan(16);
        value.TryWriteBytes(span);
        Advance(16);
    }

    /// <summary>
    /// Writes the raw <see cref="DateTime"/> representation through <see cref="DateTime.ToBinary"/>,
    /// which preserves ticks and <see cref="DateTimeKind"/> in eight bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value) => WriteInt64(value.ToBinary());

    /// <summary>
    /// Writes <c>UtcTicks</c> and <c>TotalOffsetMinutes</c>: both are the .NET 10 accessors, and
    /// neither exposes the struct's internal padding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTimeOffset(DateTimeOffset value)
    {
        WriteInt64(value.UtcTicks);
        WriteInt32(value.TotalOffsetMinutes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTimeSpan(TimeSpan value) => WriteInt64(value.Ticks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateOnly(DateOnly value) => WriteInt32(value.DayNumber);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTimeOnly(TimeOnly value) => WriteInt64(value.Ticks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRune(Rune value) => WriteInt32(value.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1: WriteByte((byte)Unsafe.As<TEnum, byte>(ref value)); break;
            case 2: WriteUInt16((ushort)Unsafe.As<TEnum, ushort>(ref value)); break;
            case 4: WriteUInt32((uint)Unsafe.As<TEnum, uint>(ref value)); break;
            case 8: WriteUInt64((ulong)Unsafe.As<TEnum, ulong>(ref value)); break;
            default: TightPackSerializationException.ThrowMessage($"Unsupported enum size for {typeof(TEnum).Name}."); break;
        }
    }

    // ----- strings -----

    /// <summary>
    /// Writes a UTF-8 string with a four-byte length prefix, or -1 for <see langword="null"/>.
    /// </summary>
    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteNullCollectionHeader();
            return;
        }

        if (value.Length == 0)
        {
            WriteCollectionHeader(0);
            return;
        }

        if (value.Length > int.MaxValue / 3)
        {
            TightPackSerializationException.ThrowMessage("String is too large to serialize.");
        }

        int maxBytes = value.Length * 3;
        Span<byte> span = GetSpan(4 + maxBytes);
        Span<byte> payload = span.Slice(4, maxBytes);

        var status = Utf8.FromUtf16(value, payload, out _, out int written, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            TightPackSerializationException.ThrowMessage(
                $"String contains an unpaired surrogate and cannot be encoded as UTF-8 (status {status})."
            );
        }

        BinaryPrimitives.WriteInt32LittleEndian(span, written);
        Advance(4 + written);
    }

    /// <summary>Writes a pre-encoded UTF-8 payload with a four-byte length prefix.</summary>
    public void WriteUtf8(scoped ReadOnlySpan<byte> utf8)
    {
        WriteCollectionHeader(utf8.Length);
        if (utf8.IsEmpty)
        {
            return;
        }

        utf8.CopyTo(GetSpan(utf8.Length));
        Advance(utf8.Length);
    }

    /// <summary>Writes raw bytes with no header.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(scoped ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        bytes.CopyTo(GetSpan(bytes.Length));
        Advance(bytes.Length);
    }

    // ----- blitting (only for generator-proven padding-free structs) -----

    /// <summary>
    /// Writes the raw bytes of <typeparamref name="T"/>. Callers must have proven that
    /// <typeparamref name="T"/> has no padding on any supported architecture; the generated static
    /// constructor calls <see cref="TightPackBlit.VerifySize{T}"/> to make that proof a runtime fact.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBlittable<T>(in T value)
        where T : unmanaged
    {
        int size = TightPackBlit.SizeOf<T>();
        Span<byte> span = GetSpan(size);
        MemoryMarshal.Write(span, in value);
        Advance(size);
    }

    /// <summary>
    /// Writes a padding-free struct's raw image. <paramref name="size"/> is the analyzer's proof;
    /// the generated static constructor already asserted it against the runtime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBlittable<T>(in T value, int size)
        where T : unmanaged
    {
        Span<byte> span = GetSpan(size);
        MemoryMarshal.Write(span, in value);
        Advance(size);
    }

    /// <summary>Writes an array of padding-free structs as one contiguous block.</summary>
    public void WriteBlittableArray<T>(T[]? values)
        where T : unmanaged
    {
        if (values is null)
        {
            WriteNullCollectionHeader();
            return;
        }

        WriteCollectionHeader(values.Length);
        if (values.Length == 0)
        {
            return;
        }

        Span<byte> bytes = TightPackSpan.AsBytes(values.AsSpan());
        bytes.CopyTo(GetSpan(bytes.Length));
        Advance(bytes.Length);
    }

    // ----- generated-type dispatch -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePackable<T>(in T? value)
        where T : ITightPackable<T>
    {
        EnsureDepth(typeof(T));
        T.Serialize(ref this, in value!);
        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteValue<T>(in T? value)
    {
        EnsureDepth(typeof(T));
        TightPackFormatterProvider.GetFormatter<T>().Serialize(ref this, in value);
        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteValueWithFormatter<T>(TightPackFormatter<T> formatter, in T? value)
    {
        EnsureDepth(typeof(T));
        formatter.Serialize(ref this, in value);
        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePackableArray<T>(T?[]? values)
        where T : ITightPackable<T>
    {
        if (values is null)
        {
            WriteNullCollectionHeader();
            return;
        }

        WriteCollectionHeader(values.Length);
        EnsureDepth(typeof(T));
        for (int i = 0; i < values.Length; i++)
        {
            T value = values[i]!;
            T.Serialize(ref this, in value);
        }

        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePackableList<T>(IList<T>? values)
        where T : ITightPackable<T>
    {
        if (values is null)
        {
            WriteNullCollectionHeader();
            return;
        }

        WriteCollectionHeader(values.Count);
        EnsureDepth(typeof(T));
        for (int i = 0; i < values.Count; i++)
        {
            T value = values[i]!;
            T.Serialize(ref this, in value);
        }

        _depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureDepth(Type type)
    {
        if (++_depth >= DepthLimit)
        {
            TightPackSerializationException.ThrowDepthLimit(type);
        }
    }

    // ----- varint (version tolerant member lengths) -----

    public void WriteVarUInt32(uint value)
    {
        Span<byte> span = GetSpan(5);
        int i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[i++] = (byte)value;
        Advance(i);
    }

    // ----- circular reference support -----

    /// <summary>
    /// Writes the header of a circular-reference object. Returns <c>false</c> when the object was
    /// already written and only a back-reference was emitted, so the caller must stop.
    /// </summary>
    public bool WriteObjectReference(byte memberCount, object value)
    {
        if (_state is not null && _state.TryGetReference(value, out uint id))
        {
            GetSpan(1)[0] = ReferenceId;
            Advance(1);
            WriteVarUInt32(id);
            return false;
        }

        uint assigned = State.AddReference(value);
        WriteObjectHeader(memberCount);
        WriteVarUInt32(assigned);
        return true;
    }
}
