using NFMWorldLibrary;

namespace NFMWorld;

internal static class ContentCacheMaintenance
{
    private static readonly HashSet<IDisposable> Pending = new(ReferenceEqualityComparer.Instance);
    private static long _lastTrim;

    private sealed class Registration(IDisposable resource) : IDisposable
    {
        public void Dispose() => Pending.Remove(resource);
    }

    public static IDisposable TrackPending(IDisposable resource)
    {
        Pending.Add(resource);

        return new Registration(resource);
    }

    public static void Update()
    {
        var now = System.Environment.TickCount64;

        if (now - _lastTrim < 1000)

            return;
        _lastTrim = now;
        Clean(() => StageMeshCache.Trim());
        Clean(() => CarThumbnails.TrimUnused());
        Clean(() => BackendGameSparker.TrimContentCaches());
    }

    public static void Shutdown()
    {
        foreach (var resource in Pending.ToArray())
            Clean(resource.Dispose);
        Pending.Clear();
        Clean(() => StageMeshCache.Trim(clearUnused: true));
        Clean(() => CarThumbnails.TrimUnused(clear: true));
        Clean(() => BackendGameSparker.TrimContentCaches(clearUnused: true));
        LoadingWork.Shutdown();
    }

    private static void Clean(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            Logging.Warning($"Content cleanup failed: {ex.Message}");
        }
    }
}
