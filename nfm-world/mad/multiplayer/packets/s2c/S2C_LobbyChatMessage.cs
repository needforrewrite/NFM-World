using System.Buffers;
using CommunityToolkit.HighPerformance;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct S2C_LobbyChatMessage : IPacketServerToClient<S2C_LobbyChatMessage>
{
    [Key(0)] public required string Sender { get; set; } = string.Empty;
    [Key(1)] public required uint SenderClientId { get; set; } = 0;
    [Key(2)] public required string Message { get; set; } = string.Empty;

    public S2C_LobbyChatMessage()
    {
    }
}