using System.Reflection;

namespace NFMWorld.Gameplay;

/// <summary>
/// The lobby this build connects to by default.
///
/// The client can select a deployed lobby at startup. Otherwise it uses the build metadata.
/// </summary>
public static class LobbyEndpoint
{
    private static readonly string DefaultHost = Metadata("NfmwLobbyEndpoint") ?? "localhost";

    private static readonly ushort DefaultPort =
        ushort.TryParse(Metadata("NfmwLobbyPort"), out var port) ? port : (ushort)7001;

    private static readonly bool DefaultInsecure =
        !bool.TryParse(Metadata("NfmwLobbyInsecure"), out var insecure) || insecure;

    public static string Host => ArchiveEndpoint.ClientEnvironment switch
    {
        "live" => "lobby.nfmw.net",
        "dev" => "lobby-dev.nfmw.net",
        _ => DefaultHost
    };

    public static ushort Port => ArchiveEndpoint.ClientEnvironment is null ? DefaultPort : (ushort)443;
    public static bool Insecure => ArchiveEndpoint.ClientEnvironment is null && DefaultInsecure;

    private static string? Metadata(string key)
        => typeof(LobbyEndpoint).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .LastOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value
            : null;
}
