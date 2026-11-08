using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(5)]
public partial struct C2S_LobbyStartRace : IPacketClientToServer<C2S_LobbyStartRace>
{
    [MemoryPackOrder(0)] public required uint SessionId { get; set; }

    public C2S_LobbyStartRace()
    {
    }
}
