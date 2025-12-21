using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct S2C_ChatMessage : IPacketServerToClient<S2C_ChatMessage>
{
    public static OpcodesServerToClient Opcode => OpcodesServerToClient.ChatMessage;
    public required string Sender { get; set; } = string.Empty;
    public required int SenderClientId { get; set; } = 0;
    public required string Message { get; set; } = string.Empty;

    public S2C_ChatMessage()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.WriteString(Sender);
        writer.Write(SenderClientId);
        writer.WriteString(Message);
    }

    public static S2C_ChatMessage Read(SpanReader data)
    {
        return new S2C_ChatMessage
        {
            Sender = data.ReadString(),
            SenderClientId = data.ReadInt32(),
            Message = data.ReadString()
        };
    }
}