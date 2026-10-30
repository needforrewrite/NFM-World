using SignalsDotnet;

namespace NFMWorld.Reactor.Sx;

/// <summary>
/// Runs signal effects once per frame instead of once per write.
/// </summary>
/// <remarks>
/// <see cref="Effect"/> schedules through a <see cref="TimeProvider"/>; this one never
/// fires a timer itself. It records that a flush is due and lets the host call
/// <see cref="Flush"/> at the end of the frame, so every write made during a frame -
/// including the per-tick HUD updates - coalesces into one effect pass.
///
/// Install once at startup: <c>Effect.DefaultScheduler = new UiScheduler();</c>. Effects
/// created before that runs would schedule against the default provider and re-run
/// immediately per write, so this has to happen before the first component mounts.
/// </remarks>
public sealed class UiScheduler : TimeProvider
{
    private static UiScheduler? _installed;
    private readonly List<(TimerCallback Callback, object? State)> _pending = [];

    public static UiScheduler Instance => _installed ??= Install();

    private static UiScheduler Install()
    {
        var scheduler = new UiScheduler();
        Effect.DefaultScheduler = scheduler;
        return scheduler;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        // The effect machinery only ever creates one-shot timers with a zero/negative due
        // time, i.e. "run at the end of this flush". Anything else would be a periodic
        // timer, which nothing in the UI needs.
        _pending.Add((callback, state));
        return NoOpTimer.Instance;
    }

    /// <summary>Runs every effect that a write since the last flush invalidated.</summary>
    public void Flush()
    {
        if (_pending.Count == 0)
            return;

        // Effects can write signals of their own, which queues more callbacks; drain until
        // quiet so a frame never ends with work outstanding.
        for (var pass = 0; pass < 100 && _pending.Count > 0; pass++)
        {
            var batch = _pending.ToArray();
            _pending.Clear();

            foreach (var (callback, state) in batch)
                callback(state);
        }
    }

    private sealed class NoOpTimer : ITimer
    {
        public static readonly NoOpTimer Instance = new();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
