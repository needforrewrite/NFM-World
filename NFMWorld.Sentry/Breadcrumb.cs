namespace NFMWorld.Sentry;

/// <summary>
/// Severity level for breadcrumbs.
/// </summary>
public enum BreadcrumbLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// A breadcrumb representing a trail of events leading up to a Sentry event.
/// Thread-safe for concurrent writes.
/// </summary>
public readonly struct Breadcrumb
{
    /// <summary>
    /// When the breadcrumb was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// A human-readable message describing this breadcrumb.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// The category of the breadcrumb (e.g. "log", "navigation", "http").
    /// </summary>
    public string Category { get; init; }

    /// <summary>
    /// The severity level of this breadcrumb.
    /// </summary>
    public BreadcrumbLevel Level { get; init; }

    /// <summary>
    /// The type of the breadcrumb (e.g. "default", "http", "error").
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Optional key-value data attached to this breadcrumb.
    /// </summary>
    public Dictionary<string, string>? Data { get; init; }

    /// <summary>
    /// Create a breadcrumb from a log message (category: "log").
    /// </summary>
    public static Breadcrumb FromMessage(string message, SentryLevel level)
    {
        return new Breadcrumb
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = message,
            Category = "log",
            Level = SentryLevelToBreadcrumbLevel(level),
            Type = "default"
        };
    }

    /// <summary>
    /// Create a breadcrumb from an exception.
    /// </summary>
    public static Breadcrumb FromException(Exception ex, SentryLevel level)
    {
        return new Breadcrumb
        {
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"{ex.GetType().Name}: {ex.Message}",
            Category = "error",
            Level = SentryLevelToBreadcrumbLevel(level),
            Type = "error",
            Data = new Dictionary<string, string>
            {
                ["exception.type"] = ex.GetType().FullName ?? ex.GetType().Name,
                ["exception.message"] = ex.Message
            }
        };
    }

    private static BreadcrumbLevel SentryLevelToBreadcrumbLevel(SentryLevel level) => level switch
    {
        SentryLevel.Debug => BreadcrumbLevel.Debug,
        SentryLevel.Info => BreadcrumbLevel.Info,
        SentryLevel.Warning => BreadcrumbLevel.Warning,
        SentryLevel.Error => BreadcrumbLevel.Error,
        SentryLevel.Fatal => BreadcrumbLevel.Critical,
        _ => BreadcrumbLevel.Info
    };
}
