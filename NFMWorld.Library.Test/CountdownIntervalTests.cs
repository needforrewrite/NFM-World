using NFMWorldLibrary;
using NFMWorldLibrary.Gamemodes.DotNet;

namespace NFMWorld.Library.Test;

/// <summary>
/// The countdown is counted in ticks, so its interval has to match the tick rate of whoever
/// runs it. The client and the server gamemode do not run at the same rate: the client ticks at
/// <see cref="Physics.TargetTps"/> (63) and the Game Master's server loop at 20. Using the
/// client's interval on the server made each countdown step take three times as long, which is
/// what left the race stuck after 3-2-1.
/// </summary>
[TestClass]
public class CountdownIntervalTests
{
    /// <summary>One countdown step is 10 original ticks, at 21.4 TPS.</summary>
    private const float StepSeconds = 10f / Physics.OriginalTps;

    [TestMethod]
    public void ClientInterval_MatchesTheClientTickRate()
    {
        var expected = (int)(StepSeconds * Physics.TargetTps);

        Assert.AreEqual(expected, GamemodeUtils.CountdownInterval);
    }

    [TestMethod]
    public void ServerInterval_MatchesTheServerTickRate()
    {
        var expected = (int)Math.Round(StepSeconds * GamemodeUtils.ServerTps);

        Assert.AreEqual(expected, GamemodeUtils.ServerCountdownInterval);
    }

    [TestMethod]
    public void ServerInterval_IsNotTheClientInterval()
    {
        // The bug was that they were the same. They cannot be, at these rates.
        Assert.AreNotEqual(GamemodeUtils.CountdownInterval, GamemodeUtils.ServerCountdownInterval);
    }

    [TestMethod]
    public void BothIntervals_AreAboutTheSameWallClockLength()
    {
        var clientSeconds = GamemodeUtils.CountdownInterval / (float)Physics.TargetTps;
        var serverSeconds = GamemodeUtils.ServerCountdownInterval / (float)GamemodeUtils.ServerTps;

        // Within a tick's worth of each other, so the two countdowns stay in step.
        Assert.IsLessThan(1f / GamemodeUtils.ServerTps, Math.Abs(clientSeconds - serverSeconds));
    }
}
