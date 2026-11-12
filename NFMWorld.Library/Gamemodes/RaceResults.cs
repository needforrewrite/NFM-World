using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Results of a completed race. Returned by <see cref="IServerGamemode.GetStateSnapshot"/>
/// and delivered by the race host on finish.
/// </summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct RaceResults
{
    /// <summary>Players ordered by finish position (0 = first).</summary>
    [TightPackOrder(0)]
    public required RaceStanding[] Standings { get; init; }

    /// <summary>Wall-clock duration of the race.</summary>
    [TightPackOrder(1)]
    public required TimeSpan RaceDuration { get; init; }

    /// <summary>Gamemode identifier (e.g., "nfmm/racing").</summary>
    [TightPackOrder(2)]
    public required string GamemodeId { get; init; }
}

/// <summary>
/// Standing for a single player in race results.
/// </summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct RaceStanding
{
    /// <summary>Application-level player ID.</summary>
    [TightPackOrder(0)]
    public required Guid PlayerId { get; init; }

    /// <summary>Zero-based finish position. 0 = first place.</summary>
    [TightPackOrder(1)]
    public required int FinishPosition { get; init; }

    /// <summary>Total race time, or null if the player did not finish.</summary>
    [TightPackOrder(2)]
    public TimeSpan? FinishTime { get; init; }
}
