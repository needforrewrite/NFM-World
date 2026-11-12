using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(2)]
public partial struct C2S_JoinSession : IPacketClientToServer<C2S_JoinSession>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
}
