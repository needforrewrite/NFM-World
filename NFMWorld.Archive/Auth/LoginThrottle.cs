namespace NFMWorld.Archive.Auth;

/// <summary>
/// Per-IP and per-username attempt limiting for login and registration.
///
/// In-memory on purpose: with a single SQLite-backed node there is no second process to share
/// state with, and a restart clearing the counters is acceptable for a slowdown that only has to
/// make offline guessing impractical. Move this to the database or a shared cache if the archive
/// ever runs more than one instance.
/// </summary>
public sealed class LoginThrottle
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _attempts = new(StringComparer.Ordinal);

    /// <summary>True when this key has exhausted its attempts and should be refused.</summary>
    public bool IsBlocked(string key, out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            if (!_attempts.TryGetValue(key, out var queue))
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            var cutoff = DateTimeOffset.UtcNow - Window;
            while (queue.Count > 0 && queue.Peek() < cutoff)
                queue.Dequeue();

            if (queue.Count < MaxAttempts)
            {
                retryAfter = TimeSpan.Zero;
                return false;
            }

            retryAfter = queue.Peek() + Window - DateTimeOffset.UtcNow;
            return true;
        }
    }

    public void RecordFailure(string key)
    {
        lock (_lock)
        {
            if (!_attempts.TryGetValue(key, out var queue))
                _attempts[key] = queue = new Queue<DateTimeOffset>();
            queue.Enqueue(DateTimeOffset.UtcNow);

            // Keep the map from growing without bound under a distributed spray.
            if (_attempts.Count > 50_000)
                Prune(DateTimeOffset.UtcNow - Window);
        }
    }

    public void Reset(string key)
    {
        lock (_lock)
            _attempts.Remove(key);
    }

    private void Prune(DateTimeOffset cutoff)
    {
        foreach (var (key, queue) in _attempts)
        {
            while (queue.Count > 0 && queue.Peek() < cutoff)
                queue.Dequeue();
            if (queue.Count == 0)
                _attempts.Remove(key);
        }
    }
}
