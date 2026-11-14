using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class SerialWorkQueueTests
{
    [TestMethod]
    public void Enqueue_RunsOffTheCallerThread_AndPreservesSaveOrder()
    {
        var queue = new SerialWorkQueue();
        var caller = Environment.CurrentManagedThreadId;
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = new List<int>();
        var first = queue.Enqueue(() =>
            {
                started.Set();
                release.Wait();
                writes.Add(1);
                return Environment.CurrentManagedThreadId;
            }
        );
        try
        {
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)));
            var second = queue.Enqueue(() => { writes.Add(2); return 2; });
            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            release.Set();
            Assert.AreNotEqual(caller, first.GetAwaiter().GetResult());
            Assert.AreEqual(2, second.GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { 1, 2 }, writes);
        }
        finally
        {
            release.Set();
        }
    }

    [TestMethod]
    public void FailedSave_DoesNotPreventTheNextSave()
    {
        var queue = new SerialWorkQueue();
        var failed = queue.Enqueue<int>(() => throw new IOException("Write failed"));
        var next = queue.Enqueue(() => 42);
        Assert.ThrowsExactly<IOException>(() => failed.GetAwaiter().GetResult());
        Assert.AreEqual(42, next.GetAwaiter().GetResult());
        queue.WhenIdle().GetAwaiter().GetResult();
    }

    [TestMethod]
    [DataRow("write-root")]
    [DataRow("directory-mount")]
    [DataRow("async-read")]
    public void AtomicSave_KeepsAnOpenReaderOnThePreviousCompleteRecording(string readPath)
    {
        var fs = new NFMWorld.PhysicsFS.VirtualFS();
        fs.MountWriteDestination(Path.Combine(Path.GetTempPath(), "nfmw-atomic-replay", Guid.NewGuid().ToString("N")));
        if (readPath == "directory-mount")
            fs.MountDirectory(fs.WriteRoot!);
        const string file = "data/tts/run.timetrial";
        fs.WriteAllBytesAtomic(file, [1, 2, 3]);
        using var previous = readPath == "async-read"
            ? fs.OpenReadAsync(file).GetAwaiter().GetResult()
            : fs.OpenRead(file);
        fs.WriteAllBytesAtomic(file, [4, 5, 6]);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, fs.ReadAllBytes(file));
        using var copy = new MemoryStream();
        previous.CopyTo(copy);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, copy.ToArray());
        Assert.AreEqual(1, fs.EnumerateWriteFiles("data/tts").Count());
    }
}
