using System.Diagnostics;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// Debug-only check that a released gamemode plugin's assembly really is collected.
/// <see cref="System.Runtime.Loader.AssemblyLoadContext.Unload"/> is silent - a context
/// that something still references simply never collects - so a plugin that leaks looks
/// exactly like one that does not.
/// </summary>
/// <remarks>
/// This cannot run from <see cref="NativeGamemodeFactory.End"/>, where it belongs
/// logically: the frames up the stack to there are still live roots, so a collection
/// forced from inside the call always finds the assembly alive and the check ends up
/// reporting the caller's own reference. It runs from the host's loop instead, a frame
/// later, once every frame that touched the plugin has returned. Hosts therefore have to
/// call <see cref="Verify"/> once per frame (game) and per tick (server), or nothing is
/// ever checked.
/// </remarks>
public static class PluginUnloadVerifier
{
    private sealed record Pending(string GamemodeId, WeakReference Assembly, int Frame);

    private static readonly object Gate = new();
    private static readonly List<Pending> PendingUnloads = [];
    private static int _frame;

    /// <summary>Queues a released plugin to check once the current frame has unwound.</summary>
    [Conditional("DEBUG")]
    public static void Arm(string gamemodeId, WeakReference assembly)
    {
        lock (Gate)
        {
            PendingUnloads.Add(new Pending(gamemodeId, assembly, _frame));
        }
    }

    /// <summary>Drops every queued check, so tests do not inherit each other's.</summary>
    [Conditional("DEBUG")]
    public static void Reset()
    {
        lock (Gate)
        {
            PendingUnloads.Clear();
        }
    }

    /// <summary>
    /// Reports any armed plugin whose assembly is still resident. Call once per frame or
    /// tick, after that frame's teardown has run.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Verify()
    {
        List<Pending> ready;

        lock (Gate)
        {
            _frame++;

            // A plugin released during the frame that just ended is still reachable from
            // that frame's stack, so give it one more frame before believing it leaked.
            ready = PendingUnloads.Where(pending => pending.Frame < _frame - 1).ToList();

            foreach (var pending in ready)
                PendingUnloads.Remove(pending);
        }

        if (ready.Count == 0)
            return;

        Collect();

        // Entries are already dropped, so a leak is reported once rather than on every
        // frame that follows - the server's tick loop would otherwise log it endlessly.
        foreach (var pending in ready)
        {
            if (pending.Assembly.IsAlive)
            {
                throw new InvalidOperationException(
                    $"Gamemode plugin '{pending.GamemodeId}' was released but its assembly is still resident: " +
                    "something the race left behind still references it, so its load context can never unload.");
            }
        }
    }

    private static void Collect()
    {
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
