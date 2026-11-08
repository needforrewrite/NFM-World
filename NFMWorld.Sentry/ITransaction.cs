namespace NFMWorld.Sentry;

/// <summary>
/// The status of a completed transaction span.
/// </summary>
public enum SpanStatus
{
    Ok,
    Cancelled,
    Unknown,
    InvalidArgument,
    DeadlineExceeded,
    NotFound,
    AlreadyExists,
    PermissionDenied,
    ResourceExhausted,
    FailedPrecondition,
    Aborted,
    OutOfRange,
    Unimplemented,
    InternalError,
    Unavailable,
    DataLoss,
    Unauthenticated
}

/// <summary>
/// Represents an active Sentry transaction (performance trace span).
/// </summary>
public interface ITransaction : IDisposable
{
    /// <summary>
    /// Human-readable name (e.g. "GameTick").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Operation category (e.g. "gameloop.tick").
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Unique event identifier. Auto-generated.
    /// </summary>
    public SentryEventId EventId { get; }

    /// <summary>
    /// When the transaction started (UTC).
    /// </summary>
    public DateTimeOffset StartTimestamp { get; }

    /// <summary>
    /// When Finish() was called, or null if still running.
    /// </summary>
    public DateTimeOffset? EndTimestamp { get; }

    /// <summary>
    /// The completion status.
    /// </summary>
    public SpanStatus Status { get; }

    /// <summary>
    /// The release version.
    /// </summary>
    public string? Release { get; set; }

    /// <summary>
    /// Finish the transaction, recording its duration. Only enqueues for upload if sampled.
    /// </summary>
    void Finish(SpanStatus status = SpanStatus.Ok);

    /// <summary>
    /// Duration of the transaction in seconds.
    /// </summary>
    double DurationSeconds { get; }

    void IDisposable.Dispose()
    {
        Finish();
    }
}
