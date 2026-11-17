using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Library.Test;

[TestClass]
public class ContentCacheTests
{
    static ContentCacheTests()
    {
        ContentCache.MountAt(Path.Combine(Path.GetTempPath(), "nfmw-content-cache-tests"));
    }

    private sealed class FakeHandler(Func<string, byte[]?> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var hash = request.RequestUri!.AbsolutePath.Split('/').Last();
            var bytes = respond(hash);
            return Task.FromResult(
                bytes is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            );
        }
    }

    private static (byte[] Bytes, string Hash) CarRadpack()
    {
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackRad3d
            {
                Metadata = new RadpackMetadata
                {
                    Name = Guid.NewGuid().ToString("N"),
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Car
                },
                Rad = MinimalCar()
            },
            writer
        );
        var bytes = writer.WrittenSpan.ToArray();
        return (bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    /// <summary>
    /// A real car, because the cache refuses one the physics cannot drive: four wheels and a
    /// complete stat set, which <c>new Rad3d([], false, ...)</c> is not.
    /// </summary>
    private static Rad3d MinimalCar() => RadParser.ParseRad(
        """
        swits(80,200,300)
        acelf(11.0,7.5,4.0)
        handb(7)
        airs(1.3)
        airc(100)
        turn(7)
        grip(25.0)
        bounce(1.1)
        simag(0.9)
        moment(1.5)
        comprad(0.5)
        push(2)
        revpush(2)
        lift(30)
        revlift(0)
        powerloss(3000000)
        flipy(-63)
        msquash(3)
        clrad(5500)
        dammult(0.38)
        maxmag(5800)
        dishandle(1.0)
        outdam(0.85)
        name(Test Car)
        <p>
        c(120,120,120)
        p(0,0,0)
        p(40,0,0)
        p(0,0,40)
        </p>
        w(-42,2,72,11,17,12)
        w(42,2,72,11,-17,12)
        w(-42,2,-84,0,17,12)
        w(42,2,-84,0,-17,12)
        """,
        "car"
    );

    private static ArchiveContentClient Client(FakeHandler handler)
        => new("https://archive.example/", new HttpClient(handler));

    private static (byte[] Bytes, string Hash) TrackRadpack()
    {
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackTrack
            {
                Metadata = new RadpackMetadata
                {
                    Name = Guid.NewGuid().ToString("N"),
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Track
                },
                Stage = new StageLoader()
            },
            writer
        );
        var bytes = writer.WrittenSpan.ToArray();
        return (bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [TestMethod]
    public async Task Prepare_PublishesAVerifiedAssetThatTheVfsCanRead()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);

        var result = await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Error);
        Assert.IsTrue(ContentCache.IsPrepared(hash));
        Assert.IsNotNull(RadpackReader.TryRead(ContentCache.PathForHash(hash), RadpackType.Car, out var error), error);
    }

    [TestMethod]
    public async Task Enumeration_RespectsTheRequestedDirectoryAndFilePattern()
    {
        var (bytes, hash) = CarRadpack();
        using var client = Client(new FakeHandler(_ => bytes));
        Assert.IsTrue((await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None)).Ok);

        Assert.IsEmpty(TheVFS.VFS.EnumerateFiles("data/tts", "*.timetrial", SearchOption.AllDirectories)
            .Where(path => path.StartsWith("archive/", StringComparison.Ordinal))
        );
        Assert.IsEmpty(TheVFS.VFS.EnumerateFiles("archive", "*.timetrial", SearchOption.AllDirectories));
        CollectionAssert.Contains(
            TheVFS.VFS.EnumerateFiles("archive", "*", SearchOption.TopDirectoryOnly).ToArray(),
            ContentCache.PathForHash(hash)
        );
        CollectionAssert.Contains(
            TheVFS.VFS.EnumerateFiles("", "*", SearchOption.AllDirectories).ToArray(),
            ContentCache.PathForHash(hash)
        );
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.Unauthorized)]
    public async Task Metadata_DistinguishesMissingContentFromServerFailures(HttpStatusCode status)
    {
        using var http = new HttpClient(new MetadataFailureHandler(status));
        using var client = new ArchiveContentClient("https://archive.example/", http);
        if (status == HttpStatusCode.NotFound)
            Assert.IsNull(await client.MetadataAsync(new string('a', 64), CancellationToken.None));
        else
        {
            var error = await Assert.ThrowsExactlyAsync<HttpRequestException>(
                () => client.MetadataAsync(new string('a', 64), CancellationToken.None)
            );
            Assert.AreEqual(status, error.StatusCode);
        }
    }

    private sealed class MetadataFailureHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status));
    }

    [TestMethod]
    public async Task Prepare_SecondCall_ServesFromTheCache()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);

        await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);
        var callsAfterFirst = handler.Calls;
        var second = await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);

        Assert.IsTrue(second.Ok, second.Error);
        Assert.AreEqual(callsAfterFirst, handler.Calls);
    }

    [TestMethod]
    public async Task Prepare_AcceptsTheCanonicalArchiveReference()
    {
        // The wire carries `archive/<sha256>`, never a bare hash: this is what a lobby or game
        // master actually passes, and it used to be refused as "Not a content hash."
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);
        var reference = RadpackReader.ArchivePrefix + hash.ToUpperInvariant();

        var result = await ContentCache.PrepareAsync(client, reference, AssetKind.Car, CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Error);
        Assert.IsTrue(ContentCache.IsPrepared(reference));
        Assert.IsTrue(ContentCache.IsPrepared(hash));
        Assert.IsNotNull(RadpackReader.TryRead(ContentCache.PathForHash(reference), RadpackType.Car, out var error), error);
    }

    [TestMethod]
    public async Task Prepare_StageByReference_IsReadableAsATrack()
    {
        // What the game master does for a custom stage: prepare the lobby's reference, then
        // construct the stage synchronously through the mount.
        var (bytes, hash) = TrackRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);
        var reference = RadpackReader.ArchivePrefix + hash;

        var result = await ContentCache.PrepareAsync(client, reference, AssetKind.Stage, CancellationToken.None);
        Assert.IsTrue(result.Ok, result.Error);

        Assert.IsTrue(ContentCache.IsPrepared(reference));
        var stage = RadpackReader.TryReadTrack(reference, out var error);
        Assert.IsNotNull(stage, error);
    }

    [TestMethod]
    public async Task Prepare_RefusesBytesWhoseHashDoesNotMatch()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes.Reverse().ToArray());
        using var client = Client(handler);

        var result = await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);

        Assert.IsFalse(result.Ok);
        Assert.IsFalse(ContentCache.IsPrepared(hash));
    }

    [TestMethod]
    public async Task Prepare_RefusesAnAssetOfTheWrongKind()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);

        var result = await ContentCache.PrepareAsync(client, hash, AssetKind.Stage, CancellationToken.None);

        Assert.IsFalse(result.Ok);
        Assert.IsFalse(ContentCache.IsPrepared(hash));
    }

    [TestMethod]
    public async Task Prepare_RecoversFromACorruptCachedFile()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);

        await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);
        File.WriteAllBytes(ContentCache.Root is { } root ? Path.Combine(root, hash) : "", [1, 2, 3]);

        var callsBefore = handler.Calls;
        var result = await ContentCache.PrepareAsync(client, hash, AssetKind.Car, CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Error);
        Assert.AreEqual(callsBefore + 1, handler.Calls);
        Assert.IsNotNull(RadpackReader.TryRead(ContentCache.PathForHash(hash), RadpackType.Car, out _));
    }

    [TestMethod]
    public async Task Download_RefusesMoreThanTheByteCap()
    {
        var (bytes, hash) = CarRadpack();
        var handler = new FakeHandler(_ => bytes);
        using var client = Client(handler);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => client.DownloadAsync(hash, 16, CancellationToken.None));
    }

    [TestMethod]
    public void Decode_RejectsANonCanonicalHash()
    {
        var (bytes, _) = CarRadpack();

        Assert.IsFalse(ContentCache.TryDecode(bytes, "not-a-hash", RadpackType.Car, out _));
    }
}
