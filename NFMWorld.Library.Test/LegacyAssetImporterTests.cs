using System.IO.Compression;
using System.Text;
using NFMWorldLibrary.LegacyAssets;
using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

[TestClass]
public class LegacyAssetImporterTests
{
    private const string RadText = "swits(50, 185, 282)\nname(Test Car)";

    private static byte[] Zip(string text)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("car.rad").Open(), Encoding.UTF8);
            writer.Write(text);
        }

        return stream.ToArray();
    }

    /// <summary>Rebuilds the bytes the legacy HTTP layer handed over: a skip of 20, then 40.</summary>
    private static byte[] Windowed(byte[] zip)
    {
        // First 480 bytes are prefixed by 20; everything after by 40.
        var result = new byte[zip.Length + 40];
        for (var i = 0; i < zip.Length; i++) result[i + (i < 480 ? 20 : 40)] = zip[i];
        return result;
    }

    [TestMethod]
    public void DecodeRadq_PlainZip_ReturnsRad()
    {
        Assert.AreEqual(RadText, LegacyAssetImporter.DecodeRadq(Zip(RadText)));
    }

    [TestMethod]
    public void DecodeRadq_LegacyWindowedPayload_ReturnsRad()
    {
        Assert.AreEqual(RadText, LegacyAssetImporter.DecodeRadq(Windowed(Zip(RadText))));
    }

    [TestMethod]
    public void DecodeRadq_NotZip_ReturnsNull()
    {
        Assert.IsNull(LegacyAssetImporter.DecodeRadq(Encoding.UTF8.GetBytes("hello")));
    }

    [TestMethod]
    public void TryReadLocal_MissingFile_ReturnsNull()
    {
        // The VFS has no data/legacy tree in the test output.
        Assert.IsNull(LegacyAssetImporter.TryReadLocal("cars", "nonexistent-car"));
    }

    [TestMethod]
    public void DecodeEmbeddedRad_ZlibPayload_ReturnsRad()
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(Encoding.UTF8.GetBytes(RadText));

        Assert.AreEqual(RadText, LegacyAssetImporter.DecodeEmbeddedRad(Convert.ToBase64String(buffer.ToArray())));
    }

    [TestMethod]
    public void DecodeEmbeddedRad_RawDeflatePayload_ReturnsRad()
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(Encoding.UTF8.GetBytes(RadText));

        Assert.AreEqual(RadText, LegacyAssetImporter.DecodeEmbeddedRad(Convert.ToBase64String(buffer.ToArray())));
    }

    [TestMethod]
    public void DecodeEmbeddedRad_NotBase64_ReturnsNull()
    {
        Assert.IsNull(LegacyAssetImporter.DecodeEmbeddedRad("not base64!"));
    }

    [TestMethod]
    public void TryFetch_UntrustedName_ReturnsNull()
    {
        // Names come from a stage file, so anything that could escape the fixed URL template is
        // refused before a request is made (these return null immediately, without network).
        Assert.IsNull(LegacyAssetImporter.TryFetch("cars", "../../secret"));
        Assert.IsNull(LegacyAssetImporter.TryFetch("cars", "a/b"));
        Assert.IsNull(LegacyAssetImporter.TryFetch("cars", "a?b"));
        Assert.IsNull(LegacyAssetImporter.TryFetch("cars", ""));
        Assert.IsNull(LegacyAssetImporter.TryFetchModel("http://example.com/x"));
        Assert.IsNull(LegacyAssetImporter.TryFetchModel("bare-label"));
    }
}
