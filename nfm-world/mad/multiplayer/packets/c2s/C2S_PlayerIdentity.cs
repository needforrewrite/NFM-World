using System.Buffers;
using CommunityToolkit.HighPerformance;
using MessagePack;
using NFMWorld.Util;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct C2S_PlayerIdentity : IPacketClientToServer<C2S_PlayerIdentity>
{
    [Key(0)] public required string PlayerName { get; set; }
    [Key(1)] public required string SelectedVehicle { get; set; }
    [Key(2)] public required Color3 Color { get; set; }
}