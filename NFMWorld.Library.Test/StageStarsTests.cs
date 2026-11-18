using NFMWorldLibrary;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
public class StageStarsTests
{
    [TestMethod]
    [DataRow("", false, false)]
    [DataRow("lightson()", false, true)]
    [DataRow("stars()", true, false)]
    [DataRow("lightson()\nstars()", true, true)]
    [DataRow("lightson()\nstars(false)", false, true)]
    [DataRow("stars()\nstars(false)", false, false)]
    [DataRow("stars(false)\nstars()", true, false)]
    public void Parse_StarsAreIndependentOfLights(string directives, bool stars, bool lights)
    {
        var stage = StageLoader.FromLines("stars-test", directives.Split('\n', StringSplitOptions.RemoveEmptyEntries));

        Assert.AreEqual(stars, stage.DrawStars);
        Assert.AreEqual(lights, stage.LightsOn);
        Assert.AreEqual(0, stage.unknownParameters.Count);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Serialize_PreservesStarsIndependentlyOfLights(bool stars, bool lights)
    {
        var stage = new StageLoader { DrawStars = stars, LightsOn = lights };
        var loaded = TightPackSerializer.Deserialize<StageLoader>(TightPackSerializer.Serialize(in stage));

        Assert.IsNotNull(loaded);
        Assert.AreEqual(stars, loaded.DrawStars);
        Assert.AreEqual(lights, loaded.LightsOn);
    }
}
