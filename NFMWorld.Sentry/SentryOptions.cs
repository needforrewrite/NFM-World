namespace NFMWorld.Sentry;

/// <summary>
/// Configuration options for the NFMWorld Sentry client.
/// </summary>
public class SentryOptions
{
    private DsnInfo? _dsnInfo;
    private string? _dsn;

    /// <summary>
    /// The Data Source Name (DSN) for the Sentry project.
    /// </summary>
    public string? Dsn
    {
        get => _dsn;
        set
        {
            _dsn = value;
            _dsnInfo = value is not null ? DsnInfo.Parse(value) : null;
        }
    }

    /// <summary>
    /// Parsed DSN information. Null if DSN is not set.
    /// </summary>
    public DsnInfo? ParsedDsn => _dsnInfo;

    /// <summary>
    /// Enable debug logging to the console. Default false.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// The release version string. Sent with every event.
    /// </summary>
    public string? Release { get; set; }

    /// <summary>
    /// Sample rate for transactions (0.0 = none, 1.0 = all). Default 0.0.
    /// </summary>
    public double TracesSampleRate { get; set; }

    /// <summary>
    /// Maximum number of events to batch in a single envelope. Default 10.
    /// </summary>
    public int MaxBatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum time to wait before sending a partial batch. Default 2 seconds.
    /// </summary>
    public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Debounce window for coalescing same-type events. Default 500ms.
    /// </summary>
    public TimeSpan DebounceWindow { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// HTTP request timeout. Default 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of breadcrumbs to keep in the ring buffer. Default 100.
    /// Set to 0 to disable breadcrumbs entirely.
    /// </summary>
    public int MaxBreadcrumbs { get; set; } = 100;
}
