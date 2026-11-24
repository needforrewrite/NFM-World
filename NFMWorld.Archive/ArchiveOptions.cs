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

    /// <summary>Path to the validator executable. Time-trial and radpack validation are refused when empty.</summary>
    public string? ValidatorPath { get; set; }

    /// <summary>Directory the validator runs in; it needs the game's data tree beside it.</summary>
    public string? ValidatorWorkingDirectory { get; set; }

    /// <summary>Hard wall clock for one validation. The validator is killed when it expires.</summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Grace between a deletion request and the sweep that erases the account.</summary>
    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Origins allowed to fetch asset downloads cross-origin, comma separated; <c>*</c> allows any
    /// page. Assets are public, credential-free downloads, so a page elsewhere - the standalone
    /// radpack preview - can read them without exposing anything a direct request would not.
    /// </summary>
    public string CorsOrigins { get; set; } = "*";

    /// <summary>
    /// Cloudflare zone whose edge caches <c>/assets/&lt;hash&gt;</c>; empty disables purging.
    /// </summary>
    public string? CloudflareZoneId { get; set; }

    /// <summary>
    /// API token with <c>Zone → Cache Purge → Purge</c> on that zone. Never logged. The purge URL is
    /// built from <see cref="PublicBaseUrl"/>, so it has to be the hostname players download from.
    /// </summary>
    public string? CloudflareApiToken { get; set; }
}
