using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct S2C_LobbyChatMessage : IPacketServerToClient<S2C_LobbyChatMessage>
{
    public required string Sender { get; set; } = string.Empty;
    public required uint SenderClientId { get; set; } = 0;
    public required string Message { get; set; } = string.Empty;

    public S2C_LobbyChatMessage()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.WriteString(Sender);
        writer.Write(SenderClientId);
        writer.WriteString(Message);
    }

    public static S2C_LobbyChatMessage Read(SpanReader data)
    {
        return new S2C_LobbyChatMessage
        {
            Sender = data.ReadString(),
            SenderClientId = data.ReadUInt32(),
            Message = data.ReadString()
        };
    }
}