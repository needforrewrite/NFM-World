using MessagePack;

namespace NFMWorldLibrary.Multiplayer.packets.s2c;

[MessagePackObject]
public struct S2C_RaceCanStart : IPacketServerToClient<S2C_RaceCanStart>;