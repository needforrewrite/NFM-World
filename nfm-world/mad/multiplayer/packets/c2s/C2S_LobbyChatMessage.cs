using System.Buffers;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    [Key(0)] public required string Message { get; set; } = string.Empty;

    public C2S_LobbyChatMessage()
    {
    }
}