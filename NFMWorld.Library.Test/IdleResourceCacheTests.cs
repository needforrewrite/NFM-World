using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class IdleResourceCacheTests
{
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }

    private sealed class Resource
    {
        public int Disposals;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }

    [TestMethod]
    public void ActiveResourcesSurviveExpiryAndClearingUntilEveryOwnerReleasesThem()
    {
        var clock = new Clock();
        var cache = new IdleResourceCache<string, Resource>(
            0,
            TimeSpan.FromMinutes(2),
            resource => resource.Dispose(),
            StringComparer.OrdinalIgnoreCase,
            clock
        );
        var first = cache.Acquire("stage", _ => new Resource());
        var resource = first.Value;
        Assert.IsTrue(cache.TryAcquire("STAGE", out var second));
        clock.Advance(TimeSpan.FromHours(1));
        cache.Trim(clearUnused: true);
        first.Dispose();
        first.Dispose();
        cache.Trim(clearUnused: true);
        Assert.AreSame(resource, second!.Value);
        Assert.AreEqual(0, resource.Disposals);

        second.Dispose();
        cache.Trim(clearUnused: true);
        cache.Trim(clearUnused: true);
        Assert.AreEqual(1, resource.Disposals);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void IdleExpiryStartsWhenTheLastOwnerReleasesTheResource()
    {
        var clock = new Clock();
        var cache = new IdleResourceCache<string, Resource>(
            4,
            TimeSpan.FromMinutes(2),
            resource => resource.Dispose(),
            clock: clock
        );
        var lease = cache.Acquire("stage", _ => new Resource());
        var resource = lease.Value;
        clock.Advance(TimeSpan.FromMinutes(10));
        lease.Dispose();
        clock.Advance(TimeSpan.FromSeconds(119));
        cache.Trim();
        Assert.AreEqual(0, resource.Disposals);
        clock.Advance(TimeSpan.FromSeconds(1));
        cache.Trim();
        Assert.AreEqual(1, resource.Disposals);
    }

    [TestMethod]
    public void CapacityEvictsTheOldestUnusedResourceWithoutTouchingActiveOnes()
    {
        var clock = new Clock();
        var cache = new IdleResourceCache<string, Resource>(
            1,
            TimeSpan.FromHours(1),
            resource => resource.Dispose(),
            clock: clock
        );
        using var active = cache.Acquire("active", _ => new Resource());
        var oldestLease = cache.Acquire("old", _ => new Resource());
        var oldest = oldestLease.Value;
        oldestLease.Dispose();
        clock.Advance(TimeSpan.FromSeconds(1));
        var newestLease = cache.Acquire("new", _ => new Resource());
        var newest = newestLease.Value;
        newestLease.Dispose();
        cache.Trim();
        Assert.AreEqual(1, oldest.Disposals);
        Assert.AreEqual(0, newest.Disposals);
        Assert.AreEqual(0, active.Value.Disposals);
        Assert.AreEqual(2, cache.Count);
    }

    [TestMethod]
    public void ConcurrentAcquisitionAndTrimmingKeepOneSharedLiveResource()
    {
        var cache = new IdleResourceCache<string, Resource>(0, TimeSpan.Zero, resource => resource.Dispose());
        var creates = 0;

        Resource Create(string _)
        {
            Interlocked.Increment(ref creates);

            return new Resource();
        }

        var root = cache.Acquire("stage", Create);
        var resource = root.Value;
        Parallel.For(
            0,
            128,
            _ =>
            {
                using var lease = cache.Acquire("stage", Create);
                cache.Trim(clearUnused: true);
                Assert.AreSame(resource, lease.Value);
                Assert.AreEqual(0, resource.Disposals);
            }
        );
        root.Dispose();
        cache.Trim();
        Assert.AreEqual(1, creates);
        Assert.AreEqual(1, resource.Disposals);
    }

    [TestMethod]
    public void ManagedModelEvictionDropsOnlyTheCacheReference()
    {
        var cache = new IdleResourceCache<string, Resource>(1, TimeSpan.FromMinutes(5));
        var activeModel = cache.GetOrAdd("first", _ => new Resource());
        cache.GetOrAdd("second", _ => new Resource());
        Assert.AreEqual(1, cache.Count);
        Assert.AreEqual(0, activeModel.Disposals);
        cache.Trim(clearUnused: true);
        Assert.AreEqual(0, cache.Count);
        Assert.AreEqual(0, activeModel.Disposals);
    }

    [TestMethod]
    public void DisposableResourcesCannotBeBorrowedWithoutALease()
    {
        var cache = new IdleResourceCache<string, Resource>(1, TimeSpan.Zero, resource => resource.Dispose());
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("stage", _ => new Resource()));
        Assert.Throws<InvalidOperationException>(() => cache.TryGetValue("stage", out _));
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void FailedCreationDoesNotLeaveAPinnedEntry()
    {
        var cache = new IdleResourceCache<string, Resource>(1, TimeSpan.Zero, resource => resource.Dispose());
        Assert.Throws<InvalidOperationException>(() => cache.Acquire("stage", _ => throw new InvalidOperationException()));
        Assert.AreEqual(0, cache.Count);
        using var lease = cache.Acquire("stage", _ => new Resource());
        Assert.AreEqual(1, cache.Count);
    }

    [TestMethod]
    public void FailedDisposalDoesNotSkipTheOtherEvictedResources()
    {
        Resource? failing = null;
        var cache = new IdleResourceCache<string, Resource>(
            1,
            TimeSpan.Zero,
            resource =>
            {
                resource.Dispose();

                if (ReferenceEquals(resource, failing))

                    throw new InvalidOperationException("simulated disposal failure");
            }
        );
        var first = cache.Acquire("first", _ => new Resource());
        var second = cache.Acquire("second", _ => new Resource());
        failing = first.Value;
        var other = second.Value;
        first.Dispose();
        second.Dispose();
        Assert.Throws<AggregateException>(() => cache.Trim(clearUnused: true));
        Assert.AreEqual(1, failing.Disposals);
        Assert.AreEqual(1, other.Disposals);
        Assert.AreEqual(0, cache.Count);
    }
}
