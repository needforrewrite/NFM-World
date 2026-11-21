using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(17)]
public partial struct C2S_SetGameRule : IPacketClientToServer<C2S_SetGameRule>
{
    [TightPackOrder(0)] public required uint SessionId { get; set; }
    [TightPackOrder(1)] public required string Name { get; set; }
    [TightPackOrder(2)] public required string Value { get; set; }
}
