using System.Reflection;

namespace NFMWorld.Gameplay;

/// <summary>
/// The archive this build connects to by default.
///
/// Desktop clients can override the build default at startup.
/// </summary>
public static class ArchiveEndpoint
{
    private static readonly string DefaultUrl = Metadata("NfmwArchiveEndpoint") ?? "http://localhost:5000";

    public static string? ClientEnvironment { get; private set; }

    public static string Url => ClientEnvironment switch
    {
        "live" => "https://nfmw.net",
        "dev" => "http://dev.nfmw.net",
        _ => DefaultUrl
    };

    public static string? ClientStorageSuffix =>
        string.Equals(Url.TrimEnd('/'), DefaultUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            ? null
            : ClientEnvironment;

    public static void SetClientEnvironment(string? environment)
        => ClientEnvironment = environment is "live" or "dev" ? environment : null;

    private static string? Metadata(string key)
        => typeof(ArchiveEndpoint).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value
            : null;
}
