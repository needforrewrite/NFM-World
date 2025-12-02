// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Runtime.CompilerServices;

namespace NFMWorld.Util;

public class UnlimitedArray<T> : IList<T>
{
    private T[] _items = [];
    private int _size = 0;

    public int Count => _size;
    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
            if (index < _size)
                return _items[index];
            return default!;
        }
        set
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
            if (index >= _items.Length)
            {
                Grow(index + 1);
            }
            if (index >= _size)
            {
                _size = index + 1;
            }
            _items[index] = value;
        }
    }
    
    public struct Enumerator(UnlimitedArray<T> array) : IEnumerator<T>
    {
        private int _index = -1;

        public T Current => array[_index];

        object? IEnumerator.Current => array[_index];
        
        public bool MoveNext()
        {
            _index++;
            return _index < array._size;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }

    public UnlimitedArray()
    {
    }

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

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
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
    public void Add(T item)
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
    
    public void Clear()
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

    public bool Remove(T item)
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

    public void Insert(int index, T item)
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

    public void RemoveAt(int index)
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

    public void Sort(Comparison<T> compareFunc)
    {
        _items.AsSpan(0, _size).Sort(compareFunc);
    }
}