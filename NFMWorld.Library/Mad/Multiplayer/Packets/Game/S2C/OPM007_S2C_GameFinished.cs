using MemoryPack;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Game Master → Client: the race has ended. Contains final results.
/// </summary>
[MemoryPackable]
[PacketServerToClient(-7)]
public readonly partial struct S2C_GameFinished : IPacketServerToClient<S2C_GameFinished>
{
    /// <summary>Full race results with standings, times, and gamemode info (v2).</summary>
    [MemoryPackOrder(0)] public required RaceResults Results { get; init; }
}
