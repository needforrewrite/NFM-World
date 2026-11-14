namespace NFMWorldLibrary.Util;

public sealed class SerialWorkQueue
{
    private readonly object _sync = new();
    private Task _tail = Task.CompletedTask;

    public Task<T> Enqueue<T>(Func<T> work)
    {
        lock (_sync)
        {
            var task = _tail.ContinueWith(
                _ => work(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default
            );
            _tail = task.ContinueWith(
                completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            return task;
        }
    }

    public Task WhenIdle()
    {
        lock (_sync)
            return _tail;
    }
}
