using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Library.Test;

[TestClass]
public class RaceWastedTrackerTests
{
    [TestMethod]
    public void DuplicateAndOutOfOrderStates_DoNotRepeatAnnouncements()
    {
        var tracker = new RaceWastedTracker();
        var player = Guid.NewGuid();
        Assert.IsTrue(tracker.TryUpdate(player, 0, false, out var died));
        Assert.IsFalse(died);
        Assert.IsTrue(tracker.TryUpdate(player, 10, true, out died));
        Assert.IsTrue(died);
        Assert.IsFalse(tracker.TryUpdate(player, 10, true, out _));
        Assert.IsFalse(tracker.TryUpdate(player, 9, false, out _));
        Assert.IsTrue(tracker.TryUpdate(player, 11, true, out died));
        Assert.IsFalse(died);
        Assert.IsTrue(tracker.TryUpdate(player, 12, false, out died));
        Assert.IsFalse(died);
        Assert.IsTrue(tracker.TryUpdate(player, 13, true, out died));
        Assert.IsTrue(died, "a death after a repair is a new announcement");
    }

    [TestMethod]
    public void FirstSnapshotAlreadyWasted_IsStillAnnounced()
    {
        Assert.IsTrue(new RaceWastedTracker().TryUpdate(Guid.NewGuid(), 100, true, out var died));
        Assert.IsTrue(died);
    }

    [TestMethod]
    public void Credit_UsesRosterOrderRatherThanDictionaryOrderOrSparseSlotNumbers()
    {
        var attacker = Guid.NewGuid();
        var victim = Guid.NewGuid();
        var roster = new Dictionary<byte, ServerSidePlayerInfo>
        {
            [7] = new() { Id = victim, PlayerName = "Victim", CarName = "car", Color = default, IsBot = true },
            [2] = new() { Id = attacker, PlayerName = "Attacker", CarName = "car", Color = default }
        };
        Assert.AreEqual("Victim has been wasted by Attacker!", RaceWastedTracker.Message(roster, victim, 1));
        Assert.AreEqual("Victim has been wasted!", RaceWastedTracker.Message(roster, victim, 0));
        Assert.AreEqual("Victim has been wasted!", RaceWastedTracker.Message(roster, victim, 2));
        Assert.AreEqual("Victim has been wasted!", RaceWastedTracker.Message(roster, victim, 99));
        Assert.IsNull(RaceWastedTracker.Message(roster, Guid.NewGuid(), 1));
    }
}
