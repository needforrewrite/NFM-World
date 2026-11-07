using System.Reflection;

namespace NFMWorld.Gameplay;

/// <summary>
/// The lobby this build connects to by default.
///
/// The endpoint is baked in at build time (see <c>LobbyEndpoint</c> in the game csproj) rather
/// than being a setting: a release lobby will not authenticate a debug client, so a Debug build
/// points at a local one. The <c>connect &lt;host&gt; [port]</c> command still reaches any other.
/// </summary>
public static class LobbyEndpoint
{
    public static string Host { get; } = Metadata("NfmwLobbyEndpoint") ?? "localhost";

    public static ushort Port { get; } =
        ushort.TryParse(Metadata("NfmwLobbyPort"), out var port) ? port : (ushort)7000;

    private static string? Metadata(string key)
        => Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value
            : null;
}
