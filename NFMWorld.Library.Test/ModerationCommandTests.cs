using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Library.Test;

[TestClass]
public class ModerationCommandTests
{
    [TestMethod]
    public void BanWithDuration_ParsesTempBan()
    {
        var ok = ModerationCommand.TryParse("/ban alice 7d griefing", out var request);
        Assert.IsTrue(ok);
        Assert.AreEqual(ModerationActionKind.TempBan, request.Action);
        Assert.AreEqual("alice", request.Target);
        Assert.AreEqual(TimeSpan.FromDays(7), request.Duration);
        Assert.AreEqual("griefing", request.Reason);
    }

    [TestMethod]
    public void BanWithProseReason_ParsesPermanentBan()
    {
        var ok = ModerationCommand.TryParse("/ban alice 7 days of griefing", out var request);
        Assert.IsTrue(ok);
        Assert.AreEqual(ModerationActionKind.Ban, request.Action);
        Assert.IsNull(request.Duration);
        Assert.AreEqual("7 days of griefing", request.Reason);
    }

    [TestMethod]
    public void UnparseableDuration_TreatsItAsReason()
    {
        var ok = ModerationCommand.TryParse("/mute bob forever", out var request);
        Assert.IsTrue(ok);
        Assert.AreEqual(ModerationActionKind.Mute, request.Action);
        Assert.IsNull(request.Duration);
        Assert.AreEqual("forever", request.Reason);
    }

    [TestMethod]
    public void UnknownVerb_IsRejected()
    {
        Assert.IsFalse(ModerationCommand.TryParse("/nope alice", out _));
    }

    [TestMethod]
    public void VerbWithoutTarget_IsRejected()
    {
        Assert.IsFalse(ModerationCommand.TryParse("/ban", out _));
    }

    [TestMethod]
    public void PlainMessage_IsNotACommand()
    {
        Assert.IsFalse(ModerationCommand.TryParse("hello there", out _));
    }

    [TestMethod]
    public void Rename_TakesTheNameAsItsArgument()
    {
        var ok = ModerationCommand.TryParse("/rename alice newname", out var request);
        Assert.IsTrue(ok);
        Assert.AreEqual(ModerationActionKind.Rename, request.Action);
        Assert.AreEqual("alice", request.Target);
        Assert.AreEqual("newname", request.Argument);
    }

    [TestMethod]
    public void Kick_ParsesPermanentKick()
    {
        var ok = ModerationCommand.TryParse("/kick bob", out var request);
        Assert.IsTrue(ok);
        Assert.AreEqual(ModerationActionKind.Kick, request.Action);
        Assert.AreEqual("bob", request.Target);
    }

    [TestMethod]
    public void Durations_ParseEveryUnit()
    {
        Assert.IsTrue(ModerationDurations.TryParse("12h", out var hours));
        Assert.AreEqual(TimeSpan.FromHours(12), hours);
        Assert.IsTrue(ModerationDurations.TryParse("30m", out var minutes));
        Assert.AreEqual(TimeSpan.FromMinutes(30), minutes);
        Assert.IsTrue(ModerationDurations.TryParse("2w", out var weeks));
        Assert.AreEqual(TimeSpan.FromDays(14), weeks);
        Assert.IsFalse(ModerationDurations.TryParse("12x", out _));
        Assert.IsFalse(ModerationDurations.TryParse("m", out _));
    }
}
