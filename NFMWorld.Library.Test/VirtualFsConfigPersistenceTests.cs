using NFMWorld.PhysicsFS;

namespace NFMWorld.Library.Test;

[TestClass]
public class VirtualFsConfigPersistenceTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NFMWorld-config-tests", Guid.NewGuid().ToString("N"));
    private const string ConfigPath = "data/cfg/config.cfg";

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void SavedConfigOverridesInstallConfigOnNextMount()
    {
        var install = Path.Combine(_root, "install");
        var user = Path.Combine(_root, "user");
        Directory.CreateDirectory(Path.Combine(install, "data", "cfg"));
        File.WriteAllText(Path.Combine(install, ConfigPath), "video_fps 60");

        VirtualFS Mount()
        {
            var fs = new VirtualFS();
            fs.MountDirectory(install);
            fs.MountWriteDestination(user);
            fs.MountDirectory(user, prepend: true);
            return fs;
        }

        var first = Mount();
        Assert.AreEqual("video_fps 60", first.ReadAllText(ConfigPath));
        using (var writer = new StreamWriter(first.OpenWrite(ConfigPath)))
            writer.Write("video_fps 240");

        var next = Mount();
        Assert.IsTrue(next.FileExists(ConfigPath));
        Assert.AreEqual("video_fps 240", next.ReadAllText(ConfigPath));
        Assert.AreEqual("video_fps 60", File.ReadAllText(Path.Combine(install, ConfigPath)));
    }

    [TestMethod]
    public void DefaultMountOrderStillUsesFirstReadMount()
    {
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");
        Directory.CreateDirectory(Path.Combine(first, "data", "cfg"));
        Directory.CreateDirectory(Path.Combine(second, "data", "cfg"));
        File.WriteAllText(Path.Combine(first, ConfigPath), "first");
        File.WriteAllText(Path.Combine(second, ConfigPath), "second");

        var fs = new VirtualFS();
        fs.MountDirectory(first);
        fs.MountDirectory(second);
        Assert.AreEqual("first", fs.ReadAllText(ConfigPath));
    }
}
