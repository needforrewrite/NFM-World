using System.Net;
using NFMWorld.Archive;

namespace NFMWorld.Archive.Test;

[TestClass]
public class FileStorageTests
{
    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "nfmw-storage-test", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task Put_NamesTheFileByItsOwnHash()
    {
        var storage = new FileStorage(_root);
        var bytes = "hello radpack"u8.ToArray();

        var (hash, relative, size) = await storage.PutAsync((ReadOnlyMemory<byte>)bytes, CancellationToken.None);

        Assert.AreEqual(bytes.Length, (int)size);
        Assert.AreEqual(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), hash);
        Assert.AreEqual($"{hash[..2]}/{hash}", relative);
        Assert.IsTrue(storage.Exists(relative));
    }

    [TestMethod]
    public async Task Put_TwiceWithTheSameBytesYieldsOneFile()
    {
        var storage = new FileStorage(_root);
        var bytes = "same"u8.ToArray();

        var (hash1, path1, _) = await storage.PutAsync((ReadOnlyMemory<byte>)bytes, CancellationToken.None);
        var (hash2, path2, _) = await storage.PutAsync((ReadOnlyMemory<byte>)bytes, CancellationToken.None);

        Assert.AreEqual(hash1, hash2);
        Assert.AreEqual(path1, path2);
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(_root, "incoming")));
    }

    [TestMethod]
    public async Task Put_RefusesAnOversizedStream()
    {
        var storage = new FileStorage(_root);
        using var stream = new MemoryStream(new byte[2048]);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => storage.PutAsync(
                stream,
                maxBytes: 1024,
                CancellationToken.None
            )
        );
    }

    [TestMethod]
    public void OpenRead_RefusesAPathThatEscapesTheRoot()
    {
        var storage = new FileStorage(_root);

        Assert.ThrowsExactly<InvalidOperationException>(() => storage.OpenRead("../../etc/passwd"));
    }
}
