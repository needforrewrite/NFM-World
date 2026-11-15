using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SignalsDotnet;

namespace NFMWorld;

internal static class LoadingWork
{
    private static readonly ConcurrentQueue<Action> Pending = new();
    private static readonly object QueueGate = new();
    private static readonly Queue<Action> Ready = new();
    private static readonly ConcurrentDictionary<long, string> Operations = new();
    private static long _nextId;
    private static volatile bool _stopped;
    public static Signal<string> Status { get; } = new("");
    public static long FrameId { get; private set; }

    public sealed class Operation(string label) : IDisposable
    {
        private readonly long _id = Interlocked.Increment(ref _nextId);

        public void Report(string label)
        {
            if (!_stopped) Operations[_id] = label;
        }

        public void Dispose() => Operations.TryRemove(_id, out _);

        public Operation Start()
        {
            Report(label);
            return this;
        }
    }

    public static Operation Begin(string label) => new Operation(label).Start();

    public readonly struct FrameAwaiter : INotifyCompletion
    {
        public bool IsCompleted => false;
        public FrameAwaiter GetAwaiter() => this;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            lock (QueueGate) if (!_stopped) Pending.Enqueue(continuation);
        }
    }

    public static FrameAwaiter NextFrame => default;

    public static void Update()
    {
        FrameId++;
        var count = Ready.Count;
        var timer = Stopwatch.StartNew();
        while (count-- > 0 && timer.ElapsedMilliseconds < 4 && Ready.TryDequeue(out var action)) action();
        var incoming = Pending.Count;
        // Render the indicator before running newly queued uploads.
        while (incoming-- > 0 && Pending.TryDequeue(out var incomingAction)) Ready.Enqueue(incomingAction);
        Status.Value = string.Join(" · ", Operations.OrderBy(pair => pair.Key).Select(pair => pair.Value).Distinct());
    }

    public static void Shutdown()
    {
        lock (QueueGate)
        {
            _stopped = true;
            Pending.Clear();
            Ready.Clear();
            Operations.Clear();
        }
    }
}
