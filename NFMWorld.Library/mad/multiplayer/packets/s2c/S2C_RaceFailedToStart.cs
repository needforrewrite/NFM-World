using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.s2c;

[MessagePackObject]
public struct S2C_RaceFailedToStart : IPacketServerToClient<S2C_RaceFailedToStart>;