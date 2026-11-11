using System.Buffers;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Library.Test;

[TestClass]
public class RadpackContentTests
{
    private static byte[] Serialize(RadpackAsset asset)
    {
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(asset, writer);
        return writer.WrittenSpan.ToArray();
    }

    private static RadpackMetadata Metadata(string name, RadpackType type)
        => new() { Name = name, CreationDate = DateTimeOffset.UnixEpoch, Type = type };

    [TestMethod]
    public void RoundTrip_Texture_PreservesKindAndPayload()
    {
        var bytes = Serialize(
            new RadpackTexture
            {
                Metadata = Metadata("paint", RadpackType.Texture),
                RawData = [1, 2, 3, 4],
                TextureFormat = RadTextureFormat.Png
            }
        );

        var asset = RadpackSerializer.Deserialize(bytes, RadpackLimits.MaxCompressedBytes, RadpackLimits.MaxDecompressedBytes);

        Assert.AreEqual(RadpackType.Texture, asset.Metadata.Type);
        Assert.IsInstanceOfType<RadpackTexture>(asset);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, ((RadpackTexture)asset).RawData);
    }

    [TestMethod]
    public void RoundTrip_GamemodePackage_PreservesAssembly()
    {
        var assembly = new byte[128];
        assembly[0] = (byte)'M';
        assembly[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(assembly, 0x3C);
        assembly[0x40] = (byte)'P';
        assembly[0x41] = (byte)'E';
        assembly[0x42] = 0;
        assembly[0x43] = 0;

        var bytes = Serialize(
            new RadpackGamemode
            {
                Metadata = Metadata("my-mode", RadpackType.GamemodePackage),
                Assembly = assembly
            }
        );

        var asset = RadpackSerializer.Deserialize(bytes, RadpackLimits.MaxCompressedBytes, RadpackLimits.MaxDecompressedBytes);

        Assert.AreEqual(RadpackType.GamemodePackage, asset.Metadata.Type);
        CollectionAssert.AreEqual(assembly, ((RadpackGamemode)asset).Assembly);
    }

    [TestMethod]
    public void Read_DeclaredTypeThatDoesNotMatchPayload_IsRejected()
    {
        var bytes = Serialize(
            new RadpackTexture
            {
                Metadata = Metadata("paint", RadpackType.Texture),
                RawData = [1],
                TextureFormat = RadTextureFormat.Png
            }
        );

        // Texture in the header, but a texture is not a legal body for a car.
        bytes[3] = (byte)RadpackType.Car;

        var ex = Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackReader.Read(
                bytes,
                "paint",
                RadpackType.Car,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            )
        );
        StringAssert.Contains(ex.Message, "Car");
    }

    [TestMethod]
    public void Read_ModelOfAnotherKind_IsRejected()
    {
        var bytes = Serialize(
            new RadpackRad3d
            {
                Metadata = Metadata("thing", RadpackType.Car),
                Rad = new Rad3d([], false, "thing")
            }
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackReader.Read(
                bytes,
                "thing",
                RadpackType.TrackPiece,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            )
        );
    }

    [TestMethod]
    public void Deserialize_CompressedInputOverTheLimit_IsRejectedBeforeDecompressing()
    {
        var bytes = Serialize(
            new RadpackTexture
            {
                Metadata = Metadata("paint", RadpackType.Texture),
                RawData = new byte[4096],
                TextureFormat = RadTextureFormat.Png
            }
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackSerializer.Deserialize(bytes, maxCompressedBytes: 16, RadpackLimits.MaxDecompressedBytes)
        );
    }

    [TestMethod]
    public void GamemodePackage_ThatIsNotAnAssembly_IsRejected()
    {
        var bytes = Serialize(
            new RadpackGamemode
            {
                Metadata = Metadata("not-a-plugin", RadpackType.GamemodePackage),
                Assembly = [1, 2, 3, 4]
            }
        );

        Assert.ThrowsExactly<InvalidDataException>(
            () => RadpackSerializer.Deserialize(
                bytes,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            )
        );
    }
}
