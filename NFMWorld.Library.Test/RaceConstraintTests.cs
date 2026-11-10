using NFMWorldLibrary.Gamemodes;

namespace NFMWorld.Library.Test;

/// <summary>
/// The race constraint decides what ends a race: a wasting race ignores the finish line, a
/// racing race ignores deaths, and both honour everything.
/// </summary>
[TestClass]
public class RaceConstraintTests
{
    [DataTestMethod]
    [DataRow("racing", true, false)]
    [DataRow("wasting", false, true)]
    [DataRow("both", true, true)]
    [DataRow("Racing", true, false, DisplayName = "the config value is matched case-insensitively")]
    public void From_ReadsTheConfiguredOption(string value, bool racing, bool wasting)
    {
        var constraint = RaceConstraint.From(value);

        Assert.AreEqual(racing, constraint.Racing);
        Assert.AreEqual(wasting, constraint.Wasting);
    }

    [TestMethod]
    public void From_IsBothWhenUnsetOrUnrecognised()
    {
        Assert.AreEqual(RaceConstraint.Both, RaceConstraint.From((string?)null));
        Assert.AreEqual(RaceConstraint.Both, RaceConstraint.From("nonsense"));
        Assert.AreEqual(RaceConstraint.Both, RaceConstraint.From((IReadOnlyDictionary<string, object>?)null));
    }

    [TestMethod]
    public void From_ReadsTheConfigDictionary()
    {
        var config = new Dictionary<string, object> { ["constraint"] = "wasting" };

        Assert.AreEqual(new RaceConstraint(Racing: false, Wasting: true), RaceConstraint.From(config));
    }
}
