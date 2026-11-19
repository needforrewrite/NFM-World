using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Shared pieces of the standard race completion every competitive gamemode uses: the start
/// countdown, the plausibility check on a client's checkpoint claim, and the canonical
/// standings order. A gamemode keeps its own state machine and event types; these are the
/// rules it calls into so two gamemodes cannot disagree about what "finished" means.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public static class StandardRace
{
    /// <summary>
    /// Ticks one countdown step. <paramref name="inner"/> counts down from
    /// <paramref name="interval"/>; when it reaches zero the displayed
    /// <paramref name="countdown"/> drops by one and <paramref name="inner"/> resets.
    /// Returns true on the step that reaches zero.
    /// </summary>
    [PluginVisible]
    public static bool TickCountdown(ref int inner, ref int countdown, int interval)
    {
        if (--inner > 0)
            return false;

        inner = interval;
        countdown--;
        return countdown <= 0;
    }

    /// <summary>
    /// True when a client's claimed progress advances the race by exactly one checkpoint and
    /// the player's last relayed position is actually near the checkpoint they claim to have
    /// crossed. The client's numbers are only a hint; without this a client could declare
    /// itself the winner.
    /// </summary>
    [PluginVisible]
    public static bool IsPlausibleCheckpoint(
        ushort claimedCheckpoint,
        byte claimedLap,
        ushort currentCheckpoint,
        byte currentLap,
        int checkpointCount,
        ushort totalLaps,
        f64Vector3? relayedPosition,
        IReadOnlyList<StageObject> checkpoints
    )
    {
        if (checkpointCount == 0)
            return false;

        var currentTotal = currentCheckpoint + currentLap * checkpointCount;
        var claimedTotal = claimedCheckpoint + claimedLap * checkpointCount;

        if (claimedTotal != currentTotal + 1)
            return false;

        if (claimedCheckpoint >= checkpointCount || claimedLap > totalLaps)
            return false;

        // Without a relayed position yet there is nothing to corroborate the claim against.
        if (relayedPosition is not { } position)
            return false;

        // The client reports the checkpoint it is now heading for, so the crossing that sent
        // this event was the one before it - wrapping back to the last checkpoint on a lap.
        var crossed = claimedCheckpoint == 0 ? checkpointCount - 1 : claimedCheckpoint - 1;
        var checkpoint = checkpoints[crossed].Position;

        // A generous radius: this only has to reject a player who is nowhere near the
        // checkpoint they claim, not to police the exact line.
        const float radiusSq = 5000f * 5000f;
        var dx = (float)position.X - (float)checkpoint.X;
        var dz = (float)position.Z - (float)checkpoint.Z;

        return dx * dx + dz * dz <= radiusSq;
    }

    /// <summary>
    /// The canonical standing order: the winner first, then running cars by position, then DNFs
    /// newest death first. A revived car is alive again, so it sorts with the running.
    /// </summary>
    [PluginVisible]
    public static RaceStanding[] BuildStandings(
        IReadOnlyList<ServerSidePlayerInfo> players,
        IReadOnlyList<RaceProgress> progress,
        Guid? winnerId
    )
    {
        return
        [
            .. progress
            .Select((p, index) => (Progress: p, Slot: index))
            .OrderBy(x => winnerId is { } w && x.Progress.PlayerId == w ? -1 : Group(x.Progress))
            .ThenBy(x => Within(x.Progress))
            .Select((x, index) => new RaceStanding
                        {
                            PlayerId = x.Progress.PlayerId,
                            FinishPosition = index,
                            FinishTime = x.Progress.Forfeited || x.Progress.WastedAt >= 0
                                ? null
                                : TimeSpan.Zero
                        }
                    )
        ];
    }

    private static int Group(RaceProgress progress) => progress.Forfeited || progress.WastedAt >= 0 ? 1 : 0;

    private static int Within(RaceProgress progress)
        => progress.Forfeited || progress.WastedAt >= 0
            ? -Math.Max(progress.WastedAt, 0)
            : progress.Position;
}

/// <summary>One player's server-side progress in a standard race.</summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public sealed class RaceProgress
{
    [PluginVisible] public required Guid PlayerId { get; init; }
    [PluginVisible] public byte Lap { get; set; }
    [PluginVisible] public ushort Checkpoint { get; set; }
    [PluginVisible] public int Position { get; set; }
    [PluginVisible] public bool Forfeited { get; set; }

    /// <summary>Server tick the car was last seen wasted, or -1 while it is alive.</summary>
    [PluginVisible]
    public int WastedAt { get; set; } = -1;

    /// <summary>
    /// Records the player's wasted state for this tick. Returns true when a previously wasted
    /// car is alive again (revived through a fix hoop).
    /// </summary>
    [PluginVisible]
    public bool TrackWasted(bool? wasted, int serverTick)
    {
        if (Forfeited)
            return false;

        switch (wasted)
        {
            case true when WastedAt < 0:
                WastedAt = serverTick;
                return false;
            case false when WastedAt >= 0:
                WastedAt = -1;
                return true;
            default:
                return false;
        }
    }

    [PluginVisible] public bool IsAlive => !Forfeited && WastedAt < 0;
}
