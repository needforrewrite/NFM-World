using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    public required PlayerState State;

    public C2S_PlayerState()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte> => writer.Write(this);

    public static C2S_PlayerState Read(SpanReader data) => data.ReadMemory<C2S_PlayerState>();
}