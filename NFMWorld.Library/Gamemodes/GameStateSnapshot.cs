using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Snapshot of authoritative game state broadcast from server to clients.
/// </summary>
[MemoryPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct GameStateSnapshot
{
    /// <summary>Whether the race has ended.</summary>
    public bool IsFinished { get; init; }

    /// <summary>Final race results, populated when IsFinished is true.</summary>
    public RaceResults? Results { get; init; }
}
