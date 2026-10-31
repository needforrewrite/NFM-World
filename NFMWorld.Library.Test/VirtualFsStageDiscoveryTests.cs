using NFMWorld.PhysicsFS;

namespace NFMWorld.Library.Test;

[TestClass]
public class VirtualFsStageDiscoveryTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NFMWorld-stage-tests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void StageCollectionsAreFoundInInstallWithSeparateUserDirectory()
    {
        var install = Path.Combine(_root, "install");
        var user = Path.Combine(_root, "user");
        Directory.CreateDirectory(Path.Combine(install, "data", "stages", "nfm1", "nested"));
        Directory.CreateDirectory(Path.Combine(install, "data", "stages", "metadata"));
        File.WriteAllText(Path.Combine(install, "data", "stages", "nfm1", "1_introductory.txt"), "stage");
        File.WriteAllText(Path.Combine(install, "data", "stages", "metadata", ".DS_Store"), "metadata");

        var fs = new VirtualFS();
        fs.MountDirectory(install);
        fs.MountWriteDestination(user);
        fs.MountDirectory(user, prepend: true);

        var collections = fs.EnumerateDirectories("data/stages")
            .Where(dir => fs.EnumerateFiles(dir, "*.txt").Any()).ToArray();
        CollectionAssert.AreEqual(new[] { "data/stages/nfm1" }, collections);
        CollectionAssert.AreEqual(new[] { "data/stages/nfm1/1_introductory.txt" },
            fs.EnumerateFiles(collections[0], "*.txt").ToArray());
    }

    [TestMethod]
    public void MissingStageDirectoryReturnsEmptyCollectionList()
    {
        Directory.CreateDirectory(_root);
        var fs = new VirtualFS();
        fs.MountDirectory(_root);
        Assert.IsFalse(fs.EnumerateDirectories("data/stages").Any());
    }
}
