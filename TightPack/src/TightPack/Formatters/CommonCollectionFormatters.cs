using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace TightPack.Formatters;

// Formatters for BCL collection types that are written and rebuilt as a sequence. Each writes a
// four-byte count then the elements, so the wire form is the same shape MemoryPack uses for the
// same type; a type that has an ordering of its own (a stack) writes in enumeration order and is
// rebuilt by re-adding, which preserves it.

/// <summary>Writes a stack bottom-to-top and rebuilds it by pushing in reverse.</summary>
public sealed class StackFormatter<T> : TightPackFormatter<Stack<T>?>
{
    public static readonly StackFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Stack<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        // Enumeration is top-first. Writing bottom-first lets the reader rebuild the same stack by
        // pushing each item as it arrives.
        T[] items = value.ToArray();
        writer.WriteCollectionHeader(items.Length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = items.Length - 1; i >= 0; i--)
        {
            T? current = items[i];
            formatter.Serialize(ref writer, in current);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref Stack<T>? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        var result = new Stack<T>(length);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            result.Push(item!);
        }

        value = result;
    }
}

public sealed class QueueFormatter<T> : TightPackFormatter<Queue<T>?>
{
    public static readonly QueueFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Queue<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, Queue<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref Queue<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        if (items is null)
        {
            value = null;
            return;
        }

        value = new Queue<T>(items);
    }
}

public sealed class LinkedListFormatter<T> : TightPackFormatter<LinkedList<T>?>
{
    public static readonly LinkedListFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in LinkedList<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ICollection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref LinkedList<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        if (items is null)
        {
            value = null;
            return;
        }

        var result = new LinkedList<T>();
        foreach (T item in items)
        {
            result.AddLast(item);
        }

        value = result;
    }
}

public sealed class SortedSetFormatter<T> : TightPackFormatter<SortedSet<T>?>
{
    public static readonly SortedSetFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in SortedSet<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, SortedSet<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref SortedSet<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new SortedSet<T>(items);
    }
}

public sealed class CollectionFormatter<T> : TightPackFormatter<Collection<T>?>
{
    public static readonly CollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in Collection<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, Collection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref Collection<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new Collection<T>(items);
    }
}

public sealed class ObservableCollectionFormatter<T> : TightPackFormatter<ObservableCollection<T>?>
{
    public static readonly ObservableCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ObservableCollection<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ObservableCollection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ObservableCollection<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ObservableCollection<T>(items);
    }
}

public sealed class ConcurrentQueueFormatter<T> : TightPackFormatter<ConcurrentQueue<T>?>
{
    public static readonly ConcurrentQueueFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ConcurrentQueue<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ConcurrentQueue<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ConcurrentQueue<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ConcurrentQueue<T>(items);
    }
}

public sealed class ConcurrentStackFormatter<T> : TightPackFormatter<ConcurrentStack<T>?>
{
    public static readonly ConcurrentStackFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ConcurrentStack<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        // Enumeration is top-first; the constructor pushes in order, so reverse it to keep the top.
        T[] items = value.ToArray();
        T[] reversed = new T[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            reversed[i] = items[items.Length - 1 - i];
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, T[]>(ref writer, reversed.Length, reversed);
    }

    public override void Deserialize(ref TightPackReader reader, ref ConcurrentStack<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ConcurrentStack<T>(items);
    }
}

public sealed class ConcurrentBagFormatter<T> : TightPackFormatter<ConcurrentBag<T>?>
{
    public static readonly ConcurrentBagFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ConcurrentBag<T>? value)
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ConcurrentBag<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ConcurrentBag<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ConcurrentBag<T>(items);
    }
}

public sealed class ConcurrentDictionaryFormatter<TKey, TValue>
    : TightPackFormatter<ConcurrentDictionary<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly ConcurrentDictionaryFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ConcurrentDictionary<TKey, TValue>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteMap<TBufferWriter, TKey, TValue, ConcurrentDictionary<TKey, TValue>>(
            ref writer,
            value.Count,
            value
        );
    }

    public override void Deserialize(ref TightPackReader reader, ref ConcurrentDictionary<TKey, TValue>? value)
    {
        Dictionary<TKey, TValue>? items = InterfaceCollections.ReadMap<TKey, TValue>(ref reader);
        value = items is null ? null : new ConcurrentDictionary<TKey, TValue>(items);
    }
}

public sealed class SortedDictionaryFormatter<TKey, TValue> : TightPackFormatter<SortedDictionary<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly SortedDictionaryFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in SortedDictionary<TKey, TValue>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteMap<TBufferWriter, TKey, TValue, SortedDictionary<TKey, TValue>>(
            ref writer,
            value.Count,
            value
        );
    }

    public override void Deserialize(ref TightPackReader reader, ref SortedDictionary<TKey, TValue>? value)
    {
        Dictionary<TKey, TValue>? items = InterfaceCollections.ReadMap<TKey, TValue>(ref reader);
        value = items is null ? null : new SortedDictionary<TKey, TValue>(items);
    }
}

public sealed class SortedListFormatter<TKey, TValue> : TightPackFormatter<SortedList<TKey, TValue>?>
    where TKey : notnull
{
    public static readonly SortedListFormatter<TKey, TValue> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in SortedList<TKey, TValue>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteMap<TBufferWriter, TKey, TValue, SortedList<TKey, TValue>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref SortedList<TKey, TValue>? value)
    {
        Dictionary<TKey, TValue>? items = InterfaceCollections.ReadMap<TKey, TValue>(ref reader);
        value = items is null ? null : new SortedList<TKey, TValue>(items);
    }
}

public sealed class PriorityQueueFormatter<TElement, TPriority>
    : TightPackFormatter<PriorityQueue<TElement, TPriority>?>
{
    public static readonly PriorityQueueFormatter<TElement, TPriority> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in PriorityQueue<TElement, TPriority>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        writer.WriteCollectionHeader(value.Count);
        var elementFormatter = TightPackFormatterProvider.GetFormatter<TElement>();
        var priorityFormatter = TightPackFormatterProvider.GetFormatter<TPriority>();
        foreach ((TElement element, TPriority priority) in value.UnorderedItems)
        {
            TElement? currentElement = element;
            TPriority? currentPriority = priority;
            elementFormatter.Serialize(ref writer, in currentElement);
            priorityFormatter.Serialize(ref writer, in currentPriority);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref PriorityQueue<TElement, TPriority>? value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = null;
            return;
        }

        var result = new PriorityQueue<TElement, TPriority>(length);
        var elementFormatter = TightPackFormatterProvider.GetFormatter<TElement>();
        var priorityFormatter = TightPackFormatterProvider.GetFormatter<TPriority>();
        for (int i = 0; i < length; i++)
        {
            TElement? element = default;
            TPriority? priority = default;
            elementFormatter.Deserialize(ref reader, ref element);
            priorityFormatter.Deserialize(ref reader, ref priority);
            result.Enqueue(element!, priority!);
        }

        value = result;
    }
}

/// <summary>Round-trips through an array, because the collection itself has no settable backing.</summary>
public sealed class ReadOnlyCollectionFormatter<T> : TightPackFormatter<ReadOnlyCollection<T>?>
{
    public static readonly ReadOnlyCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyCollection<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ReadOnlyCollection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyCollection<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ReadOnlyCollection<T>(items);
    }
}

public sealed class ReadOnlyObservableCollectionFormatter<T>
    : TightPackFormatter<ReadOnlyObservableCollection<T>?>
{
    public static readonly ReadOnlyObservableCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in ReadOnlyObservableCollection<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, ReadOnlyObservableCollection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref ReadOnlyObservableCollection<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        value = items is null ? null : new ReadOnlyObservableCollection<T>(new ObservableCollection<T>(items));
    }
}

/// <summary>
/// Blocks while deserializing, because <see cref="BlockingCollection{T}"/> owns a semaphore that has
/// to be re-created with each added item. Only use it for a collection built once.
/// </summary>
public sealed class BlockingCollectionFormatter<T> : TightPackFormatter<BlockingCollection<T>?>
{
    public static readonly BlockingCollectionFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref TightPackWriter<TBufferWriter> writer,
        in BlockingCollection<T>? value
    )
    {
        if (value is null)
        {
            writer.WriteNullCollectionHeader();
            return;
        }

        InterfaceCollections.WriteSequence<TBufferWriter, T, BlockingCollection<T>>(ref writer, value.Count, value);
    }

    public override void Deserialize(ref TightPackReader reader, ref BlockingCollection<T>? value)
    {
        List<T>? items = InterfaceCollections.ReadSequence<T>(ref reader);
        if (items is null)
        {
            value = null;
            return;
        }

        var result = new BlockingCollection<T>();
        foreach (T item in items)
        {
            result.Add(item);
        }

        value = result;
    }
}

/// <summary>
/// Writes the segment's elements only; the offset is a view into another array and is meaningless
/// once the bytes leave the process, so a read segment starts at zero over its own array.
/// </summary>
public sealed class ArraySegmentFormatter<T> : TightPackFormatter<ArraySegment<T>>
{
    public static readonly ArraySegmentFormatter<T> Default = new();

    public override void Serialize<TBufferWriter>(ref TightPackWriter<TBufferWriter> writer, in ArraySegment<T> value)
    {
        writer.WriteCollectionHeader(value.Count);
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < value.Count; i++)
        {
            T? item = value[i];
            formatter.Serialize(ref writer, in item);
        }
    }

    public override void Deserialize(ref TightPackReader reader, ref ArraySegment<T> value)
    {
        if (!reader.TryReadCollectionHeader(out int length))
        {
            value = default;
            return;
        }

        var items = new T[length];
        var formatter = TightPackFormatterProvider.GetFormatter<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            formatter.Deserialize(ref reader, ref item);
            items[i] = item!;
        }

        value = new ArraySegment<T>(items);
    }
}
