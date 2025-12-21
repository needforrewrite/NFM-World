using System.Buffers;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    public required string Message { get; set; } = string.Empty;

    public C2S_LobbyChatMessage()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.WriteString(Message);
    }

    public static C2S_LobbyChatMessage Read(SpanReader data)
    {
        return new C2S_LobbyChatMessage
        {
            Message = data.ReadString()
        };
    }
}