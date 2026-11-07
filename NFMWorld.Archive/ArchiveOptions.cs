namespace NFMWorld.Archive;

/// <summary>
/// Archive configuration, read from the <c>Archive</c> configuration section.
/// </summary>
public sealed class ArchiveOptions
{
    /// <summary>SQLite connection string.</summary>
    public string Database { get; set; } = "Data Source=archive.db";

    /// <summary>Directory holding uploaded files. Files are the source of truth; the database indexes them.</summary>
    public string StorageRoot { get; set; } = "storage";

    /// <summary>Public origin uploaded files are served from. Must differ from this app's origin.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5288";

    /// <summary>
    /// Keys trusted to report match results, in <c>id=base64secret</c> form, comma separated.
    /// The lobby signs with one of these; nothing else may write ratings.
    /// </summary>
    public string TrustedServerKeys { get; set; } = "";

    /// <summary>Discord application credentials. Registration is disabled when either is empty.</summary>
    public string DiscordClientId { get; set; } = "";
    public string DiscordClientSecret { get; set; } = "";

    /// <summary>Origin the Discord OAuth flow returns to; must match the redirect URI registered with Discord.</summary>
    public string DiscordRedirectUri { get; set; } = "";

    /// <summary>Cookie/session lifetime.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(28);

    /// <summary>Disables the registration and upload endpoints, for a read-only or private deployment.</summary>
    public bool ReadOnly { get; set; }
}
