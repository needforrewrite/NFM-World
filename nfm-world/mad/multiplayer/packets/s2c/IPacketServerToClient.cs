using System.Buffers;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketServerToClient<TSelf> where TSelf : IPacketServerToClient<TSelf>
{
    public static abstract OpcodesServerToClient Opcode { get; }

    void Write<T>(T writer) where T : IBufferWriter<byte>;
    public static abstract TSelf Read(SpanReader data);
}