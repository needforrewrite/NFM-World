using System.Reflection;

namespace NFMWorld.Gameplay;

/// <summary>
/// The archive this build connects to by default.
///
/// The endpoint is baked in at build time (see <c>ArchiveEndpoint</c> in the game csproj).
/// </summary>
public static class ArchiveEndpoint
{
    public static string Url { get; } = Metadata("NfmwArchiveEndpoint") ?? "http://localhost:5000";

    private static string? Metadata(string key)
        => Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value
            : null;
}