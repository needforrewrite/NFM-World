using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MessagePackObject]
public struct C2S_LeaveSession : IPacketClientToServer<C2S_LeaveSession>
{
    [Key(0)] public required uint SessionId { get; set; }
}