using System.Diagnostics;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Periodic health line for the lobby. It runs on its own thread, not the pool, so a starved or
/// wedged pool still produces a line, and the last line before a hang shows what state it was in.
/// </summary>
public static class LobbyHeartbeat
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>A pool that takes longer than this to start one work item is starved.</summary>
    private static readonly TimeSpan SlowPoolThreshold = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan PoolProbeTimeout = TimeSpan.FromSeconds(10);

    public static void Start(IMultiplayerServerTransport transport, CancellationToken ct)
    {
        new Thread(() => Run(transport, ct)) { IsBackground = true, Name = "Lobby heartbeat" }.Start();
    }

    private static void Run(IMultiplayerServerTransport transport, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Report(transport);
            }
            catch (Exception ex)
            {
                // An unhandled exception on a thread ends the process.
                Logging.Error($"[Lobby] Heartbeat failed", exception: ex);
            }

            if (ct.WaitHandle.WaitOne(Interval))
                break;
        }
    }

    private static void Report(IMultiplayerServerTransport transport)
    {
        using var ran = new ManualResetEventSlim();
        var started = Stopwatch.GetTimestamp();
        ThreadPool.UnsafeQueueUserWorkItem(static state => ((ManualResetEventSlim)state!).Set(), ran, preferLocal: false);
        var poolRan = ran.Wait(PoolProbeTimeout);
        var poolLatency = Stopwatch.GetElapsedTime(started);

        var line =
            $"[Lobby] heartbeat: connections={transport.Connections.Count} "
            + $"poolThreads={ThreadPool.ThreadCount} poolPending={ThreadPool.PendingWorkItemCount} "
            + $"poolLatencyMs={(poolRan ? poolLatency.TotalMilliseconds.ToString("F0") : $">{PoolProbeTimeout.TotalMilliseconds:F0}")} "
            + $"gen2={GC.CollectionCount(2)} memMB={GC.GetTotalMemory(false) / (1024 * 1024)}";

        if (!poolRan || poolLatency > SlowPoolThreshold)
            Logging.Warning(line + " (thread pool is starved)");
        else
            Logging.Info(line);
    }
}
