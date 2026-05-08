using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MessagePackObject]
public struct S2C_RaceCanStart : IPacketServerToClient<S2C_RaceCanStart>;