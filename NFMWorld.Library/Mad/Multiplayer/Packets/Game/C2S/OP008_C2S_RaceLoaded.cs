using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(8)]
public partial struct C2S_RaceLoaded : IPacketClientToServer<C2S_RaceLoaded>
{
    /// <summary>
    /// Unique 128-bit single-use token to send to the race server to join the race.
    /// </summary>
    [MemoryPackOrder(0)]
    public required Guid JoinToken { get; set; }
}
