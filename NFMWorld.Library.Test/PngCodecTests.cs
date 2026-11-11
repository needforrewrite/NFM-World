using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using NFMWorld.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class PngCodecTests
{
    [TestMethod]
    public void Encode_ProducesZlibDataAndValidChunkChecksums()
    {
        byte[] pixels = [255, 0, 0, 255, 0, 255, 0, 128];
        using var output = new MemoryStream();
        PngCodec.Encode(output, pixels, 2, 1);
        var png = output.ToArray();
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);

        var foundData = false;
        for (var offset = 8; offset < png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var chunk = png.AsSpan(offset + 4, length + 4);
            var checksum = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + length + 8, 4));
            Assert.AreEqual(Crc32.HashToUInt32(chunk), checksum);
            if (Encoding.ASCII.GetString(chunk[..4]) == "IDAT")
            {
                using var compressed = new MemoryStream(chunk[4..].ToArray());
                using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                zlib.CopyTo(raw);
                CollectionAssert.AreEqual(new byte[] { 0, 255, 0, 0, 255, 0, 255, 0, 128 }, raw.ToArray());
                foundData = true;
            }

            offset += length + 12;
        }

        Assert.IsTrue(foundData);
    }
}
