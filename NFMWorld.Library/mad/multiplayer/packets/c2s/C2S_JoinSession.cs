using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MessagePackObject]
public struct C2S_JoinSession : IPacketClientToServer<C2S_JoinSession>
{
    [Key(0)] public required uint SessionId { get; set; }
}