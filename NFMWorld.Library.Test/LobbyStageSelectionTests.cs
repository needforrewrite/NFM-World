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
            ["nfm1/1_introductory", "user/my_stage"],
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

    /// <summary>
    /// A public stage stays the session's stage when the player looks at local content again: the
    /// list grows a row for it, so the highlight never points at nothing.
    /// </summary>
    [TestMethod]
    public void ShowLocal_KeepsAPublicStage_AsItsOwnRow()
    {
        var state = new StageSelectState();
        var publicStage = "archive/" + new string('a', 64);
        LobbyStageSelection.Select(state, publicStage);

        LobbyStageSelection.ShowLocal(
            state,
            LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names),
            publicStage
        );

        Assert.AreEqual(publicStage, state.Selected.Value);
        Assert.IsTrue(state.Stages.Value.Any(stage => stage.Id == publicStage), "the chosen stage had no row");
        Assert.IsFalse(state.OnlineSource.Value);
        Assert.IsTrue(state.ConfirmEnabled.Value);
    }

    /// <summary>A search that no longer matches the chosen stage does not drop it.</summary>
    [TestMethod]
    public void ApplyFilter_KeepsAChosenPublicStage_WhenTheSearchHidesIt()
    {
        var state = new StageSelectState();
        var publicStage = "archive/" + new string('b', 64);
        LobbyStageSelection.Select(state, publicStage);

        LobbyStageSelection.ApplyFilter(
            state,
            LobbyStageSelection.LocalStages(["nfm1/1_introductory"], Names),
            "intro"
        );

        Assert.AreEqual(publicStage, state.Selected.Value);
        Assert.IsTrue(state.Stages.Value.Any(stage => stage.Id == publicStage));
        Assert.IsTrue(state.ConfirmEnabled.Value);
    }

    private static RemoteAsset Asset(string name, string hash, string author, bool eligible)
        => new(1, name, "Stage", hash, 1024, null, author, eligible);
}
