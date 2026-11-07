using NFMWorld.PhysicsFS;

namespace NFMWorld.Library.Test;

[TestClass]
public class ArchiveMountTests
{
    /// <summary>
    /// The mount is a thin translation from a path the game knows to a hash the archive serves.
    /// This exercises a FetchFileSystem built the same way, against a listing that only offers
    /// one asset, so a path the listing did not provide must not resolve.
    /// </summary>
    [TestMethod]
    public void FetchFileSystem_ServesOnlyListedPaths()
    {
        const string hash = "abc123";
        var fetched = new List<string>();

        byte[]? Fetch(string path)
        {
            fetched.Add(path);
            return path == "archive/Car/mycar" ? "radpack-bytes"u8.ToArray() : null;
        }

        var fs = new FetchFileSystem(Fetch, paths: ["archive/Car/mycar"]);

        Assert.IsTrue(fs.FileExists("archive/Car/mycar"));
        Assert.IsFalse(fs.FileExists("archive/Car/other"));

        using var stream = fs.OpenRead("archive/Car/mycar");
        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.AreEqual("radpack-bytes", reader.ReadToEnd());

        Assert.Contains("archive/Car/mycar", fetched);
    }

    [TestMethod]
    public void FetchFileSystem_EnumeratesWhatTheListingProvided()
    {
        var fs = new FetchFileSystem(_ => null, paths: ["archive/Car/a", "archive/Stage/b"]);

        CollectionAssert.AreEquivalent(
            new[] { "archive/Car/a", "archive/Stage/b" },
            fs.EnumerateFiles("archive", "*", SearchOption.AllDirectories).ToArray());
    }
}
