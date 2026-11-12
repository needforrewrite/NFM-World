using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(5)]
public partial struct C2S_LobbyStartRace : IPacketClientToServer<C2S_LobbyStartRace>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }

    public C2S_LobbyStartRace()
    {
    }
}
