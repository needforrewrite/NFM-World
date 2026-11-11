using NFMWorldLibrary.Radpack;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// What a multiplayer race may use: genuine shipped content, or public archive content named by
/// its canonical hash. A local custom file is never accepted, and a name is never trusted to mean
/// the stock asset it looks like.
/// </summary>
/// <remarks>
/// The lobby applies this at the boundary and the game master applies it again before creating a
/// race, because the lobby's copy of the roster is only a claim from a client. Enforcing it in one
/// place would make the UI the policy.
/// </remarks>
public static class MultiplayerContentPolicy
{
    public const string DefaultCar = "nfmm/radicalone";

    private static readonly string[] StockCarPrefixes = ["nfmm/", "world/", "elo/", "football/"];

    private static readonly string[] RejectedStagePrefixes = ["user/"];

    public static bool IsCanonicalArchiveId(string? id)
        => id is not null && RadpackReader.IsArchiveId(id);

    /// <summary>A shipped car's id, e.g. <c>nfmm/2000tornados</c>.</summary>
    public static bool IsStockCar(string? id)
        => id is not null
           && StockCarPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))
           && IsSafeId(id);

    public static bool IsStockStage(string? id)
        => id is not null
           && IsSafeId(id)
           && !RejectedStagePrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal));

    public static bool IsAcceptedCar(string? id) => IsCanonicalArchiveId(id) || IsStockCar(id);

    public static bool IsAcceptedStage(string? id) => IsCanonicalArchiveId(id) || IsStockStage(id);

    /// <summary>Keeps a usable car when a client sends one that cannot be raced.</summary>
    public static string CoerceCar(string? id) => IsAcceptedCar(id) ? Normalize(id!) : DefaultCar;

    /// <summary>
    /// The canonical archive assets every participant has to hold before the race can load: the
    /// stage and each distinct car. Shipped ids need no preparation; they are already on disk.
    /// </summary>
    public static PreparedContent[] RequiredArchiveContent(MatchGameplayInfo info)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var required = new List<PreparedContent>(info.Players.Count + 1);

        if (IsCanonicalArchiveId(info.StageName) && seen.Add(info.StageName))
            required.Add(new PreparedContent { Id = info.StageName, Kind = (byte)AssetKind.Stage });

        foreach (var player in info.Players.Values)
        {
            if (IsCanonicalArchiveId(player.CarName) && seen.Add(player.CarName))
                required.Add(new PreparedContent { Id = player.CarName, Kind = (byte)AssetKind.Car });
        }

        return required.ToArray();
    }

    /// <summary>
    /// Checks a whole roster before a race is constructed. Returns the reason it was refused, or
    /// null when every slot is usable.
    /// </summary>
    public static string? ValidateMatch(MatchGameplayInfo info)
    {
        if (!IsAcceptedStage(info.StageName))
            return $"Stage '{info.StageName}' is not available for online play.";

        if (!GamemodeIds.IsInstalled(info.Gamemode))
            return $"Gamemode '{info.Gamemode}' is not installed.";

        foreach (var player in info.Players.Values)
        {
            if (!IsAcceptedCar(player.CarName))
                return $"Car '{player.CarName}' is not available for online play.";
        }

        return null;
    }

    /// <summary>
    /// Rejects anything that is not a plain relative id: a URL, a drive path, a traversal, a
    /// control character or a trailing extension can never name shipped content.
    /// </summary>
    private static bool IsSafeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
            return false;

        if (id.Any(char.IsControl) || id.Contains('\\') || id.Contains(':') || id.Contains('\0'))
            return false;

        if (id.StartsWith('/') || id.StartsWith("./") || id.Contains("//"))
            return false;

        if (id.EndsWith(".rad", StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".radpack", StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || id.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;

        return id.Split('/').All(part => part.Length > 0 && part is not "." and not "..");
    }

    private static string Normalize(string id) => id.Replace('\\', '/').Trim();
}

/// <summary>The gamemodes this build can run, for both the lobby and the game master.</summary>
public static class GamemodeIds
{
    private static readonly string[] Installed = [DefaultGamemodes.PvP, DefaultGamemodes.TimeTrial];

    public static bool IsInstalled(string? gamemode)
        => gamemode is not null && Installed.Contains(gamemode, StringComparer.Ordinal);
}
