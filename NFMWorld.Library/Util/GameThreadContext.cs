using System.Collections.Concurrent;
using System.Diagnostics;
using JetBrains.Profiler.Api;

namespace NFMWorldLibrary.Util;

public class GameThreadContext : SynchronizationContext
{
    private sealed class SynchronizationContextTaskScheduler : TaskScheduler
    {
        private readonly GameThreadContext _synchronizationContext;

        internal SynchronizationContextTaskScheduler(GameThreadContext context)
        {
            _synchronizationContext = context;
        }

        protected override void QueueTask(Task task)
        {
            _synchronizationContext.Post(SendOrPostCallback, task);
        }

        private void SendOrPostCallback(object? task)
        {
            TryExecuteTask((Task)task!);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (SynchronizationContext.Current == _synchronizationContext)
            {
                return TryExecuteTask(task);
            }
            else
            {
                return false;
            }
        }

        protected override IEnumerable<Task>? GetScheduledTasks()
        {
            // We cannot access the tasks in the ConcurrentQueue when other threads are frozen.
            return null;
        }

        public override int MaximumConcurrencyLevel => 1;
    }

    private readonly ConcurrentQueue<(SendOrPostCallback d, object? state)> _tasks = new();
    private readonly Thread _gameThread;

    private SynchronizationContextTaskScheduler Scheduler { get; }

    public new static GameThreadContext Current
    {
        get
        {
            if (SynchronizationContext.Current is not GameThreadContext context)
            {
                throw new InvalidOperationException("Current SynchronizationContext is not a GameThreadContext.");
            }

            return context;
        }
    }

    public GameThreadContext(Thread gameThread)
    {
        _gameThread = gameThread;
        Scheduler = new(this);
    }

    [ThreadStatic] private static GameThreadContext? _threadContext;

    /// <summary>
    /// Makes this thread's context the ambient one, and returns it.
    ///
    /// Called once at boot and again at the top of every frame, because the context is not durable:
    /// on wasm the ThreadPool runs its work on the main thread and the runtime's pool pump leaves
    /// <see cref="SynchronizationContext.Current"/> null behind it - so a single queued work item
    /// (any `Task.Run`, which the content loaders use) silently drops it and the next read of
    /// <see cref="Current"/> throws.
    /// </summary>
    public static GameThreadContext EnsureInstalled()
    {
        if (SynchronizationContext.Current is GameThreadContext present)
        {
            _threadContext = present;
            return present;
        }

        // Reused rather than recreated: it owns the queue of posts the frame drains, so losing it
        // once must not strand work already queued on the previous instance.
        var context = _threadContext ??= new GameThreadContext(Thread.CurrentThread);
        SetSynchronizationContext(context);
        return context;
    }

    public void Run(Func<Task> action, Action callback)
    {
        AssertOnGameThread();

        var task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, Scheduler).Unwrap();
        task.ContinueWith(
            t =>
            {
                AssertOnGameThread();

                callback();
            },
            CancellationToken.None,
            TaskContinuationOptions.DenyChildAttach,
            Scheduler
        );
    }

    public void Run<T>(Func<Task<T>> action, Action<T> callback)
    {
        AssertOnGameThread();

        var task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, Scheduler).Unwrap();
        task.ContinueWith(
            t =>
            {
                AssertOnGameThread();

                callback(t.Result);
            },
            CancellationToken.None,
            TaskContinuationOptions.DenyChildAttach,
            Scheduler
        );
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _tasks.Enqueue((d, state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId == _gameThread.ManagedThreadId)
        {
            d(state);
        }
        else
        {
            throw new InvalidOperationException("Cannot send to game thread from another thread.");
        }
    }

    [Conditional("DEBUG")]
    private void AssertOnGameThread()
    {
        Debug.Assert(Environment.CurrentManagedThreadId == _gameThread.ManagedThreadId);
    }

    public bool ExecutePendingTasks()
    {
        AssertOnGameThread();

        var stopwatch = Stopwatch.StartNew();
        var hasTasks = _tasks.Count > 0;
        // The profiler's platform detection does not know wasm and throws on the first touch, which
        // is every task batch, so it is desktop-only.
        var profile = hasTasks && !OperatingSystem.IsBrowser();
        if (profile)
        {
            MeasureProfiler.StartCollectingData();
        }

        while (_tasks.TryDequeue(out var task))
        {
            task.d(task.state);
        }

        if (hasTasks)
        {
            if (profile)
            {
                MeasureProfiler.StopCollectingData();
                MeasureProfiler.SaveData("ExecutePendingTasks");
            }

            Logging.Debug($"Pending tasks: {stopwatch.Elapsed}");
            GameThreadContextDiagnostics.Record(stopwatch.ElapsedTicks);
        }

        return hasTasks;
    }
}
