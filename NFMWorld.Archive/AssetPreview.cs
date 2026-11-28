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
    /// <summary>
    /// One upload as the shell needs it: the frame URL for a cold load, and the parts the running
    /// frame is told to switch to when the page navigates without reloading.
    /// </summary>
    public sealed record PreviewRequest(string Url, string Hash, string Mode, string ArchiveUrl);

    /// <summary>The <c>preview=</c> value for a stored kind, or null when it has no 3D view.</summary>
    public static string? Mode(AssetType type) => type switch
    {
        AssetType.Stage => "stage",
        AssetType.Car or AssetType.StagePiece or AssetType.Wheel => "model",
        _ => null
    };

    /// <summary>The preview for an upload, or null when it cannot be previewed.</summary>
    public static PreviewRequest? Request(ArchiveOptions options, AssetType type, string contentHash)
    {
        if (Mode(type) is not { } mode || string.IsNullOrWhiteSpace(options.GamePreviewUrl))

            return null;

        var root = options.GamePreviewUrl.TrimEnd('/');
        var archive = options.PublicBaseUrl;

        return new PreviewRequest(
            $"{root}/index.html?hash={Uri.EscapeDataString(contentHash)}"
            + $"&preview={mode}&archive={Uri.EscapeDataString(archive)}",
            contentHash,
            mode,
            archive
        );
    }

    /// <summary>Where an upload opens in the browser build, or null when it cannot be previewed.</summary>
    public static string? Url(ArchiveOptions options, AssetType type, string contentHash)
        => Request(options, type, contentHash)?.Url;

    /// <summary>
    /// The scheme and host the preview is served from, for the page's <c>frame-src</c>. Null when no
    /// preview host is configured, in which case nothing is framed either.
    /// </summary>
    public static string? FrameOrigin(ArchiveOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.GamePreviewUrl))

            return null;

        // A bare "game.example" would otherwise be parsed as a path and read as a same-origin frame.
        var url = options.GamePreviewUrl.Trim();
        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Authority}"
            : null;
    }
}
