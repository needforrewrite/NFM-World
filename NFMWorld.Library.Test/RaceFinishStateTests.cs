using NFMWorld.UI.ViewModels;

namespace NFMWorld.Library.Test;

[TestClass]
public class RaceFinishStateTests
{
    [TestMethod]
    public void SpectatorsAreNotShownAsLosers()
    {
        var state = new RaceFinishState();
        state.Observe(true, 0, false, "Stage", "Race over", lost: false);
        Assert.IsFalse(state.Won.Value);
        Assert.IsFalse(state.Lost.Value);
    }

    [TestMethod]
    public void ContinueRequiresOneSecondAndDoesNotRestartItsDelayOnHudUpdates()
    {
        var state = new RaceFinishState();
        state.Observe(true, 100, true, "Stage", "You won");
        Assert.IsFalse(state.TryShowSummary());
        state.Observe(true, 1099, true, "Stage", "You won");
        Assert.IsFalse(state.TryShowSummary());
        state.Observe(true, 1100, true, "Stage", "You won");
        Assert.IsTrue(state.TryShowSummary());
        Assert.IsTrue(state.ShowSummary.Value);
    }

    [TestMethod]
    public void RestartClearsTheSummaryAndStartsAFreshDelay()
    {
        var state = new RaceFinishState();
        state.Observe(true, 0, false, "Stage", "You lost");
        state.Observe(true, 1000, false, "Stage", "You lost");
        Assert.IsTrue(state.TryShowSummary());
        state.ContinueSpectating.Value = true;
        state.Observe(false, 1200, false, "", "");
        Assert.IsFalse(state.Active.Value);
        Assert.IsFalse(state.ShowSummary.Value);
        Assert.IsFalse(state.ContinueSpectating.Value);
        state.Observe(true, 5000, true, "Next stage", "You won");
        Assert.IsFalse(state.CanContinue.Value);
    }
}
