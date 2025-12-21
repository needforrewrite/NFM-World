using System.Buffers;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct C2S_ChatMessage : IPacketClientToServer<C2S_ChatMessage>
{
    public static OpcodesClientToServer Opcode => OpcodesClientToServer.ChatMessage;
    public required string Message { get; set; } = string.Empty;

    public C2S_ChatMessage()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.WriteString(Message);
    }

    public static C2S_ChatMessage Read(SpanReader data)
    {
        return new C2S_ChatMessage
        {
            Message = data.ReadString()
        };
    }
}