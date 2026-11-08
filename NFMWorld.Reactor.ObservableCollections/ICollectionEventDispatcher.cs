using System.Collections;
using System.Collections.Specialized;

namespace WorldXaml.ObservableCollections;

public interface ICollectionEventDispatcher
{
    void Post(CollectionEventDispatcherEventArgs ev);
}

public class SynchronizationContextCollectionEventDispatcher(SynchronizationContext synchronizationContext)
    : ICollectionEventDispatcher
{
    private static readonly Lazy<ICollectionEventDispatcher> _current = new(static () =>
        {
            var current = SynchronizationContext.Current;
            if (current == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current is null");
            }

            return new SynchronizationContextCollectionEventDispatcher(current);
        }
    );

    public static readonly ICollectionEventDispatcher Current = _current.Value;

    private static readonly SendOrPostCallback Callback = SendOrPostCallback;

    public void Post(CollectionEventDispatcherEventArgs ev)
    {
        if (SynchronizationContext.Current == null)
        {
            // non-UI thread, post the event asynchronously
            synchronizationContext.Post(Callback, ev);
        }
        else
        {
            // UI thread, send the event synchronously
            Callback(ev);
        }
    }

    private static void SendOrPostCallback(object? state)
    {
        var ev = (CollectionEventDispatcherEventArgs)state!;
        ev.Invoke();
    }
}

internal class InlineCollectionEventDispatcher : ICollectionEventDispatcher
{
    public static readonly ICollectionEventDispatcher Instance = new InlineCollectionEventDispatcher();

    private InlineCollectionEventDispatcher()
    {
    }

    public void Post(CollectionEventDispatcherEventArgs ev)
    {
        ev.Invoke();
    }
}

public class CollectionEventDispatcherEventArgs : NotifyCollectionChangedEventArgs
{
    // +state, init;
    public object Collection { get; set; } = null!;
    public bool IsInvokeCollectionChanged { get; set; }
    public bool IsInvokePropertyChanged { get; set; }

    internal Action<CollectionEventDispatcherEventArgs> Invoker { get; set; } = null!;

    public void Invoke()
    {
        Invoker.Invoke(this);
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action) : base(action)
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems) : base(
        action,
        changedItems
    )
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem) : base(
        action,
        changedItem
    )
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList newItems, IList oldItems) : base(
        action,
        newItems,
        oldItems
    )
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems, int startingIndex) :
        base(action, changedItems, startingIndex)
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem, int index) : base(
        action,
        changedItem,
        index
    )
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? newItem, object? oldItem) : base(
        action,
        newItem,
        oldItem
    )
    {
    }

    public CollectionEventDispatcherEventArgs(
        NotifyCollectionChangedAction action,
        IList newItems,
        IList oldItems,
        int startingIndex
    ) : base(action, newItems, oldItems, startingIndex)
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems, int index, int oldIndex)
        : base(action, changedItems, index, oldIndex)
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem, int index, int oldIndex)
        : base(action, changedItem, index, oldIndex)
    {
    }

    public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? newItem, object? oldItem, int index) :
        base(action, newItem, oldItem, index)
    {
    }
}
