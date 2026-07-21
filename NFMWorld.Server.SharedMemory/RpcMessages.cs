using MemoryPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorld.Server.SharedMemory;

/// <summary>
/// Controller → Worker: batched player inputs for one simulation tick.
/// </summary>
[MemoryPackable]
public partial struct PlayerInputBatch
{
    /// <summary>Server tick number for this batch.</summary>
    [MemoryPackOrder(0)]
    public uint TickNumber { get; set; }

    /// <summary>Server time when this batch was collected.</summary>
    [MemoryPackOrder(1)]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>
    /// Player inputs, keyed by player index (matching MatchGameplayInfo.Players keys).
    /// </summary>
    [MemoryPackOrder(2)]
    public Dictionary<byte, PlayerState> PlayerStates { get; set; }
}

/// <summary>
/// Worker → Controller: game state snapshot after processing one tick of inputs.
/// </summary>
[MemoryPackable]
public partial struct GameStateSnapshot
{
    /// <summary>Echoes the tick number from the input batch that produced this state.</summary>
    [MemoryPackOrder(0)]
    public uint TickNumber { get; set; }

    /// <summary>Server time for this snapshot.</summary>
    [MemoryPackOrder(1)]
    public DateTimeOffset ServerTime { get; set; }

    /// <summary>
    /// Per-player state snapshots, keyed by player index.
    /// </summary>
    [MemoryPackOrder(2)]
    public Dictionary<byte, PlayerState> PlayerStates { get; set; }

    /// <summary>Whether the race has finished.</summary>
    [MemoryPackOrder(3)]
    public bool IsRaceFinished { get; set; }
}
