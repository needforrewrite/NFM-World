using System.Collections.Concurrent;
using System.Diagnostics;

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

    public static void Install()
    {
        var context = new GameThreadContext(Thread.CurrentThread);
        SetSynchronizationContext(context);
    }
    
    public void Run(Func<Task> action, Action callback)
    {
        AssertOnGameThread();

        var task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, Scheduler).Unwrap();
        task.ContinueWith(t =>
        {
            AssertOnGameThread();

            callback();
        }, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, Scheduler);
    }

    public void Run<T>(Func<Task<T>> action, Action<T> callback)
    {
        AssertOnGameThread();

        var task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, Scheduler).Unwrap();
        task.ContinueWith(t =>
        {
            AssertOnGameThread();

            callback(t.Result);
        }, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, Scheduler);
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

    public void ExecutePendingTasks()
    {
        AssertOnGameThread();

        while (_tasks.TryDequeue(out var task))
        {
            task.d(task.state);
        }
    }
}