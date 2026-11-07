namespace NFMWorld.Archive.Auth;

/// <summary>
/// Remembers nonces from signed server requests so a captured request cannot be replayed inside
/// its timestamp window. Entries expire at the same time the signature does, so the cache only
/// ever holds what is still replayable.
/// </summary>
public sealed class NonceCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);

    /// <summary>Records a nonce, returning false when it has already been used.</summary>
    public bool TryUse(string keyId, string nonce)
    {
        if (string.IsNullOrEmpty(nonce))
            return false;

        var key = keyId + ":" + nonce;
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_seen.TryGetValue(key, out var expires) && expires > now)
                return false;

            _seen[key] = now + Ttl;

            if (_seen.Count > 10_000)
            {
                foreach (var (k, v) in _seen)
                    if (v <= now)
                        _seen.Remove(k);
            }

            return true;
        }
    }
}
