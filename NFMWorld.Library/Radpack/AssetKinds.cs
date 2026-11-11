namespace NFMWorldLibrary.Radpack;

/// <summary>An archive asset kind. The numeric values match the archive's stored <c>AssetType</c>.</summary>
public enum AssetKind : byte
{
    Car = 0,
    Stage = 1,
    StagePiece = 2,
    Wheel = 3,
    Texture = 4,
    Replay = 5,
    Gamemode = 6,
    Ai = 7
}

/// <summary>
/// The one mapping between an archive asset kind and the radpack kind that carries it.
/// </summary>
/// <remarks>
/// The archive's stored type codes are stable and shared by the API, the website and the
/// validator; the radpack header uses different names for two of them, which is what used to make
/// every stage upload fail the type check. Codes match the archive's <c>AssetType</c> byte.
/// </remarks>
public static class AssetKinds
{
    public const byte Car = 0;
    public const byte Stage = 1;
    public const byte StagePiece = 2;
    public const byte Wheel = 3;
    public const byte Texture = 4;
    public const byte Replay = 5;
    public const byte Gamemode = 6;
    public const byte Ai = 7;

    public static RadpackType? ToRadpackType(AssetKind kind) => ToRadpackType((byte)kind);

    public static bool IsDataAsset(AssetKind kind) => IsDataAsset((byte)kind);

    /// <summary>The radpack header kind for a stored asset type, or null when it has no radpack form.</summary>
    public static RadpackType? ToRadpackType(byte assetType)
        => assetType switch
        {
            Car => RadpackType.Car,
            Stage => RadpackType.Track,
            StagePiece => RadpackType.TrackPiece,
            Wheel => RadpackType.Wheel,
            Texture => RadpackType.Texture,
            _ => null
        };

    /// <summary>The stored asset type for a radpack header kind, or null when it is not publishable.</summary>
    public static byte? FromRadpackType(RadpackType radpackType)
        => radpackType switch
        {
            RadpackType.Car => Car,
            RadpackType.Track => Stage,
            RadpackType.TrackPiece => StagePiece,
            RadpackType.Wheel => Wheel,
            RadpackType.Texture => Texture,
            _ => null
        };

    /// <summary>Whether the kind names a data asset (a model or stage) rather than code or a replay.</summary>
    public static bool IsDataAsset(byte assetType) => ToRadpackType(assetType) is not null;

    /// <summary>Whether the kind may be published at all. Executable and replay payloads may not.</summary>
    public static bool IsPublishable(byte assetType) => IsDataAsset(assetType);

    /// <summary>Whether a legacy <c>.rad</c> file may claim this kind.</summary>
    public static bool IsLegacyRadKind(byte assetType) => assetType is Car or StagePiece or Wheel;

    public static string Name(byte assetType)
        => assetType switch
        {
            Car => "Car",
            Stage => "Stage",
            StagePiece => "StagePiece",
            Wheel => "Wheel",
            Texture => "Texture",
            Replay => "Replay",
            Gamemode => "Gamemode",
            Ai => "Ai",
            _ => assetType.ToString()
        };

    public static bool TryParseName(string? text, out byte assetType)
    {
        assetType = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        switch (text.Trim().ToLowerInvariant())
        {
            case "car": assetType = Car; return true;
            case "stage": assetType = Stage; return true;
            case "stagepiece" or "trackpiece": assetType = StagePiece; return true;
            case "wheel": assetType = Wheel; return true;
            case "texture": assetType = Texture; return true;
            case "replay": assetType = Replay; return true;
            case "gamemode": assetType = Gamemode; return true;
            case "ai": assetType = Ai; return true;
            default: return false;
        }
    }
}
