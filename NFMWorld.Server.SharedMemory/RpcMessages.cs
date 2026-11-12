using TightPack;
using NFMWorldLibrary;

namespace NFMWorld.Server.SharedMemory;

/// <summary>
/// Controller → Worker: batched player inputs for one simulation tick.
/// </summary>
[TightPackable]
public partial struct PlayerInputBatch
{
    /// <summary>Server tick number for this batch.</summary>
    [TightPackOrder(0)]
    public uint TickNumber { get; set; }

    /// <summary>Server time when this batch was collected.</summary>
    [TightPackOrder(1)]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>
    /// Player inputs, keyed by player index (matching MatchGameplayInfo.Players keys).
    /// </summary>
    [TightPackOrder(2)]
    public Dictionary<byte, PlayerState> PlayerStates { get; set; }
}

/// <summary>
/// Worker → Controller: game state snapshot after processing one tick of inputs.
/// </summary>
[TightPackable]
public partial struct GameStateSnapshot
{
    /// <summary>Echoes the tick number from the input batch that produced this state.</summary>
    [TightPackOrder(0)]
    public uint TickNumber { get; set; }

    /// <summary>Server time for this snapshot.</summary>
    [TightPackOrder(1)]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>
    /// Per-player state snapshots, keyed by player index.
    /// </summary>
    [TightPackOrder(2)]
    public Dictionary<byte, PlayerState> PlayerStates { get; set; }

    /// <summary>Whether the race has finished.</summary>
    [TightPackOrder(3)]
    public bool IsRaceFinished { get; set; }
}
