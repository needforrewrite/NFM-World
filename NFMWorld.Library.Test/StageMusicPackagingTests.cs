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
