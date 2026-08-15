using MemoryPack;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Results of a completed race. Returned by <see cref="IGamemode.GetResults"/>
/// and delivered by the race host on finish.
/// </summary>
[MemoryPackable]
public readonly partial struct RaceResults
{
    /// <summary>Players ordered by finish position (0 = first).</summary>
    [MemoryPackOrder(0)]
    public required RaceStanding[] Standings { get; init; }

    /// <summary>Wall-clock duration of the race.</summary>
    [MemoryPackOrder(1)]
    public required TimeSpan RaceDuration { get; init; }

    /// <summary>Gamemode identifier (e.g., "nfmm/racing").</summary>
    [MemoryPackOrder(2)]
    public required string GamemodeId { get; init; }
}

/// <summary>
/// Standing for a single player in race results.
/// </summary>
[MemoryPackable]
public readonly partial struct RaceStanding
{
    /// <summary>Application-level player ID.</summary>
    [MemoryPackOrder(0)]
    public required Guid PlayerId { get; init; }

    /// <summary>Zero-based finish position. 0 = first place.</summary>
    [MemoryPackOrder(1)]
    public required int FinishPosition { get; init; }

    /// <summary>Total race time, or null if the player did not finish.</summary>
    [MemoryPackOrder(2)]
    public TimeSpan? FinishTime { get; init; }

    /// <summary>True if this standing belongs to the local client player.</summary>
    [MemoryPackOrder(3)]
    public bool IsClientPlayer { get; init; }
}