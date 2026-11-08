using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using ObservableCollections;

namespace WorldXaml.ObservableCollections;

public class NonSynchronizedObservableCollection<T> : Collection<T>, INonSynchronizedObservableCollection<T>
{
    private SimpleMonitor?
        _monitor; // Lazily allocated only when a subclass calls BlockReentrancy() or during serialization. Do not rename (binary serialization)

    [NonSerialized] private int _blockReentrancyCount;

    /// <summary>
    /// Initializes a new instance of ObservableCollection that is empty and has default initial capacity.
    /// </summary>
    public NonSynchronizedObservableCollection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ObservableCollection class that contains
    /// elements copied from the specified collection and has sufficient capacity
    /// to accommodate the number of elements copied.
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new list.</param>
    /// <remarks>
    /// The elements are copied onto the ObservableCollection in the
    /// same order they are read by the enumerator of the collection.
    /// </remarks>
    /// <exception cref="ArgumentNullException"> collection is a null reference </exception>
    public NonSynchronizedObservableCollection(IEnumerable<T> collection) : base(
        new List<T>(collection ?? throw new ArgumentNullException(nameof(collection)))
    )
    {
    }

    /// <summary>
    /// Initializes a new instance of the ObservableCollection class
    /// that contains elements copied from the specified list
    /// </summary>
    /// <param name="list">The list whose elements are copied to the new list.</param>
    /// <remarks>
    /// The elements are copied onto the ObservableCollection in the
    /// same order they are read by the enumerator of the list.
    /// </remarks>
    /// <exception cref="ArgumentNullException"> list is a null reference </exception>
    public NonSynchronizedObservableCollection(List<T> list) : base(
        new List<T>(list ?? throw new ArgumentNullException(nameof(list)))
    )
    {
    }

    /// <summary>
    /// Move item at oldIndex to newIndex.
    /// </summary>
    public void Move(int oldIndex, int newIndex) => MoveItem(oldIndex, newIndex);

    /// <summary>
    /// Occurs when the collection changes, either by adding or removing an item.
    /// </summary>
    [field: NonSerialized]
    public virtual event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

    /// <summary>
    /// Called by base class Collection&lt;T&gt; when the list is being cleared;
    /// raises a CollectionChanged event to any listeners.
    /// </summary>
    protected override void ClearItems()
    {
        CheckReentrancy();
        base.ClearItems();
        OnCollectionReset();
    }

    /// <summary>
    /// Called by base class Collection&lt;T&gt; when an item is removed from list;
    /// raises a CollectionChanged event to any listeners.
    /// </summary>
    protected override void RemoveItem(int index)
    {
        CheckReentrancy();
        T removedItem = this[index];

        base.RemoveItem(index);

        OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
    }

    /// <summary>
    /// Called by base class Collection&lt;T&gt; when an item is added to list;
    /// raises a CollectionChanged event to any listeners.
    /// </summary>
    protected override void InsertItem(int index, T item)
    {
        CheckReentrancy();
        base.InsertItem(index, item);

        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
    }

    /// <summary>
    /// Called by base class Collection&lt;T&gt; when an item is set in list;
    /// raises a CollectionChanged event to any listeners.
    /// </summary>
    protected override void SetItem(int index, T item)
    {
        CheckReentrancy();
        T originalItem = this[index];
        base.SetItem(index, item);

        OnCollectionChanged(NotifyCollectionChangedAction.Replace, originalItem, item, index);
    }

    /// <summary>
    /// Called by base class ObservableCollection&lt;T&gt; when an item is to be moved within the list;
    /// raises a CollectionChanged event to any listeners.
    /// </summary>
    protected virtual void MoveItem(int oldIndex, int newIndex)
    {
        CheckReentrancy();

        T removedItem = this[oldIndex];

        base.RemoveItem(oldIndex);
        base.InsertItem(newIndex, removedItem);

        OnCollectionChanged(NotifyCollectionChangedAction.Move, removedItem, newIndex, oldIndex);
    }

    /// <summary>
    /// PropertyChanged event (per <see cref="INotifyPropertyChanged" />).
    /// </summary>
    [field: NonSerialized]
    protected virtual event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raise CollectionChanged event to any listeners.
    /// Properties/methods modifying this ObservableCollection will raise
    /// a collection changed event through this virtual method.
    /// </summary>
    /// <remarks>
    /// When overriding this method, either call its base implementation
    /// or call <see cref="BlockReentrancy"/> to guard against reentrant collection changes.
    /// </remarks>
    protected virtual void OnCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
    {
        NotifyCollectionChangedEventHandler<T>? handler = CollectionChanged;
        if (handler != null)
        {
            // Not calling BlockReentrancy() here to avoid the SimpleMonitor allocation.
            _blockReentrancyCount++;
            try
            {
                handler(e);
            }
            finally
            {
                _blockReentrancyCount--;
            }
        }
    }

    /// <summary>
    /// Disallow reentrant attempts to change this collection. E.g. an event handler
    /// of the CollectionChanged event is not allowed to make changes to this collection.
    /// </summary>
    /// <remarks>
    /// typical usage is to wrap e.g. a OnCollectionChanged call with a using() scope:
    /// <code>
    ///         using (BlockReentrancy())
    ///         {
    ///             CollectionChanged(this, new NotifyCollectionChangedEventArgs(action, item, index));
    ///         }
    /// </code>
    /// </remarks>
    protected SimpleMonitor BlockReentrancy()
    {
        _blockReentrancyCount++;
        return EnsureMonitorInitialized();
    }

    /// <summary> Check and assert for reentrant attempts to change this collection. </summary>
    /// <exception cref="InvalidOperationException"> raised when changing the collection
    /// while another collection change is still being notified to other listeners </exception>
    protected void CheckReentrancy()
    {
        if (_blockReentrancyCount > 0)
        {
            // we can allow changes if there's only one listener - the problem
            // only arises if reentrant changes make the original event args
            // invalid for later listeners.  This keeps existing code working
            // (e.g. Selector.SelectedItems).
            NotifyCollectionChangedEventHandler<T>? handler = CollectionChanged;
            if (handler is { HasSingleTarget: false })
                throw new InvalidOperationException("Cannot modify the collection during a CollectionChanged event.");
        }
    }

    /// <summary>
    /// Helper to raise CollectionChanged event to any listeners
    /// </summary>
    private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index)
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs<T>(action, true, newItem: item, newStartingIndex: index));
    }

    /// <summary>
    /// Helper to raise CollectionChanged event to any listeners
    /// </summary>
    private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index, int oldIndex)
    {
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs<T>(
                action,
                true,
                newItem: item,
                newStartingIndex: index,
                oldStartingIndex: oldIndex
            )
        );
    }

    /// <summary>
    /// Helper to raise CollectionChanged event to any listeners
    /// </summary>
    private void OnCollectionChanged(NotifyCollectionChangedAction action, T oldItem, T newItem, int index)
    {
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs<T>(
                action,
                true,
                newItem: newItem,
                oldItem: oldItem,
                newStartingIndex: index,
                oldStartingIndex: index
            )
        );
    }

    /// <summary>
    /// Helper to raise CollectionChanged event with action == Reset to any listeners
    /// </summary>
    private void OnCollectionReset() => OnCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reset());

    private SimpleMonitor EnsureMonitorInitialized() => _monitor ??= new SimpleMonitor(this);

    protected readonly struct SimpleMonitor : IDisposable
    {
        private readonly NonSynchronizedObservableCollection<T> _collection;

        public SimpleMonitor(NonSynchronizedObservableCollection<T> collection)
        {
            Debug.Assert(collection != null);
            _collection = collection;
        }

        public void Dispose() => _collection._blockReentrancyCount--;
    }
}
