using System.Numerics;
using System.Xml.Linq;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class SvgColorAdjustmentTests
{
    [TestMethod]
    public void SnapMultiplicationBrightensAndDarkensChannelsWithTheLegacyImageCap()
    {
        var document = XDocument.Parse(
            SvgColorAdjustment.Apply(
                "<svg><path fill='#80bef4' stroke='#fff'/></svg>",
                new Vector3(1.1f, 1.3f, 0.62f),
                225
            )
        );
        var shape = document.Root!.Element("path")!;
        Assert.AreEqual("#8ce197", (string?)shape.Attribute("fill"));
        Assert.AreEqual("#e1e19e", (string?)shape.Attribute("stroke"));
    }

    [TestMethod]
    public void AlphaAndOpacityArePreservedForShortAndLongHexColors()
    {
        var document = XDocument.Parse(
            SvgColorAdjustment.Apply(
                "<svg><path fill='#1234' stroke='#33669980' opacity='.25'/></svg>",
                new Vector3(2)
            )
        );
        var shape = document.Root!.Element("path")!;
        Assert.AreEqual("#22446644", (string?)shape.Attribute("fill"));
        Assert.AreEqual("#66ccff80", (string?)shape.Attribute("stroke"));
        Assert.AreEqual(".25", (string?)shape.Attribute("opacity"));
    }

    [TestMethod]
    public void StylesAndGradientStopsAreAdjustedWithoutChangingGeometryOrGradientReferences()
    {
        const string markup = """
                              <svg><style>.label{fill:#80bef4}</style><defs>
                              <linearGradient id="meter"><stop offset="0" stop-color="#fff"/></linearGradient>
                              </defs><path class="label" d="M0 0L10 10"/>
                              <path fill="url(#meter)" stroke="none"/></svg>
                              """;
        var document = XDocument.Parse(SvgColorAdjustment.Apply(markup, new Vector3(1.1f, 1.3f, 0.62f), 225));
        var paths = document.Root!.Elements("path").ToArray();
        StringAssert.Contains((string)paths[0].Attribute("style")!, "fill:#8ce197");
        Assert.AreEqual("M0 0L10 10", (string?)paths[0].Attribute("d"));
        Assert.AreEqual("url(#meter)", (string?)paths[1].Attribute("fill"));
        Assert.AreEqual("none", (string?)paths[1].Attribute("stroke"));
        Assert.AreEqual("#e1e19e", (string?)document.Descendants("stop").Single().Attribute("stop-color"));
    }

    [TestMethod]
    public void NegativeMultipliersClampToBlackAndNeutralAdjustmentKeepsBlackBlack()
    {
        var dark = XDocument.Parse(SvgColorAdjustment.Apply("<svg fill='#fff'/>", new Vector3(-1)));
        Assert.AreEqual("#000000", (string?)dark.Root!.Attribute("fill"));
        var neutral = XDocument.Parse(SvgColorAdjustment.Apply("<svg fill='#000'/>", Vector3.One, 225));
        Assert.AreEqual("#000000", (string?)neutral.Root!.Attribute("fill"));
    }

    [TestMethod]
    public void NonFiniteMultipliersAreRejected()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SvgColorAdjustment.Apply("<svg/>", new Vector3(float.NaN)));
}
