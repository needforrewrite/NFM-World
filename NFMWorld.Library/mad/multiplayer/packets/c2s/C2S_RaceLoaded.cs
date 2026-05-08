using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_RaceLoaded : IPacketClientToServer<C2S_RaceLoaded>;