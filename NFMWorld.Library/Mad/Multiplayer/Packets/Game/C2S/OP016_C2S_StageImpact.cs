using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(16)]
public partial struct C2S_StageImpact : IPacketClientToServer<C2S_StageImpact>
{
    [TightPackOrder(0)] public Guid PlayerId;
    [TightPackOrder(1)] public StageImpact Impact;
}
