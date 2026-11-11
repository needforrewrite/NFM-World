using System.Net;
using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

[TestClass]
public class ArchiveMountTests
{
    private const string Hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Path = "archive/" + Hash;

    private sealed class ArchiveHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new ByteArrayContent("radpack-bytes"u8.ToArray())
            });
        }
    }

    [TestMethod]
    public void FetchFileSystem_ReadsByHashWithoutListing()
    {
        using var handler = new ArchiveHandler();
        using var http = new HttpClient(handler);
        var fs = ArchiveMount.Create(new Uri("https://archive.example/"), http);

        Assert.IsFalse(fs.FileExists(Path));
        Assert.IsFalse(fs.DirectoryExists("archive"));
        Assert.IsEmpty(handler.Requests);

        using var stream = fs.OpenRead(Path);
        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.AreEqual("radpack-bytes", reader.ReadToEnd());
        Assert.IsTrue(fs.FileExists(Path));
        CollectionAssert.AreEqual(new[] { new Uri("https://archive.example/assets/" + Hash) }, handler.Requests);
    }

    [TestMethod]
    public void FetchFileSystem_EnumeratesOnlyPreviouslyFetchedAssets()
    {
        using var handler = new ArchiveHandler();
        using var http = new HttpClient(handler);
        var fs = ArchiveMount.Create(new Uri("https://archive.example/"), http);

        Assert.IsEmpty(fs.EnumerateFiles("archive", "*", SearchOption.AllDirectories));
        Assert.IsEmpty(handler.Requests);
        using var stream = fs.OpenRead(Path);
        CollectionAssert.AreEqual(
            new[] { Path },
            fs.EnumerateFiles("archive", "*", SearchOption.AllDirectories).ToArray()
        );
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    [DataRow("data/models/car.rad")]
    [DataRow("archive/Car/mycar")]
    [DataRow("archive/../assets/search")]
    [DataRow("archive/abcdef0123456789abcdef0123456789abcdef0123456789abcdef012345678g")]
    [DataRow("archive/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789?limit=100")]
    public void InvalidOrUnrelatedPath_DoesNotContactArchive(string path)
    {
        using var handler = new ArchiveHandler();
        using var http = new HttpClient(handler);
        var fs = ArchiveMount.Create(new Uri("https://archive.example/"), http);

        Assert.IsNull(fs.OpenRead(path));
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public void MissingAsset_IsNotRegisteredAndCanBeRetried()
    {
        using var handler = new ArchiveHandler { Status = HttpStatusCode.NotFound };
        using var http = new HttpClient(handler);
        var fs = ArchiveMount.Create(new Uri("https://archive.example/"), http);

        Assert.IsNull(fs.OpenRead(Path));
        Assert.IsFalse(fs.FileExists(Path));
        Assert.IsEmpty(fs.EnumerateFiles("archive", "*", SearchOption.AllDirectories));
        handler.Status = HttpStatusCode.OK;
        using var stream = fs.OpenRead(Path);
        Assert.IsNotNull(stream);
        Assert.IsTrue(fs.FileExists(Path));
    }
}
