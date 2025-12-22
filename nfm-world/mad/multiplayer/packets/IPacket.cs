using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using NFMWorld.Util;

namespace NFMWorld.Mad.packets;

public interface IPacket;

public interface IReadableWritable<out TSelf>
{
    private static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithResolver(CompositeResolver.Create(StandardResolver.Instance, MsgPackResolver.Instance));

    void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        MessagePackSerializer.Serialize<TSelf>(writer, (TSelf)this, Options);
    }

    public static virtual TSelf Read(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<TSelf>(data);
    }
}