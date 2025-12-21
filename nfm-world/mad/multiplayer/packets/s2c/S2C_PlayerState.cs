using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct S2C_PlayerState : IPacketServerToClient<S2C_PlayerState>
{
    public required int PlayerClientId { get; set; } = 0;
    public required PlayerState State;

    public S2C_PlayerState()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte> => writer.Write(this);

    public static S2C_PlayerState Read(SpanReader data) => data.ReadMemory<S2C_PlayerState>();
}