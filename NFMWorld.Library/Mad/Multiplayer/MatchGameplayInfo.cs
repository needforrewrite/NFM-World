using System.Runtime.InteropServices;
using TightPack;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Required gameplay info to construct a match on the client or server.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[TightPackable]
public partial struct MatchGameplayInfo()
{
    [TightPackOrder(0)] public required string StageName { get; set; }

    /// <summary>
    /// Key: player car index
    /// Value: client ID
    /// </summary>
    [TightPackOrder(1)]
    public required IDictionary<byte, ServerSidePlayerInfo> Players { get; set; }

    [TightPackOrder(2)] public required string Gamemode { get; set; } = DefaultGamemodes.PvP;

    /// <summary>
    /// Gamemode config. Strings, not objects: the wire format has no formatter for <c>object</c>,
    /// and resolving one fails even for an empty dictionary. Every declared gamemode config
    /// property is a string; the plugin boundary converts to <c>object</c>.
    /// </summary>
    [TightPackOrder(3)]
    public Dictionary<string, string> Parameters { get; set; } = [];

    /// <summary>
    /// The players in array order. The client gamemode addresses players by array position
    /// while relayed state is addressed by id, so the array has to be the slots in key order -
    /// a dictionary enumerates in no particular order, and building it as-is put each player
    /// in another player's slot.
    /// </summary>
    public readonly ServerSidePlayerInfo[] InSlotOrder()
        => [.. Players.OrderBy(kv => kv.Key).Select(kv => kv.Value)];

    /// <summary>
    /// The largest field a session may have, bots included. Both ends clamp to it: the client
    /// stops its sliders here and the lobby clamps whatever a client sends.
    /// </summary>
    public const int MaxPlayers = 16;
}

public static class DefaultGamemodes
{
    public const string PvP = "nfmm/pvp";
    public const string TimeTrial = "nfmm/timetrial";
}
