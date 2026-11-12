namespace NFMWorld.Sentry;

/// <summary>
/// Represents a Sentry event (error or message).
/// </summary>
public struct SentryEvent()
{
    /// <summary>
    /// Unique event identifier. Auto-generated on construction.
    /// </summary>
    public SentryEventId EventId { get; } = SentryEventId.NewId();

    /// <summary>
    /// The timestamp when this event was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The exception associated with this event, if any.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// A human-readable message describing the event.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The severity level of this event.
    /// </summary>
    public SentryLevel Level { get; set; } = SentryLevel.Error;

    /// <summary>
    /// The release version.
    /// </summary>
    public string? Release { get; set; }

    /// <summary>
    /// Transaction or operation name associated with this event.
    /// </summary>
    public string? TransactionName { get; set; }

    /// <summary>
    /// Additional key-value tags for the event.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    /// Breadcrumbs captured at the time this event was created.
    /// A snapshot of the global breadcrumb ring buffer.
    /// </summary>
    public IReadOnlyList<Breadcrumb>? Breadcrumbs { get; set; }

    /// <summary>
    /// Create a Sentry event from an exception.
    /// </summary>
    public SentryEvent(Exception exception) : this()
    {
        Exception = exception;
        Message = exception.Message;
    }
}
