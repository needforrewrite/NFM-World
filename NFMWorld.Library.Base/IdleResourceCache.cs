namespace NFMWorldLibrary.Util;

public sealed class ResourceLease<T> : IDisposable
{
    private Action? _release;
    private T _value;

    internal ResourceLease(T value, Action release)
    {
        _value = value;
        _release = release;
    }

    public T Value => _release is not null ? _value : throw new ObjectDisposedException(nameof(ResourceLease<T>));

    public void Dispose()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release is null) return;
        _value = default!;
        release();
    }
}

public sealed class IdleResourceCache<TKey, TValue> where TKey : notnull
{
    private sealed class Entry(TValue value, long used)
    {
        public readonly TValue Value = value;
        public long Used = used;
        public int Pins;
    }

    private readonly object _gate = new();
    private readonly Dictionary<TKey, Entry> _entries;
    private readonly int _maxUnused;
    private readonly TimeSpan _idleTimeout;
    private readonly Action<TValue>? _dispose;
    private readonly TimeProvider _clock;
    private int _unused;

    public IdleResourceCache(
        int maxUnused,
        TimeSpan idleTimeout,
        Action<TValue>? dispose = null,
        IEqualityComparer<TKey>? comparer = null,
        TimeProvider? clock = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxUnused);
        if (idleTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        _maxUnused = maxUnused;
        _idleTimeout = idleTimeout;
        _dispose = dispose;
        _entries = new(comparer);
        _clock = clock ?? TimeProvider.System;
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_dispose is not null) throw new InvalidOperationException("Disposable resources require a lease.");
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.Used = _clock.GetTimestamp();
                value = entry.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> create)
    {
        if (_dispose is not null) throw new InvalidOperationException("Disposable resources require a lease.");
        TValue value;
        bool trim;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new(create(key), _clock.GetTimestamp());
                _entries.Add(key, entry);
                _unused++;
            }

            entry.Used = _clock.GetTimestamp();
            value = entry.Value;
            trim = _unused > _maxUnused;
        }

        if (trim) Trim();
        return value;
    }

    public ResourceLease<TValue> Acquire(TKey key, Func<TKey, TValue> create)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new(create(key), _clock.GetTimestamp()) { Pins = 1 };
                _entries.Add(key, entry);
            }
            else Pin(entry);

            return Lease(entry);
        }
    }

    public bool TryAcquire(TKey key, out ResourceLease<TValue>? lease)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                Pin(entry);
                lease = Lease(entry);
                return true;
            }
        }

        lease = null;
        return false;
    }

    private void Pin(Entry entry)
    {
        if (entry.Pins++ == 0) _unused--;
        entry.Used = _clock.GetTimestamp();
    }

    private ResourceLease<TValue> Lease(Entry entry) => new(entry.Value, () =>
        {
            lock (_gate)
            {
                if (--entry.Pins == 0)
                {
                    entry.Used = _clock.GetTimestamp();
                    _unused++;
                }
            }
        }
    );

    // Call GPU-cache maintenance on the graphics thread, between frames.
    public void Trim(bool clearUnused = false)
    {
        var released = new List<TValue>();
        lock (_gate)
        {
            var now = _clock.GetTimestamp();
            var excess = Math.Max(0, _unused - _maxUnused);
            foreach (var pair in _entries.Where(pair => pair.Value.Pins == 0).OrderBy(pair => pair.Value.Used).ToArray())
            {
                if (!clearUnused && excess <= 0 && _clock.GetElapsedTime(pair.Value.Used, now) < _idleTimeout) continue;
                _entries.Remove(pair.Key);
                _unused--;
                excess--;
                released.Add(pair.Value.Value);
            }
        }

        List<Exception>? failures = null;
        foreach (var value in released)
        {
            try
            {
                _dispose?.Invoke(value);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null) throw new AggregateException("Some cached resources could not be disposed.", failures);
    }
}
