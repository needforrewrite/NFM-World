using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct C2S_LobbyStartRace : IPacketClientToServer<C2S_LobbyStartRace>
{
    public required int SessionId { get; set; }
    
    public C2S_LobbyStartRace()
    {
    }

    public void Write<T>(T writer) where T : IBufferWriter<byte> => writer.Write(this);

    public static C2S_LobbyStartRace Read(SpanReader data) => data.ReadMemory<C2S_LobbyStartRace>();
}