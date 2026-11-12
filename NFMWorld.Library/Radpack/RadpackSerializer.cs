using System.Buffers;
using TightPack;
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

        var body = TightPackSerializer.Serialize<RadpackAsset>(asset);
        Deflate.Compress(body, writer);
    }

    public static RadpackAsset Deserialize(ReadOnlySpan<byte> data)
        => Deserialize(data, RadpackLimits.MaxCompressedBytes, RadpackLimits.MaxDecompressedBytes);

    /// <summary>
    /// Deserializes an asset that arrived from an untrusted source, capping both the compressed
    /// and the decompressed size. The header is tiny relative to the payload it can expand to,
    /// so the decompression ceiling has to be enforced while reading rather than afterwards.
    /// </summary>
    public static RadpackAsset Deserialize(ReadOnlySpan<byte> data, int maxDecompressedBytes)
        => Deserialize(data, RadpackLimits.MaxCompressedBytes, maxDecompressedBytes);

    public static RadpackAsset Deserialize(ReadOnlySpan<byte> data, int maxCompressedBytes, int maxDecompressedBytes)
    {
        if (data.Length > maxCompressedBytes)
            throw new InvalidDataException($"Radpack exceeds {maxCompressedBytes} compressed bytes");

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
            throw new InvalidDataException($"Radpack declares unknown type {type}");
        }

        var decompData = Deflate.Decompress(data[4..], maxDecompressedBytes);
        RadpackAsset? result;
        try
        {
            result = TightPackSerializer.Deserialize<RadpackAsset>(decompData);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("Radpack payload could not be read", ex);
        }

        if (result == null || result.Metadata == null)
        {
            throw new InvalidDataException("Radpack payload could not be read");
        }

        var declared = (RadpackType)type;
        if (!RadpackTypes.MatchesPayload(declared, result))
        {
            throw new InvalidDataException(
                $"Radpack declares {declared} but contains a {result.GetType().Name}"
            );
        }

        // The type byte is the authority: a payload's own union tag only selects how to read it,
        // and one payload type (Rad3d) is shared by cars, stage pieces and wheels.
        result.Metadata.Type = declared;
        RadpackTypes.ValidatePayload(result);
        return result;
    }

    /// <summary>Equivalent to the sequence overload, with an output ceiling.</summary>
    public static RadpackAsset Deserialize(ReadOnlySequence<byte> data, int maxDecompressedBytes)
        => Deserialize(data.ToArray(), maxDecompressedBytes);
}

/// <summary>
/// Maps between the radpack header type and the payload type that may carry it.
/// </summary>
public static class RadpackTypes
{
    /// <summary>Whether <paramref name="payload"/> is a legal body for the declared type.</summary>
    public static bool MatchesPayload(RadpackType declared, RadpackAsset payload)
        => payload switch
        {
            RadpackTrack => declared == RadpackType.Track,
            RadpackRad3d => declared is RadpackType.Car or RadpackType.TrackPiece or RadpackType.Wheel,
            RadpackTexture => declared == RadpackType.Texture,
            RadpackLua => declared == RadpackType.LuaScript,
            RadpackGamemode => declared == RadpackType.GamemodePackage,
            _ => false
        };

    /// <summary>
    /// Rejects a payload that is readable but cannot be used, so a corrupt package fails where it
    /// is loaded instead of silently behaving as an empty one.
    /// </summary>
    public static void ValidatePayload(RadpackAsset asset)
    {
        if (asset is RadpackGamemode { Assembly: var assembly } && !IsManagedAssembly(assembly))
            throw new InvalidDataException("The gamemode package does not contain a .NET assembly");
    }

    /// <summary>Checks the DOS/PE signature without loading or executing anything.</summary>
    public static bool IsManagedAssembly(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x40 || bytes[0] != 'M' || bytes[1] != 'Z')
            return false;

        var peOffset = BitConverter.ToInt32(bytes[0x3C..0x40]);
        return peOffset > 0
               && peOffset <= bytes.Length - 4
               && bytes[peOffset] == 'P'
               && bytes[peOffset + 1] == 'E'
               && bytes[peOffset + 2] == 0
               && bytes[peOffset + 3] == 0;
    }
}

[TightPackable]
[TightPackUnion(0, typeof(RadpackTrack))]
[TightPackUnion(1, typeof(RadpackRad3d))]
[TightPackUnion(2, typeof(RadpackTexture))]
[TightPackUnion(3, typeof(RadpackLua))]
// Appended: older payload tags keep their numbers so existing packages stay readable.
[TightPackUnion(4, typeof(RadpackGamemode))]
public abstract partial class RadpackAsset
{
    [TightPackOrder(0)] public required RadpackMetadata Metadata { get; set; }
}

[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackMetadata
{
    [TightPackOrder(0)] public required string Name { get; init; }
    [TightPackOrder(1)] public string? Description { get; init; }
    [TightPackOrder(2)] public required DateTimeOffset CreationDate { get; init; }

    [TightPackIgnore] public RadpackType Type { get; set; }
}

[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackTrack : RadpackAsset
{
    [TightPackOrder(1)] public required StageLoader Stage;
}

[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackRad3d : RadpackAsset
{
    [TightPackOrder(1)] public required Rad3d Rad;
}

[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackTexture : RadpackAsset
{
    // Was [BrotliFormatter], which is a native call and throws on the browser. Plain bytes instead:
    // an image is already compressed, so the outer deflate stream carries it at almost its own size
    // and the per-field layer was buying little over the format's own compression.
    [TightPackOrder(1)] public required byte[] RawData;

    [TightPackOrder(2)] public RadTextureFormat TextureFormat;
}

public enum LuaScriptKind : byte
{
    Gamemode,
    Ai
}

[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackLua : RadpackAsset
{
    [TightPackOrder(1)] public required LuaScriptKind Kind;

    /// <summary>
    /// The files, with path separator replaced with .
    /// </summary>
    [TightPackOrder(2)] public required Dictionary<string, string> Files;
}

/// <summary>
/// A native gamemode package: one managed assembly, loaded through the plugin sandbox like a
/// <c>gamemode.dll</c>. Never extracted and never executed by browsing.
/// </summary>
[TightPackable(GenerateType.VersionTolerant)]
public partial class RadpackGamemode : RadpackAsset
{
    [TightPackOrder(1)] public required byte[] Assembly;
}

public enum RadTextureFormat : byte
{
    Png,
    Dds
}

/// <summary>
/// The header byte of a radpack. New kinds are appended; the numbers of existing kinds are
/// part of the on-disk format and must not change.
/// </summary>
public enum RadpackType : byte
{
    Track,
    Car,
    TrackPiece,
    Texture,
    Campaign,
    Wheel,
    LuaScript,
    GamemodePackage,
    Max = GamemodePackage
}
