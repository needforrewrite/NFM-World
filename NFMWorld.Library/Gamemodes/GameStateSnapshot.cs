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

    /// <summary>
    /// Gamemode-specific key-value state for HUD rendering.
    /// Example keys: "payloadProgress", "hillController", "flagState".
    /// </summary>
    public Dictionary<string, object>? State { get; init; }
}
