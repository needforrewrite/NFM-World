using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace NFMWorld.Sentry;

/// <summary>
/// Background uploader that reads events from a Channel, batches them, debounces duplicates,
/// serializes to Sentry envelope format, and POSTs to the DSN endpoint.
/// </summary>
public sealed class SentryUploader : IDisposable
{
    private readonly SentryOptions _options;
    private readonly SentryHttpTransport _transport;
    private readonly Channel<SentryEnvelopeItem> _channel;
    private readonly Thread? _uploadThread;
    private readonly CancellationTokenSource _cts;
    private readonly IReadOnlyList<SentryEnvelopeItem> _emptyBatch = [];

    // Debounce tracking: per-type last-enqueue time and accumulated items
    private readonly ConcurrentDictionary<string, (DateTimeOffset Time, SentryEnvelopeItem Item)> _debounceState = new();

    private volatile bool _disposed;

    public SentryUploader(SentryOptions options)
    {
        _options = options;
        _transport = new SentryHttpTransport(options);
        _channel = Channel.CreateUnbounded<SentryEnvelopeItem>(
            new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });
        _cts = new CancellationTokenSource();

        // The browser cannot start threads at all (Thread.Start throws), and there would be nothing
        // to upload from a page to this endpoint anyway - so the loop is simply never started there
        // and Enqueue's writes go into the channel unread.
        if (!OperatingSystem.IsBrowser())
        {
            _uploadThread = new Thread(UploadLoop)
            {
                Name = "SentryUploader",
                IsBackground = true
            };
            _uploadThread.Start();
        }
    }

    /// <summary>
    /// Enqueue an envelope item for upload. Thread-safe.
    /// </summary>
    public void Enqueue(SentryEnvelopeItem item)
    {
        if (_disposed)
            return;

        // Debounce: coalesce same-type items within the debounce window
        if (_options.DebounceWindow > TimeSpan.Zero)
        {
            var now = DateTimeOffset.UtcNow;

            if (_debounceState.TryGetValue(item.ItemType, out var existing))
            {
                _debounceState.TryUpdate(item.ItemType, (now, item), existing);
                
                if (now - existing.Time < _options.DebounceWindow)
                {
                    if (_options.Debug)
                        Debug.WriteLine($"[Sentry] Debounced {item.ItemType} event");
                    return;
                }
            }
            else
            {
                _debounceState[item.ItemType] = (now, item);
            }
        }

        if (!_channel.Writer.TryWrite(item))
        {
            if (_options.Debug)
                Debug.WriteLine("[Sentry] Failed to enqueue event: channel full or completed");
        }
    }

    /// <summary>
    /// Flush all pending events. Blocks until the queue is drained or the timeout expires.
    /// </summary>
    public void Flush(TimeSpan timeout)
    {
        if (_options.Debug)
            Debug.WriteLine("[Sentry] Flush started");

        // First drain any debounced items
        DrainDebounced();

        // Signal completion and wait for the upload thread to finish
        _channel.Writer.TryComplete();

        if (_uploadThread is not null && !_uploadThread.Join(timeout))
        {
            if (_options.Debug)
                Debug.WriteLine("[Sentry] Flush timed out");
        }

        if (_options.Debug)
            Debug.WriteLine("[Sentry] Flush complete");
    }

    /// <summary>
    /// Flush all pending events asynchronously.
    /// </summary>
    public Task FlushAsync(TimeSpan timeout)
    {
        return Task.Run(() => Flush(timeout));
    }

    private void UploadLoop()
    {
        try
        {
            var reader = _channel.Reader;
            var ct = _cts.Token;

            while (!ct.IsCancellationRequested)
            {
                var batch = ReadBatch(reader, ct);
                if (batch.Count == 0)
                    break; // Channel completed

                ProcessBatch(batch, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (_options.Debug)
                Debug.WriteLine($"[Sentry] Upload loop error: {ex}");
        }
        finally
        {
            // Drain any remaining items after channel completion
            try
            {
                while (_channel.Reader.TryRead(out var item))
                {
                    ProcessBatch([item], CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                if (_options.Debug)
                    Debug.WriteLine($"[Sentry] Final drain error: {ex}");
            }
        }
    }

    private List<SentryEnvelopeItem> ReadBatch(ChannelReader<SentryEnvelopeItem> reader, CancellationToken ct)
    {
        var batch = new List<SentryEnvelopeItem>(_options.MaxBatchSize);

        // Wait for the first item
        if (!reader.TryRead(out var first))
        {
            // Try to wait asynchronously
            var waitTask = reader.WaitToReadAsync(ct).AsTask();
            waitTask.Wait(ct);
            if (!reader.TryRead(out first))
                return _emptyBatch.ToList(); // Channel completed
        }

        batch.Add(first);

        // Drain any immediately available items up to MaxBatchSize
        while (batch.Count < _options.MaxBatchSize && reader.TryRead(out var item))
        {
            batch.Add(item);
        }

        // If batch is not full, wait for MaxBatchDelay for more items
        if (batch.Count < _options.MaxBatchSize)
        {
            var deadline = DateTimeOffset.UtcNow + _options.MaxBatchDelay;
            while (DateTimeOffset.UtcNow < deadline && batch.Count < _options.MaxBatchSize)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                if (reader.TryRead(out var item))
                {
                    batch.Add(item);
                }
                else
                {
                    // Brief sleep to avoid busy-waiting
                    Thread.Sleep(Math.Min(50, (int)remaining.TotalMilliseconds));
                }
            }
        }

        return batch;
    }

    private void ProcessBatch(List<SentryEnvelopeItem> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        try
        {
            if (_options.ParsedDsn is not { } dsn)
            {
                if (_options.Debug)
                    Debug.WriteLine("[Sentry] No valid DSN configured, dropping events");
                return;
            }

            var envelopeBytes = EnvelopeSerializer.Serialize(
                batch, dsn, DateTimeOffset.UtcNow, _options.Release);

            var error = _transport.SendEnvelopeAsync(envelopeBytes, dsn, ct)
                .GetAwaiter().GetResult();

            if (error is not null && _options.Debug)
                Debug.WriteLine($"[Sentry] Upload error: {error}");
        }
        catch (Exception ex)
        {
            if (_options.Debug)
                Debug.WriteLine($"[Sentry] Batch processing error: {ex.Message}");
        }
    }

    private void DrainDebounced()
    {
        foreach (var (_, item) in _debounceState.Values)
        {
            _channel.Writer.TryWrite(item);
            _debounceState.TryRemove(item.ItemType, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _cts.Cancel();
        _transport.Dispose();
        _cts.Dispose();
    }
}
