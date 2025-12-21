using System.Buffers;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketClientToServer : IPacket;

public interface IPacketClientToServer<TSelf> : IPacketClientToServer where TSelf : IPacketClientToServer<TSelf>
{
    void Write<T>(T writer) where T : IBufferWriter<byte>;
    public static abstract TSelf Read(SpanReader data);
}