using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-4)]
public partial struct S2C_RaceCanStart : IPacketServerToClient<S2C_RaceCanStart>;
