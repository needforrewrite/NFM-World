using System.Collections;
using System.Runtime.InteropServices;
using ObservableCollections;

namespace WorldXaml.ObservableCollections;

public partial class NonSynchronizedObservableList<T> : IList<T>, IReadOnlyNonSynchronizedObservableList<T>
{
    readonly List<T> list;

    public NonSynchronizedObservableList()
    {
        list = new List<T>();
    }

    public NonSynchronizedObservableList(int capacity)
    {
        list = new List<T>(capacity);
    }

    public NonSynchronizedObservableList(IEnumerable<T> collection)
    {
        list = collection.ToList();
    }

    public T this[int index]
    {
        get => list[index];
        set
        {
            var oldValue = list[index];
            list[index] = value;
            CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Replace(value, oldValue, index, index));
        }
    }

    public int Count => list.Count;

    public bool IsReadOnly => false;

    public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

    public void Add(T item)
    {
        var index = list.Count;
        list.Add(item);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, index));
    }

    public void AddRange(IEnumerable<T> items)
    {
        var index = list.Count;
        using (var xs = new CloneCollection<T>(items))
        {
            // to avoid iterate twice, require copy before insert.
            list.AddRange(xs.Span);
            CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
        }
    }

    public void AddRange(T[] items)
    {
        var index = list.Count;
        list.AddRange(items);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
    }

    public void AddRange(params ReadOnlySpan<T> items)
    {
        var index = list.Count; // starting index
        list.AddRange(items);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
    }

    public void Clear()
    {
        list.Clear();
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Reset());
    }

    public bool Contains(T item)
    {
        return list.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        list.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in list)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void ForEach(Action<T> action)
    {
        foreach (var item in list)
        {
            action(item);
        }
    }

    public int IndexOf(T item)
    {
        return list.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        list.Insert(index, item);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, index));
    }

    public void InsertRange(int index, T[] items)
    {
        list.InsertRange(index, items);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
    }

    public void InsertRange(int index, IEnumerable<T> items)
    {
        using (var xs = new CloneCollection<T>(items))
        {
            list.InsertRange(index, xs.AsEnumerable());
            CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
        }
    }

    public void InsertRange(int index, ReadOnlySpan<T> items)
    {
        list.InsertRange(index, items);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
    }

    public bool Remove(T item)
    {
        var index = list.IndexOf(item);

        if (index >= 0)
        {
            list.RemoveAt(index);
            CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(item, index));
            return true;
        }
        else
        {
            return false;
        }
    }

    public void RemoveAt(int index)
    {
        var item = list[index];
        list.RemoveAt(index);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(item, index));
    }

    public void RemoveRange(int index, int count)
    {
        var range = CollectionsMarshal.AsSpan(list).Slice(index, count);
        // require copy before remove
        using (var xs = new CloneCollection<T>(range))
        {
            list.RemoveRange(index, count);
            CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(xs.Span, index));
        }
    }

    public void Move(int oldIndex, int newIndex)
    {
        var removedItem = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, removedItem);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Move(removedItem, newIndex, oldIndex));
    }

    public void Sort()
    {
        list.Sort();
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Sort(0, list.Count, null));
    }

    public void Sort(IComparer<T> comparer)
    {
        list.Sort(comparer);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Sort(0, list.Count, comparer));
    }

    public void Sort(int index, int count, IComparer<T> comparer)
    {
        list.Sort(index, count, comparer);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Sort(index, count, comparer));
    }

    public void Reverse()
    {
        list.Reverse();
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Reverse(0, list.Count));
    }

    public void Reverse(int index, int count)
    {
        list.Reverse(index, count);
        CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Reverse(index, count));
    }
}
