namespace NFMWorld.Sentry;

/// <summary>
/// A unique identifier for a Sentry event. Wraps a Guid, serialized as 32-char hex without dashes.
/// </summary>
public readonly struct SentryEventId(Guid value) : IEquatable<SentryEventId>
{
    private readonly Guid _value = value;

    public static SentryEventId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Parse a Sentry event ID from a 32-character hex string (with or without dashes).
    /// </summary>
    public static SentryEventId Parse(string s)
    {
        return new SentryEventId(Guid.Parse(s));
    }

    /// <summary>
    /// Returns the event ID as a 32-character lowercase hex string (no dashes), per Sentry spec.
    /// </summary>
    public override string ToString() => _value.ToString("N");

    public bool Equals(SentryEventId other) => _value.Equals(other._value);
    public override bool Equals(object? obj) => obj is SentryEventId other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();

    public static bool operator ==(SentryEventId left, SentryEventId right) => left.Equals(right);
    public static bool operator !=(SentryEventId left, SentryEventId right) => !left.Equals(right);
}
