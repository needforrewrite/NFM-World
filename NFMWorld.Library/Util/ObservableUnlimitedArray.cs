using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NFMWorldLibrary.Util;

/// <summary>
/// An <see cref="UnlimitedArray{T}"/> subclass that implements
/// <see cref="INotifyPropertyChanged"/>, <see cref="INotifyPropertyChanging"/>,
/// and <see cref="INotifyCollectionChanged"/> for data-binding scenarios.
/// </summary>
/// <remarks>
/// <para>Notification behaviour follows the same conventions as
/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>:</para>
/// <list type="bullet">
/// <item><c>Add</c> / <c>Insert</c> → <c>PropertyChanging("Count")</c>,
/// <c>PropertyChanging("Item[]")</c>, then <c>CollectionChanged(Add)</c>,
/// <c>PropertyChanged("Count")</c>, <c>PropertyChanged("Item[]")</c>.</item>
/// <item><c>RemoveAt</c> → same pattern with <c>CollectionChanged(Remove)</c>.</item>
/// <item><c>Clear</c> → <c>CollectionChanged(Reset)</c>.</item>
/// <item>Indexer setter (replace) → <c>PropertyChanging("Item[]")</c>,
/// <c>CollectionChanged(Replace)</c>, <c>PropertyChanged("Item[]")</c>.</item>
/// <item>Indexer setter (append) → same as <c>Add</c>.</item>
/// <item><c>Sort</c> → <c>CollectionChanged(Reset)</c> (after base sort).</item>
/// </list>
/// <para>
/// <c>Remove(T)</c> is intentionally <em>not</em> overridden — the base
/// implementation delegates to <see cref="RemoveAt"/> which fires the
/// appropriate events.
/// </para>
/// </remarks>
public class ObservableUnlimitedArray<T> : UnlimitedArray<T>,
    INotifyPropertyChanged,
    INotifyPropertyChanging,
    INotifyCollectionChanged
{
    // ------------------------------------------------------------------
    // Constructors
    // ------------------------------------------------------------------

    public ObservableUnlimitedArray()
    {
    }

    public ObservableUnlimitedArray(int capacity) : base(capacity)
    {
    }

    public ObservableUnlimitedArray(IEnumerable<T> items) : base(items)
    {
    }

    // ------------------------------------------------------------------
    // Cached event-args singletons (avoid per-call allocations)
    // ------------------------------------------------------------------

    private static readonly PropertyChangedEventArgs s_countChangedArgs = new("Count");
    private static readonly PropertyChangedEventArgs s_itemIndexChangedArgs = new("Item[]");
    private static readonly PropertyChangingEventArgs s_countChangingArgs = new("Count");
    private static readonly PropertyChangingEventArgs s_itemIndexChangingArgs = new("Item[]");
    private static readonly NotifyCollectionChangedEventArgs s_resetCollectionArgs = new(NotifyCollectionChangedAction.Reset);

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    // ------------------------------------------------------------------
    // Raise-helpers (virtual so further subclasses can customise)
    // ------------------------------------------------------------------

    /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged"/>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    /// <inheritdoc cref="INotifyPropertyChanging.PropertyChanging"/>
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        PropertyChanging?.Invoke(this, e);
    }

    /// <inheritdoc cref="INotifyCollectionChanged.CollectionChanged"/>
    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }

    // ------------------------------------------------------------------
    // Overridden mutators
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public override void Add(T item)
    {
        var index = Count; // capture before base.Add mutates _size

        OnPropertyChanging(s_countChangingArgs);
        OnPropertyChanging(s_itemIndexChangingArgs);

        base.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        OnPropertyChanged(s_countChangedArgs);
        OnPropertyChanged(s_itemIndexChangedArgs);
    }

    /// <inheritdoc />
    public override void Insert(int index, T item)
    {
        OnPropertyChanging(s_countChangingArgs);
        OnPropertyChanging(s_itemIndexChangingArgs);

        base.Insert(index, item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        OnPropertyChanged(s_countChangedArgs);
        OnPropertyChanged(s_itemIndexChangedArgs);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Remove"/> is intentionally <em>not</em> overridden; the
    /// base implementation calls <see cref="RemoveAt"/> which dispatches
    /// to this override.
    /// </remarks>
    public override void RemoveAt(int index)
    {
        var oldItem = base[index];

        OnPropertyChanging(s_countChangingArgs);
        OnPropertyChanging(s_itemIndexChangingArgs);

        base.RemoveAt(index);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Remove, oldItem, index));
        OnPropertyChanged(s_countChangedArgs);
        OnPropertyChanged(s_itemIndexChangedArgs);
    }

    /// <inheritdoc />
    public override void Clear()
    {
        OnPropertyChanging(s_countChangingArgs);
        OnPropertyChanging(s_itemIndexChangingArgs);

        base.Clear();

        OnCollectionChanged(s_resetCollectionArgs);
        OnPropertyChanged(s_countChangedArgs);
        OnPropertyChanged(s_itemIndexChangedArgs);
    }

    /// <inheritdoc />
    public override T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => base[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "Index must be non-negative.");

            var isReplace = index < Count;

            if (isReplace)
            {
                var oldItem = base[index];

                OnPropertyChanging(s_itemIndexChangingArgs);

                base[index] = value;

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace, value, oldItem, index));
            }
            else
            {
                OnPropertyChanging(s_countChangingArgs);
                OnPropertyChanging(s_itemIndexChangingArgs);

                base[index] = value;

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, value, index));
                OnPropertyChanged(s_countChangedArgs);
            }

            OnPropertyChanged(s_itemIndexChangedArgs);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Fires <see cref="CollectionChanged"/> with <see cref="NotifyCollectionChangedAction.Reset"/>
    /// and <c>PropertyChanged("Item[]")</c> after the sort completes. <c>Count</c> is unchanged.
    /// </remarks>
    public override void Sort(Comparison<T> compareFunc)
    {
        base.Sort(compareFunc);

        OnCollectionChanged(s_resetCollectionArgs);
        OnPropertyChanged(s_itemIndexChangedArgs);
    }
}
