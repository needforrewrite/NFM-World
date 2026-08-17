using System.Buffers;
using System.IO.Compression;
using MemoryPack;
using MemoryPack.Compression;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Radpack;

public static class RadpackSerializer
{
    public static void Serialize(RadpackAsset asset, IBufferWriter<byte> writer)
    {
        var head = writer.GetSpan(4);
        head[0] = (byte)'R';
        head[1] = (byte)'A';
        head[2] = (byte)'D';
        head[3] = (byte)asset.Metadata.Type;
        writer.Advance(4);
        
        using var compressor = new BrotliCompressor(CompressionLevel.Fastest);
        MemoryPackSerializer.Serialize(compressor, asset, MemoryPackHelpers.Options);
        compressor.CopyTo(writer);
    }
    
    public static RadpackAsset Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        
        if (data[0] != 'R' || data[1] != 'A' || data[2] != 'D')
        {
            throw new InvalidDataException("Not a Radpack asset");
        }

        var type = data[3];
        if (type > (byte)RadpackType.Max)
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        
        using var decompressor = new BrotliDecompressor();
        var decompData = decompressor.Decompress(data[4..]);
        var result = MemoryPackSerializer.Deserialize<RadpackAsset>(decompData);
        if (result == null)
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        result.Metadata.Type = (RadpackType)type;
        return result;
        
    }

    public static RadpackAsset Deserialize(ReadOnlySequence<byte> data)
    {
        var reader = new SequenceReader<byte>(data.Slice(0, 4));
        if (!reader.TryRead(out var r) || r != 'R')
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        if (!reader.TryRead(out var a) || a != 'A')
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        if (!reader.TryRead(out var d) || d != 'D')
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        if (!reader.TryRead(out var type) || type > (byte)RadpackType.Max)
        {
            throw new InvalidDataException("Not a Radpack asset");
        }

        using var decompressor = new BrotliDecompressor();
        var decompData = decompressor.Decompress(data.Slice(4));
        var result = MemoryPackSerializer.Deserialize<RadpackAsset>(decompData);
        if (result == null)
        {
            throw new InvalidDataException("Not a Radpack asset");
        }
        result.Metadata.Type = (RadpackType)type;
        return result;
    }
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(RadpackTrack))]
[MemoryPackUnion(1, typeof(RadpackRad3d))]
[MemoryPackUnion(2, typeof(RadpackTexture))]
[MemoryPackUnion(3, typeof(RadpackLua))]
public abstract partial class RadpackAsset
{
    [MemoryPackOrder(0)] public required RadpackMetadata Metadata { get; set; }
}

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class RadpackMetadata
{
    [MemoryPackOrder(0)] public required string Name { get; init; }
    [MemoryPackOrder(1)] public string? Description { get; init; }
    [MemoryPackOrder(2)] public required DateTimeOffset CreationDate { get; init; }
    
    [MemoryPackIgnore] public RadpackType Type { get; set; }
}

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class RadpackTrack : RadpackAsset
{
    [MemoryPackOrder(1)] public required StageLoader Stage;
}

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class RadpackRad3d : RadpackAsset
{
    [MemoryPackOrder(1)] public required Rad3d Rad;
}

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class RadpackTexture : RadpackAsset
{
    [BrotliFormatter(CompressionLevel.Optimal)]
    [MemoryPackOrder(1)]
    public required byte[] RawData;

    [MemoryPackOrder(2)]
    public RadTextureFormat TextureFormat;
}

public enum LuaScriptKind : byte
{
    Gamemode,
    Ai
}

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class RadpackLua : RadpackAsset
{
    [MemoryPackOrder(1)] public required LuaScriptKind Kind;
    [MemoryPackOrder(2)] public required Dictionary<string, string> Files;
}

public enum RadTextureFormat : byte
{
    Png,
    Dds
}

public enum RadpackType : byte
{
    Track,
    Car,
    TrackPiece,
    Texture,
    Campaign,
    Wheel,
    LuaScript,
    Max = LuaScript
}