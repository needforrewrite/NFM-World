using System.Runtime.CompilerServices;

namespace TightPack;

/// <summary>
/// Per-serialization state: the reference table for <see cref="GenerateType.CircularReference"/>.
/// The writer materialises one only when a reference-tracked object is written, so a serialization
/// that never uses the feature allocates nothing here.
/// </summary>
public sealed class TightPackWriterState
{
    private List<object>? _objects;
    private Dictionary<object, uint>? _ids;

    internal bool TryGetReference(object value, out uint id)
    {
        if (_ids is null)
        {
            id = 0;
            return false;
        }

        return _ids.TryGetValue(value, out id);
    }

    internal uint AddReference(object value)
    {
        _objects ??= [];
        _ids ??= new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
        uint id = (uint)_objects.Count;
        _objects.Add(value);
        _ids[value] = id;
        return id;
    }

    internal bool TryGetObject(uint id, out object? value)
    {
        if (_objects is null || id >= _objects.Count)
        {
            value = null;
            return false;
        }

        value = _objects[(int)id];
        return true;
    }
}

/// <summary>The reader-side mirror of <see cref="TightPackWriterState"/>.</summary>
public sealed class TightPackReaderState
{
    private List<object>? _objects;

    /// <summary>Registers an object before its members are read, so a cycle can resolve to it.</summary>
    public void AddObject(object value)
    {
        _objects ??= [];
        _objects.Add(value);
    }

    internal bool TryGetObject(uint id, out object? value)
    {
        if (_objects is null || id >= _objects.Count)
        {
            value = null;
            return false;
        }

        value = _objects[(int)id];
        return true;
    }
}

internal static class ReferenceEqualityComparer
{
    public static readonly IEqualityComparer<object> Instance = new Comparer();

    private sealed class Comparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
