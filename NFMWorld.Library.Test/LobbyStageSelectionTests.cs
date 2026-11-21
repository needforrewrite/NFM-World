using NFMWorld.Gameplay;
using NFMWorld.UI.Routes;
using NFMWorldLibrary.Content;

namespace NFMWorld.Library.Test;

/// <summary>
/// The lobby's stage list as the phase fills it: LOCAL offers only the stages a session may use,
/// ONLINE offers public archive stages, and a pick the lobby would refuse never becomes the
/// session's stage.
/// </summary>
[TestClass]
public class LobbyStageSelectionTests
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nfm1/1_introductory"] = "Introductory",
        ["user/my_stage"] = "My Stage"
    };

    [TestMethod]
    public void LocalStages_OffersOnlyWhatASessionCanUse()
    {
        var stages = LobbyStageSelection.LocalStages(
            ["nfm1/1_introductory", "user/my_stage", "archive/" + new string('a', 64)],
            Names
        );

        Assert.AreEqual(1, stages.Count);
        Assert.AreEqual("nfm1/1_introductory", stages[0].Id);
        Assert.AreEqual("Introductory", stages[0].Label);
    }

    [TestMethod]
    public void LocalStages_KeepsTheStageWhenASelectionStillFits()
    {
        var state = new StageSelectState();
        var stages = LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names);

        LobbyStageSelection.ShowLocal(state, stages, "nfm1/1_introductory");

        Assert.AreEqual("nfm1/1_introductory", state.Selected.Value);
        Assert.AreEqual("", state.Restriction.Value, "an acceptable stage must not carry a restriction");
        Assert.IsTrue(state.ConfirmEnabled.Value);
    }

    [TestMethod]
    public void ShowLocal_DropsASelectionItCannotOffer()
    {
        var state = new StageSelectState();
        var stages = LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names);

        LobbyStageSelection.ShowLocal(state, stages, "user/my_stage");

        Assert.AreEqual("", state.Selected.Value);
        Assert.AreEqual("", state.Restriction.Value);
        Assert.AreEqual(1, state.Stages.Value.Count);
    }

    [TestMethod]
    public void Select_RefusesALocalStage_AndSaysWhy()
    {
        var state = new StageSelectState();

        var accepted = LobbyStageSelection.Select(state, "user/my_stage");

        Assert.IsFalse(accepted);
        Assert.IsFalse(state.ConfirmEnabled.Value);
        StringAssert.Contains(state.Restriction.Value, "published");
    }

    [TestMethod]
    public void Select_AcceptsAPublicStage()
    {
        var state = new StageSelectState();
        var id = "archive/" + new string('a', 64);

        var accepted = LobbyStageSelection.Select(state, id);

        Assert.IsTrue(accepted);
        Assert.IsTrue(state.ConfirmEnabled.Value);
        Assert.AreEqual("", state.Restriction.Value);
    }

    [TestMethod]
    public void RemoteStages_NamesEachStageByItsHash_AndShowsItsAuthor()
    {
        var stages = LobbyStageSelection.RemoteStages(
            [
                Asset("Sunset Loop", "AAAA", "maxine", eligible: true),
                Asset("Not Eligible", "BBBB", "someone", eligible: false)
            ]
        );

        Assert.AreEqual(1, stages.Count, "an ineligible asset must not be offered");
        Assert.AreEqual("archive/aaaa", stages[0].Id);
        Assert.AreEqual("Sunset Loop", stages[0].Label);
        Assert.AreEqual("maxine", stages[0].Subtitle);
    }

    [TestMethod]
    public void ApplyFilter_NarrowsByLabelAndKeepsTheSearchText()
    {
        var state = new StageSelectState();
        var stages = LobbyStageSelection.LocalStages(["nfm1/1_introductory", "nfm1/2_contrary"], Names);

        LobbyStageSelection.ApplyFilter(state, stages, "intro");

        Assert.AreEqual("intro", state.Search.Value);
        Assert.AreEqual(1, state.Stages.Value.Count);
        Assert.AreEqual("nfm1/1_introductory", state.Stages.Value[0].Id);
    }

    [TestMethod]
    public void SwitchingTabs_KeepsTheArchiveSelectionOnlyInOnlineResults()
    {
        var state = new StageSelectState();
        var archive = new StageOption("archive/" + new string('a', 64), "Prolific Dryland", "maxine");
        LobbyStageSelection.ShowRemote(state, [archive]);
        Assert.IsTrue(LobbyStageSelection.Select(state, archive.Id));
        var local = LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names);

        LobbyStageSelection.ShowLocal(state, local, archive.Id);

        Assert.AreEqual(archive.Id, state.Selected.Value);
        Assert.AreEqual(archive, state.SelectedArchiveStage.Value);
        CollectionAssert.AreEqual(local.ToArray(), state.Stages.Value.ToArray());
        Assert.IsFalse(state.OnlineSource.Value);
        Assert.IsTrue(state.ConfirmEnabled.Value);

        LobbyStageSelection.ShowRemote(state, []);

        Assert.IsTrue(state.OnlineSource.Value);
        CollectionAssert.AreEqual(new[] { archive }, state.Stages.Value.ToArray());
    }

    [TestMethod]
    public void ApplyFilter_LeavesAnArchiveIdOutOfLocalResults()
    {
        var state = new StageSelectState();
        var publicStage = "archive/" + new string('b', 64);
        var local = LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names);
        LobbyStageSelection.ShowLocal(state, local, publicStage);

        LobbyStageSelection.ApplyFilter(state, local, publicStage);

        Assert.AreEqual(publicStage, state.Selected.Value);
        Assert.AreEqual(0, state.Stages.Value.Count);
        Assert.IsTrue(state.ConfirmEnabled.Value);
    }

    private static RemoteAsset Asset(string name, string hash, string author, bool eligible)
        => new(1, name, "Stage", hash, 1024, null, author, eligible);
}
