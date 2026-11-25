using NFMWorld.Archive.Data;

namespace NFMWorld.Archive;

/// <summary>
/// The link from an upload to the browser build's standalone preview page. Both pages that list
/// uploads build theirs here, so the query string lives in one place.
/// </summary>
/// <remarks>
/// The preview runs on its own origin, so the page also names the archive to download from: the
/// game fetches <c>{PublicBaseUrl}/assets/{hash}</c> cross-origin, which is what
/// <see cref="CorsOrigins"/> admits.
/// </remarks>
public static class AssetPreview
{
    /// <summary>The <c>preview=</c> value for a stored kind, or null when it has no 3D view.</summary>
    public static string? Mode(AssetType type) => type switch
    {
        AssetType.Stage => "stage",
        AssetType.Car or AssetType.StagePiece or AssetType.Wheel => "model",
        _ => null
    };

    /// <summary>Where an upload opens in the browser build, or null when it cannot be previewed.</summary>
    public static string? Url(ArchiveOptions options, AssetType type, string contentHash)
    {
        if (Mode(type) is not { } mode || string.IsNullOrWhiteSpace(options.GamePreviewUrl))
            return null;

        var root = options.GamePreviewUrl.TrimEnd('/');
        return $"{root}/index.html?hash={Uri.EscapeDataString(contentHash)}"
               + $"&preview={mode}&archive={Uri.EscapeDataString(options.PublicBaseUrl)}";
    }
}
