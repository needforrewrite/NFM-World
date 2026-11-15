using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(15)]
public partial struct C2S_RaceChat : IPacketClientToServer<C2S_RaceChat>
{
    [TightPackOrder(0)] public required string Message { get; set; }
}
