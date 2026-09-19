namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Helper class to track leaks. Stores the stack trace of creation which lets you easily find where your leaky objects
/// are created. Make sure to suppress the finalizer on dispose.
/// </summary>
internal abstract class TrackLeaks : IDisposable
{
#if DEBUG
    private string _stack = Environment.StackTrace;
#endif

    ~TrackLeaks()
    {
        Dispose();
        
#if DEBUG
        Console.WriteLine($"Warning: {GetType().Name} was not disposed before being finalized. This may indicate a resource leak.\n{_stack}");
#endif
    }

    public abstract void Dispose();
}