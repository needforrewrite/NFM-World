using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(4)]
public partial struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    [TightPackOrder(0)] public required string Message { get; set; } = string.Empty;

    public C2S_LobbyChatMessage()
    {
    }
}
