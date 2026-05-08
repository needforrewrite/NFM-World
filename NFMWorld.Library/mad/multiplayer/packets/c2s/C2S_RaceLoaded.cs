using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MessagePackObject]
public struct C2S_RaceLoaded : IPacketClientToServer<C2S_RaceLoaded>;