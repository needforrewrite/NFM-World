using System.Collections;
using System.Collections.Concurrent;

namespace NFMWorld.Util;

public readonly struct ConcurrentHashSet<T>(ConcurrentDictionary<T, byte> dictionary) : ISet<T>, IReadOnlyCollection<T>
    where T : notnull
{
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var (k, _) in dictionary)
        {
            yield return k;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<T>.Add(T item)
    {
        dictionary.TryAdd(item, 0);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        foreach (var e in other)
        {
            dictionary.Remove(e, out _);
        }
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        foreach (var (k, _) in dictionary)
        {
            if (!enumerable.Contains(k))
            {
                dictionary.Remove(k, out _);
            }
        }
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        foreach (var (k, _) in dictionary)
        {
            if (!enumerable.Contains(k))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        foreach (var e in enumerable)
        {
            if (!dictionary.ContainsKey(e))
            {
                return false;
            }
        }

        return dictionary.Count > enumerable.Length;
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        foreach (var (k, _) in dictionary)
        {
            if (!enumerable.Contains(k))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        foreach (var e in enumerable)
        {
            if (!dictionary.ContainsKey(e))
            {
                return false;
            }
        }

        return true;
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        foreach (var e in other)
        {
            if (dictionary.ContainsKey(e))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        var enumerable = other as T[] ?? other.ToArray();
        if (dictionary.Count != enumerable.Length)
        {
            return false;
        }

        foreach (var e in enumerable)
        {
            if (!dictionary.ContainsKey(e))
            {
                return false;
            }
        }

        return true;
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        foreach (var e in other)
        {
            if (!dictionary.TryRemove(e, out _))
            {
                dictionary.TryAdd(e, 0);
            }
        }
    }

    public void UnionWith(IEnumerable<T> other)
    {
        foreach (var e in other)
        {
            dictionary.TryAdd(e, 0);
        }
    }

    public bool Add(T item)
    {
        return dictionary.TryAdd(item, 0);
    }

    public void Clear()
    {
        dictionary.Clear();
    }

    public bool Contains(T item)
    {
        return dictionary.ContainsKey(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        foreach (var (k, _) in dictionary)
        {
            array[arrayIndex++] = k;
        }
    }

    public bool Remove(T item)
    {
        return dictionary.Remove(item, out _);
    }

    public int Count => dictionary.Count;

    public bool IsReadOnly => false;

    int IReadOnlyCollection<T>.Count => dictionary.Count;
}