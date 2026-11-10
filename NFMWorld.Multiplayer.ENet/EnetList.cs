namespace ENet;

internal interface IEnetListLink<T> where T : class
{
    T? Next { get; set; }
    T? Previous { get; set; }
}

/// <summary>
/// Managed equivalent of ENet's intrusive <c>ENetList</c>. A null position is the list end,
/// matching the C sentinel semantics the protocol port relies on.
/// </summary>
internal sealed class EnetList<T> where T : class, IEnetListLink<T>
{
    public T? Head { get; private set; }
    public T? Tail { get; private set; }

    public bool IsEmpty => Head is null;

    public T? Begin => Head;
    public static T? End => null;
    public static T? Next(T? node) => node?.Next;
    public static T? Previous(T? node) => node?.Previous;

    public void Clear()
    {
        Head = null;
        Tail = null;
    }

    public void InsertBefore(T? position, T item)
    {
        if (position is null)
        {
            item.Next = null;
            item.Previous = Tail;

            if (Tail is null)
                Head = item;
            else
                Tail.Next = item;

            Tail = item;
            return;
        }

        item.Next = position;
        item.Previous = position.Previous;

        if (position.Previous is null)
            Head = item;
        else
            position.Previous.Next = item;

        position.Previous = item;
    }

    public void InsertEnd(T item) => InsertBefore(null, item);

    public void Remove(T item)
    {
        if (item.Previous is { } previous)
            previous.Next = item.Next;
        else
            Head = item.Next;

        if (item.Next is { } next)
            next.Previous = item.Previous;
        else
            Tail = item.Previous;

        item.Next = null;
        item.Previous = null;
    }

    public T? RemoveFirst()
    {
        if (Head is not { } head)
            return null;

        Remove(head);
        return head;
    }

    /// <summary>Moves the inclusive [first, last] range to the end of this list.</summary>
    public void MoveToEnd(EnetList<T> destination, T first, T last)
    {
        var current = first;

        while (true)
        {
            var next = current.Next;

            Remove(current);
            destination.InsertEnd(current);

            if (ReferenceEquals(current, last) || next is null)
                break;

            current = next;
        }
    }
}
