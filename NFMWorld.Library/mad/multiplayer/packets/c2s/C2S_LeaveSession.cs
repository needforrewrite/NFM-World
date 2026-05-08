using MessagePack;

namespace NFMWorldLibrary.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_LeaveSession : IPacketClientToServer<C2S_LeaveSession>
{
    [Key(0)] public required uint SessionId { get; set; }
}