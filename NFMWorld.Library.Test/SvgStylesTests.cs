using System.Text;
using System.Xml.Linq;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class SvgStylesTests
{
    private static XElement Shape(XDocument document, string id) =>
        document.Descendants().Single(e => (string?)e.Attribute("id") == id);

    [TestMethod]
    public void DesignerClassesResolveColorsOpacityAndStrokes()
    {
        var document = XDocument.Parse(
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 733.17 409.6">
              <defs><style>
                .cls-1{fill:#7da8ff;}
                .cls-5,.cls-7{fill:#fff;}
                .cls-5{opacity:0.22;}
                .cls-6{fill:none;stroke:#2b548e;stroke-width:12px;stroke-linecap:square;stroke-linejoin:round;}
              </style></defs>
              <path id="blue" class="cls-1" d="M0 0L10 10"/>
              <path id="shine" class="cls-5" d="M0 0L20 20"/>
              <path id="white" class="cls-7" d="M0 0L30 30"/>
              <path id="outline" class="cls-6" d="M0 0L40 40"/>
            </svg>
            """
        );

        SvgStyles.Inline(document);

        Assert.AreEqual("fill:#7da8ff", (string?)Shape(document, "blue").Attribute("style"));
        StringAssert.Contains((string)Shape(document, "shine").Attribute("style")!, "opacity:0.22");
        StringAssert.Contains((string)Shape(document, "shine").Attribute("style")!, "fill:#fff");
        Assert.AreEqual("fill:#fff", (string?)Shape(document, "white").Attribute("style"));
        var outline = (string)Shape(document, "outline").Attribute("style")!;
        StringAssert.Contains(outline, "fill:none");
        StringAssert.Contains(outline, "stroke:#2b548e");
        StringAssert.Contains(outline, "stroke-width:12px");
        StringAssert.Contains(outline, "stroke-linecap:square");
        Assert.AreEqual("M0 0L40 40", (string?)Shape(document, "outline").Attribute("d"));
        Assert.AreEqual("0 0 733.17 409.6", (string?)document.Root!.Attribute("viewBox"));
    }

    [TestMethod]
    public void StylesheetAndInlineRulesFollowSpecificityAndImportance()
    {
        var document = XDocument.Parse(
            """
            <svg><style>
              path{fill:red} .car{fill:blue} #body{fill:green}
              .car{fill:yellow} path.car{stroke:blue !important}
            </style><path id="body" class="car" fill="black" style="opacity:0.5;stroke:red;fill:white"/></svg>
            """
        );
        SvgStyles.Inline(document);
        var style = (string)Shape(document, "body").Attribute("style")!;
        StringAssert.Contains(style, "fill:white");
        StringAssert.Contains(style, "stroke:blue");
        StringAssert.Contains(style, "opacity:0.5");
        Assert.IsFalse(style.Contains("!important"));
    }

    [TestMethod]
    public void RulesApplyToGroupsAndMultipleClassTokens()
    {
        var document = XDocument.Parse(
            """
            <svg><style>.base{fill:blue}.base.light{opacity:0.2}</style>
            <g id="group" class="base light"><path d="M0 0L1 1"/></g></svg>
            """
        );
        SvgStyles.Inline(document);
        var style = (string)Shape(document, "group").Attribute("style")!;
        StringAssert.Contains(style, "fill:blue");
        StringAssert.Contains(style, "opacity:0.2");
    }

    [TestMethod]
    public void LoadingLeavesTheSourceStreamUnchangedAndOpen()
    {
        const string original = "<svg><style>.a{fill:blue}</style><path class='a' d='M0 0L1 1'/></svg>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(original), writable: false);
        var loaded = SvgStyles.ReadWithInlineStyles(stream);
        Assert.IsTrue(stream.CanRead);
        Assert.AreEqual(original, Encoding.UTF8.GetString(stream.ToArray()));
        StringAssert.Contains(loaded, "style=\"fill:blue\"");
    }
}
