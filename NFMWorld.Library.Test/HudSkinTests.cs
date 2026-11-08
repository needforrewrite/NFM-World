using System.IO.Compression;
using System.Text.Json;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary.Hud;

namespace NFMWorld.Library.Test;

[TestClass]
public class HudSkinTests
{
    [TestMethod]
    public void HudTextSnapPreservesAlphaAndClampsChannels()
    {
        var color = HudColorSnap.Apply(new Maxine.Extensions.Mathematics.Color(128, 190, 244, 80), new(10, 30, -38));
        Assert.AreEqual((byte)140, color.R);
        Assert.AreEqual((byte)246, color.G);
        Assert.AreEqual((byte)151, color.B);
        Assert.AreEqual((byte)80, color.A);
    }

    [TestMethod]
    public void SafeZoneInsetsAreSymmetricAndKeepTheViewportCenter()
    {
        var bounds = HudSafeZoneSettings.Bounds(new(10, 20), new(1000, 500), 0.8f, 0.9f);
        Assert.AreEqual(110f, bounds.X, 0.001f);
        Assert.AreEqual(45f, bounds.Y, 0.001f);
        Assert.AreEqual(800f, bounds.Width, 0.001f);
        Assert.AreEqual(450f, bounds.Height, 0.001f);
        Assert.AreEqual(510f, bounds.X + bounds.Width / 2, 0.001f);
        Assert.AreEqual(270f, bounds.Y + bounds.Height / 2, 0.001f);
    }

    [TestMethod]
    public void DefaultSafeZonePreservesTheOriginalViewport()
    {
        var bounds = HudSafeZoneSettings.Bounds(new(10, 20), new(1000, 500), 1, 1);
        Assert.AreEqual(10f, bounds.X);
        Assert.AreEqual(20f, bounds.Y);
        Assert.AreEqual(1000f, bounds.Width);
        Assert.AreEqual(500f, bounds.Height);
    }

    [TestMethod]
    [DataRow("0.9", 0.9f)]
    [DataRow("0.1", 0.8f)]
    [DataRow("2", 1f)]
    [DataRow("NaN", 1f)]
    [DataRow("Infinity", 1f)]
    [DataRow("invalid", 1f)]
    public void SafeZoneConfigIsFiniteAndBounded(string saved, float expected)
        => Assert.AreEqual(expected, HudSafeZoneSettings.Parse(saved));

    [TestMethod]
    [DataRow("1.25", 1.25f)]
    [DataRow("0.1", 0.5f)]
    [DataRow("5", 2f)]
    [DataRow("NaN", 1f)]
    [DataRow("Infinity", 1f)]
    [DataRow("invalid", 1f)]
    [DataRow("1,25", 1f)]
    public void HudScaleConfigIsFiniteBoundedAndUsesInvariantNumbers(string saved, float expected)
        => Assert.AreEqual(expected, HudScaleSettings.Parse(saved));

    [TestMethod]
    [DataRow("Classic", "classic")]
    [DataRow("World", "world")]
    [DataRow("my-hud", "my-hud")]
    [DataRow("../other", "classic")]
    [DataRow("custom\nhud_skin world", "classic")]
    public void ConfigIdsMigrateWithoutDiscardingCustomSelections(string saved, string expected)
        => Assert.AreEqual(expected, HudSkinId.Normalize(saved));

    [TestMethod]
    public void DiscoverySkipsInvalidSkinsAndKeepsValidPackages()
    {
        var fs = Files(
            new Dictionary<string, string>
            {
                ["data/hud/skins/good/skin.json"] = Manifest("good", """{"text":"{speed} km/h"}"""),
                ["data/hud/skins/broken/skin.json"] = "{",
                ["data/hud/skins/world/skin.json"] = Manifest("world", """{"text":"reserved"}"""),
                ["data/hud/skins/wrong-folder/skin.json"] = Manifest("different-id", """{"text":"wrong"}"""),
                ["data/hud/skins/missing-image/skin.json"] = Manifest(
                    "missing-image",
                    """{"type":"image","source":"missing.svg"}"""
                )
            }
        );
        var errors = new List<string>();
        var skins = HudSkinCatalog.Discover(fs, (path, _) => errors.Add(path));
        Assert.AreEqual(2, skins.Count);
        Assert.AreEqual("good", skins[0].Definition.Id);
        Assert.AreEqual("missing-image", skins[1].Definition.Id);
        Assert.AreEqual(3, errors.Count);
    }

    [TestMethod]
    public void ImagesResolveRelativeToThePackageInTheVfs()
    {
        var fs = Files(
            new Dictionary<string, string>
            {
                ["data/hud/skins/custom/skin.json"] = Manifest("custom", """{"type":"image","source":"images/frame.svg"}"""),
                ["data/hud/skins/custom/images/frame.svg"] = "<svg/>"
            }
        );
        var skins = HudSkinCatalog.Discover(fs, (_, error) => Assert.Fail(error.Message));
        Assert.AreEqual("data/hud/skins/custom", skins.Single().Directory);
    }

    [TestMethod]
    [DataRow("../frame.svg")]
    [DataRow("/frame.svg")]
    [DataRow("C:/frame.svg")]
    [DataRow("nested/../../frame.svg")]
    [DataRow("images\\frame.svg")]
    [DataRow("frame.exe")]
    public void ImagePathsCannotEscapeThePackage(string path)
        => Assert.IsFalse(HudElement.IsAssetPath(path));

    [TestMethod]
    [DataRow("""{"type":"script"}""")]
    [DataRow("""{"text":"{speeed}"}""")]
    [DataRow("""{"text":"{speed"}""")]
    [DataRow("""{"width":0}""")]
    [DataRow("""{"fontSize":1000}""")]
    [DataRow("""{"color":"red"}""")]
    [DataRow("""{"font":null}""")]
    [DataRow("""{"type":"bar","value":"health"}""")]
    public void InvalidElementsAreRejected(string element)
        => Assert.ThrowsExactly<FormatException>(() => HudSkinDefinition.Parse(Manifest("custom", element)));

    [TestMethod]
    public void UnknownPropertiesAndUnsupportedVersionsAreRejected()
    {
        Assert.ThrowsExactly<JsonException>(() => HudSkinDefinition.Parse(Manifest("custom", """{"fontSze":12}""")));
        Assert.ThrowsExactly<FormatException>(() => HudSkinDefinition.Parse(
                Manifest("custom", """{"text":"hello"}""").Replace("\"version\":1", "\"version\":2")
            )
        );
    }

    [TestMethod]
    public void TextUsesHudValuesAndDoesNotInterpretBracesInsideNames()
    {
        var render = HudText.Compile("{speed} km/h {power}% {damage}% LAP {lap}/{totalLaps} {time} [{target}]");
        Assert.AreEqual("123 km/h 100% 75% LAP 2/3 01:02.345 [{damage}]", render(new SampleHud()));
        Assert.AreEqual("00:00.000", HudText.FormatTime(-1));
        Assert.AreEqual("100:00.000", HudText.FormatTime(6_000_000));
    }

    [TestMethod]
    [DataRow(false, 1, 1, true)]
    [DataRow(true, 1, 1, true)]
    [DataRow(false, 2, 2, true)]
    [DataRow(true, 2, 2, true)]
    [DataRow(false, 0, 0, false)]
    [DataRow(true, 0, 2, false)]
    [DataRow(false, 3, 2, false)]
    [DataRow(true, 1, 0, false)]
    public void PositionVisibilityUsesReportedStandingsRegardlessOfMode(
        bool timeTrial,
        int position,
        int totalRacers,
        bool visible
    )
    {
        var element = HudSkinDefinition.Parse(
            Manifest(
                "custom",
                """{"text":"{position}/{totalRacers}","visibleWhen":"hasPosition"}"""
            )
        ).Elements.Single();
        IHudData data = new SampleHud { TimeTrial = timeTrial, Position = position, TotalRacers = totalRacers };
        Assert.AreEqual(visible, data.HasPosition);
        Assert.AreEqual(visible, element.IsVisible(data));
        Assert.AreEqual($"{position}/{totalRacers}", HudText.Compile(element.Text)(data));
    }

    private static string Manifest(string id, string element)
        => $$"""{"version":1,"id":"{{id}}","name":"Test","elements":[{{element}}]}""";

    private static VirtualFS Files(Dictionary<string, string> files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(text);
            }

        var fs = new VirtualFS();
        fs.MountZip(stream.ToArray());
        return fs;
    }

    private sealed class SampleHud : IHudData
    {
        public int SpeedKmh => 123;
        public float PowerFraction => 1;
        public float DamageFraction => 0.75f;
        public int Lap => 2;
        public int TotalLaps => 3;
        public int LapTimeMs => 62345;
        public int Position { get; init; } = 1;
        public int TotalRacers { get; init; } = 7;
        public int Countdown => 0;
        public string Message => "";
        public bool Paused => false;
        public bool TimeTrial { get; init; } = true;
        public int Wasted => 0;
        public int Opponents => 6;
        public float? ArrowAngle => 30;
        public string ArrowTarget => "{damage}";
        public bool ArrowAtCars => true;
        public IReadOnlyList<HudRacer> Racers => [];
        public (float X, float Y, float Z)? ClientPosition => null;
        public float ClientHeading => 0;
        public (float X, float Z) TrackCenter => default;
        public (float X, float Z)? TargetCheckpoint => null;
        public IReadOnlyList<(float X, float Z)> Checkpoints => [];
        public IReadOnlyList<HudRacerMarker> Markers => [];
        public System.Numerics.Matrix4x4 ViewProjection => System.Numerics.Matrix4x4.Identity;
    }
}
