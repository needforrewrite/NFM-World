// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;
using Lua;
using Lua.Runtime;
using MemoryPack;
using MemoryPack.Formatters;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaVisible]
public class UnlimitedArray<T> : IList<T>, IReadOnlyList<T>, IMemoryPackable<UnlimitedArray<T>>, ILuaUserData
{
    private protected T[] _items = [];
    private protected int _size = 0;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _size;
    }

    public bool IsReadOnly
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    public virtual T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index < 0)
                ThrowArgumentOutOfRange(index);
            if (index < _size)
                return _items[index];
            return default!;

            static void ThrowArgumentOutOfRange(int i)
            {
                throw new ArgumentOutOfRangeException(nameof(i), i, "Index must be non-negative.");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (index < 0)
                ThrowArgumentOutOfRange(index);
            if (index >= _items.Length)
            {
                Grow(index + 1);
            }
            if (index >= _size)
            {
                _size = index + 1;
            }
            _items[index] = value;

            static void ThrowArgumentOutOfRange(int i)
            {
                throw new ArgumentOutOfRangeException(nameof(i), i, "Index must be non-negative.");
            }
        }
    }
    
    public struct Enumerator(UnlimitedArray<T> array) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => array[_index];
        }

        object? IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => array[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _index++;
            return _index < array._size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnlimitedArray()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnlimitedArray(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 0);
        
        if (capacity <= 4)
        {
            _items = new T[4];
        }
        else
        {
            _items = new T[capacity];
        }
    }
    
    
    public UnlimitedArray(IEnumerable<T> items) : this(items.TryGetNonEnumeratedCount(out var count) ? count : 0)
    {
        foreach (var item in items)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            Add(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity cannot be less than zero.");
        }
        if (_items.Length < capacity)
        {
            Grow(capacity);
        }

        return _items.Length;
    }

    // Adds the given object to the end of this list. The size of the list is
    // increased by one. If required, the capacity of the list is doubled
    // before adding the new element.
    //
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(T item)
    {
        var array = _items;
        var size = _size;
        if ((uint)size < (uint)array.Length)
        {
            _size = size + 1;
            array[size] = item;
        }
        else
        {
            AddWithResize(item);
        }
    }

    // Non-inline from List.Add to improve its code quality as uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        var size = _size;
        Grow(size + 1);
        _size = size + 1;
        _items[size] = item;
    }

    internal void Grow(int capacity)
    {
        capacity = GetNewCapacity(capacity);
        Array.Resize(ref _items, capacity);
    }
    
    internal void GrowForInsertion(int indexToInsert, int insertionCount = 1)
    {
        var requiredCapacity = checked(_size + insertionCount);
        var newCapacity = GetNewCapacity(requiredCapacity);

        var newItems = new T[newCapacity];
        if (indexToInsert != 0)
        {
            Array.Copy(_items, newItems, length: indexToInsert);
        }

        if (_size != indexToInsert)
        {
            Array.Copy(_items, indexToInsert, newItems, indexToInsert + insertionCount, _size - indexToInsert);
        }

        _items = newItems;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNewCapacity(int capacity)
    {
        var newCapacity = _items.Length == 0 ? 4 : 2 * _items.Length;

        // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
        // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
        if ((uint)newCapacity > Array.MaxLength) newCapacity = Array.MaxLength;

        // If the computed capacity is still less than specified, set to the original argument.
        // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
        if (newCapacity < capacity) newCapacity = capacity;

        return newCapacity;
    }
    
    public virtual void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var size = _size;
            _size = 0;
            if (size > 0)
            {
                Array.Clear(_items, 0, size); // Clear the elements so that the gc can reclaim the references.
            }
        }
        else
        {
            _size = 0;
        }
    }

    public bool Contains(T item)
    {
        return _size != 0 && IndexOf(item) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Array.Copy(_items, 0, array, arrayIndex, _size);
    }

    public virtual bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }

        return false;
    }

    public int IndexOf(T item)
        => Array.IndexOf(_items, item, 0, _size);

    public virtual void Insert(int index, T item)
    {
        // Note that insertions at the end are legal.
        if ((uint)index > (uint)_size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index cannot be greater than the size of the collection.");
        }
        if (_size == _items.Length)
        {
            GrowForInsertion(index, 1);
        }
        else if (index < _size)
        {
            Array.Copy(_items, index, _items, index + 1, _size - index);
        }
        _items[index] = item;
    }

    public virtual void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_size)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be less than the size of the collection.");
        }
        _size--;
        if (index < _size)
        {
            Array.Copy(_items, index + 1, _items, index, _size - index);
        }
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _items[_size] = default!;
        }
    }

    public T[] ToArray()
    {
        return _items.AsSpan(0, _size).ToArray();
    }

    public static implicit operator Span<T>(UnlimitedArray<T> array) => array._items.AsSpan(0, array._size);
    public static implicit operator ReadOnlySpan<T>(UnlimitedArray<T> array) => array._items.AsSpan(0, array._size);

    public virtual void Sort(Comparison<T> compareFunc)
    {
        _items.AsSpan(0, _size).Sort(compareFunc);
    }

    internal Span<T> GetSpan()
    {
        return _items.AsSpan();
    }

    // ------------------------------------------------------------------
    // ILuaUserData — table-like behaviour via metatable
    // ------------------------------------------------------------------

    LuaTable? ILuaUserData.Metatable
    {
        get
        {
            if (field == null)
            {
                field = SharedMetatable;
            }
            return field;
        }
        set;
    }

    /// <summary>Shared metatable for all <see cref="UnlimitedArray{T}"/> instances of the same T.</summary>
    private static LuaTable SharedMetatable
    {
        get
        {
            if (field != null)
                return field;

            var mt = new LuaTable(0, 3);
            mt[Metamethods.Index] = new LuaFunction("__index", IndexMetamethodImpl);
            mt[Metamethods.NewIndex] = new LuaFunction("__newindex", NewIndexMetamethodImpl);
            mt[Metamethods.Len] = new LuaFunction("__len", LenMetamethodImpl);

            Interlocked.CompareExchange(ref field, mt, null);
            return field!;
        }
    }

    private static ValueTask<int> IndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        var key = context.GetArgument(1);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Count)
            {
                return new(context.Return(LuaValue.FromObject(arr[index]!)));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static ValueTask<int> NewIndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        var key = context.GetArgument(1);
        var value = context.GetArgument(2);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if (!value.TryRead<T>(out var typedValue))
            {
                // Fallback: try number → T conversion for common numeric types
                typedValue = ConvertLuaValue(value);
            }
            arr[index] = typedValue;
        }

        return new(context.Return());
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        return new(context.Return((double)arr.Count));
    }

    /// <summary>
    /// Checks whether a Lua number represents a valid 1-based array index,
    /// and converts it to a 0-based C# index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLuaIndex(double num, out int csharpIndex)
    {
        // Must be a finite integer ≥ 1 (Lua arrays are 1-indexed)
        if (double.IsFinite(num) && num >= 1.0 && num == Math.Floor(num) && num <= int.MaxValue)
        {
            csharpIndex = (int)num - 1;
            return true;
        }

        csharpIndex = 0;
        return false;
    }

    /// <summary>Converts a <see cref="LuaValue"/> to <typeparamref name="T"/> with flexible coercion.</summary>
    private static T ConvertLuaValue(LuaValue value)
    {
        // Let LuaValue's own conversion handle it (supports double, string, bool, etc.)
        if (value.TryRead<T>(out var result))
            return result;

        // For numeric types, try reading as double and converting
        if (value.TryRead<double>(out var num))
        {
            var targetType = typeof(T);
            if (targetType == typeof(float))  { var v = (float)num;  return Unsafe.As<float, T>(ref v); }
            if (targetType == typeof(int))    { var v = (int)num;    return Unsafe.As<int, T>(ref v); }
            if (targetType == typeof(long))   { var v = (long)num;   return Unsafe.As<long, T>(ref v); }
            if (targetType == typeof(uint))   { var v = (uint)num;   return Unsafe.As<uint, T>(ref v); }
            if (targetType == typeof(ulong))  { var v = (ulong)num;  return Unsafe.As<ulong, T>(ref v); }
            if (targetType == typeof(short))  { var v = (short)num;  return Unsafe.As<short, T>(ref v); }
            if (targetType == typeof(ushort)) { var v = (ushort)num; return Unsafe.As<ushort, T>(ref v); }
            if (targetType == typeof(byte))   { var v = (byte)num;   return Unsafe.As<byte, T>(ref v); }
            if (targetType == typeof(sbyte))  { var v = (sbyte)num;  return Unsafe.As<sbyte, T>(ref v); }
            if (targetType == typeof(double)) { return Unsafe.As<double, T>(ref num); }
        }

        return default!;
    }

    public static void RegisterFormatter()
    {
        MemoryPackFormatterProvider.Register(UnlimitedArrayFormatter<T>.Instance);
    }

    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref UnlimitedArray<T>? value) where TBufferWriter : IBufferWriter<byte>
    {
        UnlimitedArrayFormatter<T>.Instance.Serialize(ref writer, ref value);
    }

    public static void Deserialize(ref MemoryPackReader reader, scoped ref UnlimitedArray<T>? value)
    {
        UnlimitedArrayFormatter<T>.Instance.Deserialize(ref reader, ref value);
    }

    internal static UnlimitedArray<T> MarshalFrom(T[] arr)
    {
        return new UnlimitedArray<T>
        {
            _items = arr
        };
    }
}
    
public sealed class UnlimitedArrayFormatter<T> : MemoryPackFormatter<UnlimitedArray<T>?>
{
    public static readonly MemoryPackFormatter<UnlimitedArray<T>?> Instance = new UnlimitedArrayFormatter<T>();

    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref UnlimitedArray<T>? value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteSpan(value.GetSpan()!);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref UnlimitedArray<T>? value)
    {
        if (reader.PeekIsNull())
        {
            reader.Advance(1); // skip null block
            value = null;
            return;
        }

        T[] arr = [];
        reader.ReadArray(ref arr!);
        value = UnlimitedArray<T>.MarshalFrom(arr);
    }
}
