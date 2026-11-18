using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-15)]
public partial struct S2C_StagePieceState : IPacketServerToClient<S2C_StagePieceState>
{
    [TightPackOrder(0)] public StagePieceState State;
    [TightPackOrder(1)] public bool Snapshot;
}
