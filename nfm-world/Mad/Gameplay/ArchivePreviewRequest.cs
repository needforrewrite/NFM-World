namespace NFMWorld.Gameplay;

/// <summary>How a single-asset archive preview renders its radpack.</summary>
public enum ArchivePreviewMode
{
    /// <summary>Whatever the radpack's own header kind is: a track orbits, a model spins.</summary>
    Auto,
    Stage,
    Model
}

/// <summary>
/// One archive radpack to show in place of the game, from the preview page's query string.
/// </summary>
/// <remarks>
/// The browser head's <c>Main</c> cannot take arguments (the wasm runtime calls the assembly's
/// entry point bare), so the page parks the request in a static field before booting and the
/// head branches on it.
/// </remarks>
public sealed record ArchivePreviewRequest(ArchivePreviewMode Mode, string Hash, string? ArchiveUrl)
{
    /// <summary>Parses <c>?preview=</c>, <c>?hash=</c> and <c>?archive=</c>; null means "no preview".</summary>
    /// <remarks>
    /// Lenient about the values: an unknown mode is <see cref="ArchivePreviewMode.Auto"/> and a
    /// hash that is not a SHA-256 is reported by the phase, where the page can see the message.
    /// </remarks>
    public static ArchivePreviewRequest? Parse(string? mode, string? hash, string? archiveUrl)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return null;

        return new ArchivePreviewRequest(
            ParseMode(mode),
            hash.Trim(),
            string.IsNullOrWhiteSpace(archiveUrl) ? null : archiveUrl.Trim()
        );
    }

    private static ArchivePreviewMode ParseMode(string? mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "stage" => ArchivePreviewMode.Stage,
            "model" => ArchivePreviewMode.Model,
            _ => ArchivePreviewMode.Auto
        };
}
