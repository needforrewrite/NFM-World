using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class GameThreadContextTests
{
    /// <summary>
    /// On wasm the runtime's ThreadPool runs work on the game thread and leaves
    /// <see cref="SynchronizationContext.Current"/> null behind it, so the context is installed once
    /// per frame rather than once at boot. The re-install has to hand back the same instance: it
    /// owns the queue of posts the frame drains, and a fresh one would strand work queued before
    /// the pool dropped the old one.
    /// </summary>
    [TestMethod]
    public void EnsureInstalled_AfterTheContextIsDropped_ReinstallsTheSameInstance()
    {
        var previous = SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            var first = GameThreadContext.EnsureInstalled();

            var ran = false;
            first.Post(_ => ran = true, null);

            // What the wasm ThreadPool pump leaves behind after any work item.
            SynchronizationContext.SetSynchronizationContext(null);
            var second = GameThreadContext.EnsureInstalled();

            Assert.AreSame(first, second);
            Assert.AreSame(first, SynchronizationContext.Current);
            Assert.IsTrue(second.ExecutePendingTasks(), "the post made before the context was dropped should still be queued");
            Assert.IsTrue(ran);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
