using System.IO.Compression;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Library.Test;

[TestClass]
public class RadpackLimitsTests
{
    // 8 MB of zeroes compresses to a few kilobytes, which is the shape of a deflate bomb.
    private static byte[] DeflateBomb(int decompressedBytes)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(new byte[decompressedBytes]);
        return output.ToArray();
    }

    [TestMethod]
    public void Deserialize_RefusesPayloadThatInflatesPastTheCap()
    {
        var compressed = DeflateBomb(8 * 1024 * 1024);
        var payload = new byte[compressed.Length + 4];
        payload[0] = (byte)'R';
        payload[1] = (byte)'A';
        payload[2] = (byte)'D';
        payload[3] = (byte)RadpackType.Car;
        compressed.CopyTo(payload, 4);

        Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackSerializer.Deserialize(payload, maxDecompressedBytes: 1024 * 1024));
    }

    [TestMethod]
    public void Deserialize_RejectsUnknownTypeByteBeforeDecompressing()
    {
        var payload = new byte[] { (byte)'R', (byte)'A', (byte)'D', (byte)RadpackType.Max + 1 };

        Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackSerializer.Deserialize(payload, maxDecompressedBytes: 1024));
    }
}
