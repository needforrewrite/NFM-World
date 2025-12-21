using System.Buffers;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketServerToClient : IPacket;

public interface IPacketServerToClient<TSelf> : IPacketServerToClient where TSelf : IPacketServerToClient<TSelf>
{
    void Write<T>(T writer) where T : IBufferWriter<byte>;
    public static abstract TSelf Read(SpanReader data);
}