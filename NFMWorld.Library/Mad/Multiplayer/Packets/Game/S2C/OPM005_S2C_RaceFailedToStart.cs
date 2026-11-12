using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-5)]
public partial struct S2C_RaceFailedToStart : IPacketServerToClient<S2C_RaceFailedToStart>;
