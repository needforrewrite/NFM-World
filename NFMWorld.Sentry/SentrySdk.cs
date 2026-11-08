using System.Diagnostics;

namespace NFMWorld.Sentry;

/// <summary>
/// Main entry point for the NFMWorld Sentry SDK. Provides a static API surface
/// compatible with the official Sentry SDK for drop-in replacement.
/// </summary>
public static class SentrySdk
{
    private static SentryOptions? _options;
    private static SentryUploader? _uploader;
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    // Breadcrumb ring buffer
    private static Breadcrumb[]? _breadcrumbs;
    private static int _breadcrumbIndex;
    private static int _breadcrumbCount;

    /// <summary>
    /// Initialize the Sentry SDK with the given configuration.
    /// Must be called once before any other methods.
    /// </summary>
    public static void Init(Action<SentryOptions> configure)
    {
        lock (InitLock)
        {
            if (_initialized)
                throw new InvalidOperationException("SentrySdk.Init has already been called");

            var options = new SentryOptions();
            configure(options);

            if (string.IsNullOrWhiteSpace(options.Dsn))
                throw new ArgumentException("Sentry DSN must be configured");

            _options = options;
            _uploader = new SentryUploader(options);
            _breadcrumbs = options.MaxBreadcrumbs > 0 ? new Breadcrumb[options.MaxBreadcrumbs] : null;
            _breadcrumbIndex = 0;
            _breadcrumbCount = 0;
            _initialized = true;

            if (options.Debug)
                Debug.WriteLine(
                    $"[Sentry] Initialized. DSN: {options.ParsedDsn?.EnvelopeUri}, Release: {options.Release ?? "none"}"
                );
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            lock (InitLock)
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException("SentrySdk.Init must be called before using SentrySdk");
                }
            }
        }
    }

    /// <summary>
    /// Capture an exception and send it to Sentry.
    /// Also records the exception as a breadcrumb.
    /// </summary>
    public static SentryEventId CaptureException(Exception ex, SentryLevel level = SentryLevel.Error)
    {
        EnsureInitialized();

        // Record breadcrumb first
        AddBreadcrumb(Breadcrumb.FromException(ex, level));

        var evt = new SentryEvent(ex)
        {
            Level = level,
            Release = _options!.Release,
            Breadcrumbs = SnapshotBreadcrumbs()
        };

        _uploader!.Enqueue(new EventItem(evt));
        return evt.EventId;
    }

    /// <summary>
    /// Capture a message and send it to Sentry.
    /// Also records the message as a breadcrumb.
    /// </summary>
    public static SentryEventId CaptureMessage(string message, SentryLevel level = SentryLevel.Info)
    {
        EnsureInitialized();

        // Record breadcrumb first
        AddBreadcrumb(Breadcrumb.FromMessage(message, level));

        var evt = new SentryEvent
        {
            Message = message,
            Level = level,
            Release = _options!.Release,
            Breadcrumbs = SnapshotBreadcrumbs()
        };

        _uploader!.Enqueue(new EventItem(evt));
        return evt.EventId;
    }

    /// <summary>
    /// Capture a manually constructed event and send it to Sentry.
    /// Automatically attaches current breadcrumbs if none are set.
    /// </summary>
    public static SentryEventId CaptureEvent(SentryEvent evt)
    {
        EnsureInitialized();

        evt.Release ??= _options!.Release;
        evt.Breadcrumbs ??= SnapshotBreadcrumbs();
        _uploader!.Enqueue(new EventItem(evt));
        return evt.EventId;
    }

    /// <summary>
    /// Add a breadcrumb to the global breadcrumb ring buffer.
    /// Breadcrumbs are automatically attached to subsequent events.
    /// Thread-safe.
    /// </summary>
    public static void AddBreadcrumb(Breadcrumb breadcrumb)
    {
        var buffer = _breadcrumbs;
        if (buffer is null || buffer.Length == 0)
            return;

        var index = Interlocked.Increment(ref _breadcrumbIndex) - 1;
        var wrappedIndex = index % buffer.Length;

        buffer[wrappedIndex] = breadcrumb;

        // Track count up to buffer size
        if (_breadcrumbCount < buffer.Length)
            Interlocked.Increment(ref _breadcrumbCount);
    }

    /// <summary>
    /// Take a snapshot of the current breadcrumb ring buffer.
    /// Returns null if breadcrumbs are disabled or empty.
    /// </summary>
    internal static IReadOnlyList<Breadcrumb>? SnapshotBreadcrumbs()
    {
        var buffer = _breadcrumbs;
        if (buffer is null || buffer.Length == 0)
            return null;

        var count = _breadcrumbCount;
        if (count == 0)
            return null;

        // Read the ring buffer in insertion order
        var startIndex = _breadcrumbIndex - count;
        var snapshot = new Breadcrumb[count];
        for (var i = 0; i < count; i++)
        {
            var srcIndex = ((startIndex + i) % buffer.Length + buffer.Length) % buffer.Length;
            snapshot[i] = buffer[srcIndex];
        }

        return snapshot;
    }

    /// <summary>
    /// Capture user feedback associated with a Sentry event.
    /// </summary>
    public static void CaptureFeedback(SentryFeedback feedback)
    {
        EnsureInitialized();

        _uploader!.Enqueue(new FeedbackItem(feedback));
    }

    /// <summary>
    /// Start a new transaction for performance tracing.
    /// Returns a no-op transaction if the trace is not sampled.
    /// </summary>
    public static SentryTransaction StartTransaction(string name, string operation)
    {
        EnsureInitialized();

        // Sampling check
        if (_options!.TracesSampleRate <= 0)
            return new SentryTransaction();

        if (_options.TracesSampleRate < 1.0)
        {
            if (Random.Shared.NextDouble() >= _options.TracesSampleRate)
                return new SentryTransaction();
        }

        return new SentryTransaction(name, operation);
    }

    /// <summary>
    /// A sampled Sentry transaction representing a measured operation span.
    /// </summary>
    public struct SentryTransaction : ITransaction
    {
        private readonly bool _noop;
        private readonly long _startTime;
        private TimeSpan? _elapsed;

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
        public DateTimeOffset? EndTimestamp { get; private set; }

        /// <summary>
        /// The completion status.
        /// </summary>
        public SpanStatus Status { get; private set; }

        /// <summary>
        /// The release version.
        /// </summary>
        public string? Release { get; set; }

        public SentryTransaction()
        {
            _noop = true;
            Name = "";
            Operation = "";
        }

        internal SentryTransaction(string name, string operation)
        {
            Name = name;
            Operation = operation;
            EventId = SentryEventId.NewId();
            StartTimestamp = DateTimeOffset.UtcNow;
            _startTime = Stopwatch.GetTimestamp();
            Release = _options!.Release;
        }

        /// <summary>
        /// Finish the transaction and enqueue it for upload.
        /// </summary>
        public void Finish(SpanStatus status = SpanStatus.Ok)
        {
            if (_noop)
                return;

            if (EndTimestamp.HasValue)
                return; // Already finished

            EndTimestamp = DateTimeOffset.UtcNow;
            _elapsed = Stopwatch.GetElapsedTime(_startTime);
            Status = status;

            // Only enqueue if still initialized (not disposed during shutdown)
            _uploader?.Enqueue(new TransactionItem(this));
        }

        /// <summary>
        /// Duration of the transaction in seconds.
        /// </summary>
        public double DurationSeconds => _elapsed?.TotalSeconds ?? 0;
    }

    /// <summary>
    /// Flush all pending events synchronously.
    /// </summary>
    public static void Flush(TimeSpan timeout)
    {
        _uploader?.Flush(timeout);
    }

    /// <summary>
    /// Flush all pending events asynchronously.
    /// </summary>
    public static Task FlushAsync(TimeSpan timeout)
    {
        if (_uploader is null)
            return Task.CompletedTask;
        return _uploader.FlushAsync(timeout);
    }

    /// <summary>
    /// The current Sentry options, or null if not initialized.
    /// </summary>
    public static SentryOptions? Options => _options;
}
