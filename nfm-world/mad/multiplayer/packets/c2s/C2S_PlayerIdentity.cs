using System.Buffers;
using CommunityToolkit.HighPerformance;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public struct C2S_PlayerIdentity : IPacketClientToServer<C2S_PlayerIdentity>
{
    public required string PlayerName { get; set; }
    public required string SelectedVehicle { get; set; }
    public required Color3 Color { get; set; }

    public void Write<T>(T writer) where T : IBufferWriter<byte>
    {
        writer.WriteString(PlayerName);
        writer.WriteString(SelectedVehicle);
        writer.Write(Color);
    }

    public static C2S_PlayerIdentity Read(SpanReader data)
    {
        return new C2S_PlayerIdentity
        {
            PlayerName = data.ReadString(),
            SelectedVehicle = data.ReadString(),
            Color = data.ReadMemory<Color3>()
        };
    }
}