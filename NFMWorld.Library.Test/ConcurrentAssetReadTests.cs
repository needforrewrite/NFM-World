using System.IO.Compression;
using NFMWorld.PhysicsFS;

namespace NFMWorld.Library.Test;

[TestClass]
public class ConcurrentAssetReadTests
{
    [TestMethod]
    public void ParallelStageAndMusicReadsFromTheSameZipKeepTheirOwnBytes()
    {
        var stage = new byte[128 * 1024];
        var music = new byte[256 * 1024];
        new Random(17).NextBytes(stage);
        new Random(29).NextBytes(music);
        using var bytes = new MemoryStream();

        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var stream = archive.CreateEntry("data/stages/test.txt").Open())
                stream.Write(stage);
            using (var stream = archive.CreateEntry("data/music/test.wav").Open())
                stream.Write(music);
        }

        var fs = new VirtualFS();
        fs.MountZip(bytes.ToArray());

        Parallel.For(
            0,
            80,
            index =>
            {
                var isStage = index % 2 == 0;
                var actual = fs.ReadAllBytes(isStage ? "data/stages/test.txt" : "data/music/test.wav");
                CollectionAssert.AreEqual(isStage ? stage : music, actual);
            }
        );
    }
}
