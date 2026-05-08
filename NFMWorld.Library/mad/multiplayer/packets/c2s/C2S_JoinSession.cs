using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_JoinSession : IPacketClientToServer<C2S_JoinSession>
{
    [Key(0)] public required uint SessionId { get; set; }
}