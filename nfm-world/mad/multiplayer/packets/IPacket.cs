using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Xna.Framework;
using SoftFloat;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad.packets;

public interface IPacket;

file static class MsgPackHelpers
{
    public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithResolver(CompositeResolver.Create([
            new UnsafeUnmanagedStructFormatter<PlayerState>(100),
            new UnsafeUnmanagedStructFormatter<Vector2>(101),
            new UnsafeUnmanagedStructFormatter<Vector3>(102),
            new UnsafeUnmanagedStructFormatter<Vector4>(103),
            new UnsafeUnmanagedStructFormatter<Quaternion>(104),
            new UnsafeUnmanagedStructFormatter<Matrix>(105),
            new UnsafeUnmanagedStructFormatter<Color>(106),
            new UnsafeUnmanagedStructFormatter<Color3>(107),
            new UnsafeUnmanagedStructFormatter<AngleSingle>(108),
            new UnsafeUnmanagedStructFormatter<fix64>(109),
            new UnsafeUnmanagedStructFormatter<f64Vector3>(110),
        ], [
            StandardResolver.Instance,
            MsgPackResolver.Instance
        ]));
}

public interface IReadableWritable<out TSelf>
{
    void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        MessagePackSerializer.Serialize<TSelf>(writer, (TSelf)this, MsgPackHelpers.Options);
    }

    public static virtual TSelf Read(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<TSelf>(data);
    }
}