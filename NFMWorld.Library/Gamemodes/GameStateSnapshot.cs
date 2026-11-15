using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Snapshot of authoritative game state broadcast from server to clients.
/// </summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct GameStateSnapshot
{
    /// <summary>Whether the race has ended.</summary>
    public bool IsFinished { get; init; }

    /// <summary>The result can still be withdrawn before a player accepts it.</summary>
    public bool IsProvisional { get; init; }

    /// <summary>Final race results, populated when IsFinished is true.</summary>
    public RaceResults? Results { get; init; }
}
