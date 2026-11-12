using System.Buffers;

namespace TightPack;

/// <summary>
/// A growable <see cref="IBufferWriter{T}"/> backed by a pooled array. Instances are reused, so a
/// steady-state serialization allocates only the result array.
/// </summary>
public sealed class TightPackBufferWriter : IBufferWriter<byte>
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    // Per-thread stacks: a lock on a shared stack cost ~24ns per serialize, more than the encoding
    // of a small payload. A writer rented on one thread and returned on another still works - it
    // just joins that thread's stack.
    [ThreadStatic] private static Stack<TightPackBufferWriter>? t_free;

    private byte[] _buffer = [];
    private int _length;

    /// <summary>Rents a cleared writer. Return it with <see cref="Return"/>.</summary>
    public static TightPackBufferWriter Rent()
    {
        Stack<TightPackBufferWriter>? free = t_free;
        return free is { Count: > 0 } ? free.Pop() : new TightPackBufferWriter();
    }

    public static void Return(TightPackBufferWriter writer)
    {
        writer.Reset();
        if (writer._buffer.Length > 1 << 20)
        {
            // Do not pin a large serialization's buffer forever.
            Pool.Return(writer._buffer, clearArray: false);
            writer._buffer = [];
        }

        (t_free ??= new Stack<TightPackBufferWriter>()).Push(writer);
    }

    public int WrittenCount => _length;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public void Reset() => _length = 0;

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_length + count > _buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Advanced past the end of the buffer.");
        }

        _length += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_length);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_length);
    }

    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = 1;
        }

        int required = checked(_length + sizeHint);
        if (_buffer.Length >= required)
        {
            return;
        }

        int newSize = Math.Max(required, _buffer.Length == 0 ? 256 : _buffer.Length * 2);
        byte[] next = Pool.Rent(newSize);
        _buffer.AsSpan(0, _length).CopyTo(next);
        if (_buffer.Length != 0)
        {
            Pool.Return(_buffer, clearArray: false);
        }

        _buffer = next;
    }
}
