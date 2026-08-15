using MemoryPack;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Snapshot of authoritative game state broadcast from server to clients.
/// </summary>
[MemoryPackable]
public readonly partial struct GameStateSnapshot
{
    /// <summary>Whether the race has ended.</summary>
    public bool IsFinished { get; init; }

    /// <summary>Final race results, populated when IsFinished is true.</summary>
    public RaceResults? Results { get; init; }
}
