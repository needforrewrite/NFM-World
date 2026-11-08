using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MemoryPackable]
[PacketClientToServer(6)]
public partial struct C2S_PlayerIdentity : IPacketClientToServer<C2S_PlayerIdentity>
{
    [MemoryPackOrder(0)] public required string PlayerName { get; set; }
    [MemoryPackOrder(1)] public required string SelectedVehicle { get; set; }
    [MemoryPackOrder(2)] public required Color3 Color { get; set; }
}
