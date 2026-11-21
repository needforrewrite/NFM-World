using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-22)]
public partial struct S2C_GameLobbyNotice : IPacketServerToClient<S2C_GameLobbyNotice>
{
    [TightPackOrder(0)] public required string Message { get; set; }
}
