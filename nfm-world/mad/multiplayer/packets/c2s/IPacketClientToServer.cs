using System.Buffers;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketClientToServer<TSelf> where TSelf : IPacketClientToServer<TSelf>
{
    public static abstract OpcodesClientToServer Opcode { get; }
    
    void Write<T>(T writer) where T : IBufferWriter<byte>;
    public static abstract TSelf Read(SpanReader data);
}