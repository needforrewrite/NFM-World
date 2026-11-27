using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class StageMusicPackagingTests
{
    [TestInitialize]
    public void Setup()
        => TheVFS.VFS.MountWriteDestination(Path.Combine(Path.GetTempPath(), "nfmw-stage-music", Guid.NewGuid().ToString("N")));

    [TestMethod]
    public void Export_PreservesBothTracks_AndReloadsWithoutTheOriginalFiles()
    {
        var stage = new StageLoader { musicPath = "custom/prolific_heights.mod", remasteredMusicPath = "custom/remaster.ogg" };
        Write("data/music/" + stage.musicPath, [1, 2, 3]);
        Write("data/music/" + stage.remasteredMusicPath, [4, 5, 6]);
        var bytes = StockCatalog.PackageStage("music-stage", stage, includeMusic: true);
        Setup();
        Write("data/stages/music-stage.radpack", bytes);

        var loaded = RadpackReader.TryReadTrack("music-stage", out var error);
        Assert.IsNotNull(loaded, error);
        Assert.IsNotNull(loaded.EmbeddedMusic);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, loaded.EmbeddedMusic[loaded.musicPath]);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, loaded.EmbeddedMusic[loaded.remasteredMusicPath]);
        Assert.IsFalse(TheVFS.VFS.FileExists("data/music/" + stage.musicPath));

        var repacked = StockCatalog.PackageStage("music-stage", loaded, includeMusic: true);
        CollectionAssert.AreEqual(bytes, repacked);
    }

    [TestMethod]
    public void CanonicalPackage_DoesNotChangeWhenEmbeddedMusicIsAttached()
    {
        var stage = new StageLoader { musicPath = "custom/missing.mod" };
        var original = StockCatalog.PackageStage("stock-stage", stage);
        stage.EmbeddedMusic = new Dictionary<string, byte[]> { [stage.musicPath] = [1, 2, 3] };
        CollectionAssert.AreEqual(original, StockCatalog.PackageStage("stock-stage", stage));
        Assert.IsInstanceOfType<RadpackTrack>(RadpackSerializer.Deserialize(original));
        Assert.IsFalse(RadpackSerializer.Deserialize(original) is RadpackTrackWithMusic);
    }

    [TestMethod]
    public void Export_ReplacementTracksDoNotMutateLiveStageOrCanonicalPackage()
    {
        var stage = new StageLoader
        {
            musicPath = "custom/original.mod",
            remasteredMusicPath = "custom/remaster.wav",
            musicFreqMul = 1.25,
            musicTempoMul = 0.75,
            EmbeddedMusic = new Dictionary<string, byte[]>
            {
                ["custom/original.mod"] = [1, 2],
                ["custom/remaster.wav"] = [3, 4]
            }
        };
        var canonical = StockCatalog.PackageStage("stage", stage);
        var source = StockCatalog.ReadStageMusic(stage);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, source[stage.musicPath]);
        var replacements = new Dictionary<string, StageMusic>
        {
            [stage.musicPath] = new("custom/original.opus", [5, 6]),
            [stage.remasteredMusicPath] = new("custom/remaster.opus", [7, 8])
        };

        var bytes = StockCatalog.PackageStage("stage", stage, replacements);
        var loaded = (RadpackTrackWithMusic)RadpackSerializer.Deserialize(bytes);
        Assert.AreEqual("custom/original.opus", loaded.Stage.musicPath);
        Assert.AreEqual("custom/remaster.opus", loaded.Stage.remasteredMusicPath);
        Assert.AreEqual(1.25, loaded.Stage.musicFreqMul);
        Assert.AreEqual(0.75, loaded.Stage.musicTempoMul);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, loaded.Stage.EmbeddedMusic![loaded.Stage.musicPath]);
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, loaded.Stage.EmbeddedMusic[loaded.Stage.remasteredMusicPath]);
        Assert.AreEqual("custom/original.mod", stage.musicPath);
        Assert.AreEqual("custom/remaster.wav", stage.remasteredMusicPath);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, stage.EmbeddedMusic![stage.musicPath]);
        CollectionAssert.AreEqual(canonical, StockCatalog.PackageStage("stage", stage));
    }

    [TestMethod]
    public void Export_ReplacementTracksPreserveSharedPath()
    {
        var stage = new StageLoader { musicPath = "shared.wav", remasteredMusicPath = "shared.wav" };
        var replacements = new Dictionary<string, StageMusic> { ["shared.wav"] = new("shared.opus", [1]) };
        var loaded = (RadpackTrackWithMusic)RadpackSerializer.Deserialize(StockCatalog.PackageStage(
            "stage",
            stage,
            replacements
        )
        );
        Assert.AreEqual("shared.opus", loaded.Stage.musicPath);
        Assert.AreEqual(loaded.Stage.musicPath, loaded.Stage.remasteredMusicPath);
        Assert.AreEqual(1, loaded.Music.Count);
    }

    [TestMethod]
    public void Export_UnchangedReplacementMatchesNormalPackage()
    {
        var stage = new StageLoader { musicPath = "custom/original.wav", remasteredMusicPath = "custom/remaster.wav" };
        Write("data/music/custom/original.wav", [1, 2, 3]);
        Write("data/music/custom/remaster.wav", [4, 5, 6]);
        var source = StockCatalog.ReadStageMusic(stage);
        var replacements = source.ToDictionary(entry => entry.Key, entry => new StageMusic(entry.Key, entry.Value));
        CollectionAssert.AreEqual(
            StockCatalog.PackageStage("stage", stage, includeMusic: true),
            StockCatalog.PackageStage("stage", stage, replacements)
        );
    }

    [TestMethod]
    public void Export_ReplacementTracksMustBeCompleteAndHaveDistinctSafePaths()
    {
        var stage = new StageLoader { musicPath = "first.wav", remasteredMusicPath = "second.wav" };
        var replacements = new Dictionary<string, StageMusic> { ["first.wav"] = new("first.opus", [1]) };
        Assert.Throws<InvalidDataException>(() => StockCatalog.PackageStage("stage", stage, replacements));
        replacements["second.wav"] = new("first.opus", [2]);
        Assert.Throws<InvalidDataException>(() => StockCatalog.PackageStage("stage", stage, replacements));
        replacements["second.wav"] = new("../outside.opus", [2]);
        Assert.Throws<InvalidDataException>(() => StockCatalog.PackageStage("stage", stage, replacements));
    }

    [TestMethod]
    public void Export_MissingSoundtrackFailsInsteadOfWritingAnIncompletePackage()
    {
        var stage = new StageLoader { musicPath = $"custom/missing-{Guid.NewGuid():N}.mod" };
        Assert.Throws<FileNotFoundException>(() => StockCatalog.PackageStage("stage", stage, includeMusic: true));
    }

    [TestMethod]
    public void Export_StageWithoutMusicUsesTheExistingTrackFormat()
    {
        var stage = new StageLoader();
        CollectionAssert.AreEqual(
            StockCatalog.PackageStage("stage", stage),
            StockCatalog.PackageStage("stage", stage, includeMusic: true)
        );
    }

    [TestMethod]
    [DataRow("../../cfg/account.json")]
    [DataRow("/outside.wav")]
    [DataRow("C:\\outside.wav")]
    public void Export_SoundtrackCannotReadOutsideTheMusicDirectory(string path)
    {
        var stage = new StageLoader { musicPath = path };
        Assert.Throws<InvalidDataException>(() => StockCatalog.PackageStage("stage", stage, includeMusic: true));
    }

    /// <summary>
    /// A remaster directive that names a file which is not shipped plays nothing, with no error:
    /// the loader only records the path. Re-encoding the OSTs therefore has to keep the stage text
    /// and the files in step.
    /// </summary>
    [TestMethod]
    public void EveryShippedRemasterTrack_ResolvesToAShippedFile()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        var declared = 0;
        var missing = new List<string>();
        foreach (var file in TheVFS.VFS.EnumerateFiles("data/stages", "*.txt", SearchOption.AllDirectories))
        {
            foreach (var line in TheVFS.VFS.ReadAllText(file).Split('\n'))
            {
                var start = line.IndexOf("soundtrackremaster(", StringComparison.Ordinal);
                if (start < 0) continue;

                var args = line[(start + "soundtrackremaster(".Length)..].Split(')')[0].Split(',');
                if (args.Length < 2) continue;

                declared++;
                var path = $"data/music/{args[0].Trim()}/{args[1].Trim()}";
                if (!TheVFS.VFS.FileExists(path)) missing.Add($"{file} -> {path}");
            }
        }

        Assert.AreNotEqual(0, declared, "no shipped stage declares a remastered track");
        CollectionAssert.AreEqual(Array.Empty<string>(), missing, string.Join("\n", missing));
    }

    private static void Write(string path, byte[] bytes)
    {
        using var stream = TheVFS.VFS.OpenWrite(path);
        stream.Write(bytes);
    }
}
