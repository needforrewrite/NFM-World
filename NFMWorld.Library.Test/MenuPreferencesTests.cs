using System.IO.Compression;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class MenuPreferencesTests
{
    [TestMethod]
    public void StageChoicesIncludeNestedAndPackedStagesWithoutDuplicates()
    {
        var fs = CreateFileSystem(
            "data/stages/custom/nested/track.txt",
            "data/stages/nfm1/10_stage.txt",
            "data/stages/nfm1/2_stage.txt",
            "data/stages/nfm1/2_stage.radpack",
            "data/stages/readme.json"
        );

        CollectionAssert.AreEqual(
            new[] { "custom/nested/track", "nfm1/2_stage", "nfm1/10_stage" },
            MenuPreferences.GetStages(fs)
        );
    }

    [TestMethod]
    public void FixedStageIsIndependentOfLastPlayedAndMissingStageFallsBack()
    {
        var fs = CreateFileSystem("data/stages/custom/menu.radpack");
        Assert.AreEqual("custom/menu", MenuPreferences.ResolveStage(fs, "custom/menu", "nfm1/race"));
        Assert.AreEqual("nfm1/race", MenuPreferences.ResolveStage(fs, MenuPreferences.LastPlayedStage, "nfm1/race"));
        Assert.AreEqual("nfm1/race", MenuPreferences.ResolveStage(fs, "custom/missing", "nfm1/race"));
    }

    [TestMethod]
    public void InterfaceMusicChoicesExcludeOtherFoldersAndNonAudioFiles()
    {
        var fs = CreateFileSystem(
            "data/music/interface/shift.mp3",
            "data/music/interface/custom/theme.mod",
            "data/music/interface/notes.txt",
            "data/music/nfm1/stage.mp3"
        );
        CollectionAssert.AreEqual(new[] { "custom/theme.mod", "shift.mp3" }, MenuPreferences.GetMusic(fs));
        Assert.AreEqual("data/music/interface/shift.mp3", MenuPreferences.ResolveMusic(fs, "shift.mp3"));
        Assert.IsNull(MenuPreferences.ResolveMusic(fs, MenuPreferences.StageSoundtrack));
        Assert.IsNull(MenuPreferences.ResolveMusic(fs, "missing.mp3"));
    }

    [TestMethod]
    [DataRow("../stage.mp3")]
    [DataRow("/absolute.mp3")]
    [DataRow("C:/music.mp3")]
    [DataRow("nested/../../stage.mp3")]
    [DataRow("track\nmenu_stage other")]
    public void ConfigSelectionsCannotEscapeTheirAssetFolders(string selection)
    {
        var fs = CreateFileSystem("data/music/stage.mp3");
        Assert.AreEqual(MenuPreferences.LastPlayedStage, MenuPreferences.NormalizeStage(selection));
        Assert.AreEqual(MenuPreferences.StageSoundtrack, MenuPreferences.NormalizeMusic(selection));
        Assert.AreEqual("last/track", MenuPreferences.ResolveStage(fs, selection, "last/track"));
        Assert.IsNull(MenuPreferences.ResolveMusic(fs, selection));
    }

    [TestMethod]
    public void BrowserMusicCatalogResolvesSavedTracksWithoutDownloadingAudio()
    {
        var reads = 0;
        var listings = 0;
        var fs = new VirtualFS();
        fs.Mount(
            new FetchFileSystem(
                _ =>
                {
                    reads++;
                    return null;
                },
                listPaths: () =>
                {
                    listings++;
                    return
                    [
                        "data/music/interface/shift.mp3", "data/music/interface/nested/theme.mod", "data/music/nfm1/stage.mp3"
                    ];
                }
            )
        );

        Assert.AreEqual("data/music/interface/shift.mp3", MenuPreferences.ResolveMusic(fs, "shift.mp3"));
        CollectionAssert.AreEqual(new[] { "nested/theme.mod", "shift.mp3" }, MenuPreferences.GetMusic(fs));
        CollectionAssert.AreEqual(
            new[] { "data/music/interface/shift.mp3" },
            fs.EnumerateFiles("data/music/interface", "*.mp3").ToArray()
        );
        Assert.AreEqual(1, listings);
        Assert.AreEqual(0, reads);
    }

    [TestMethod]
    public void StageNamesComeFromTheNameLineWithoutReadingPackedStages()
    {
        var fs = CreateFileSystemWithContent(
            ("data/stages/nfm1/1_stage.txt", "\nname(The Introductory Stage)\n\nsnap(-5,-5,20)\n"),
            ("data/stages/custom/unnamed.txt", "snap(0,0,0)\n"),
            ("data/stages/custom/packed.radpack", "")
        );

        var names = MenuPreferences.GetStageNames(fs);

        Assert.AreEqual("The Introductory Stage", names["nfm1/1_stage"]);
        Assert.IsFalse(names.ContainsKey("custom/unnamed"));
        Assert.IsFalse(names.ContainsKey("custom/packed"));
    }

    private static VirtualFS CreateFileSystemWithContent(params (string Path, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }

        var fs = new VirtualFS();
        fs.MountZip(stream.ToArray());
        return fs;
    }

    private static VirtualFS CreateFileSystem(params string[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in files)
                archive.CreateEntry(file);
        var fs = new VirtualFS();
        fs.MountZip(stream.ToArray());
        return fs;
    }
}
