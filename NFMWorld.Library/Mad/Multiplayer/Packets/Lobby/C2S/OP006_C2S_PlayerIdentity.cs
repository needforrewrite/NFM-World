using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(6)]
public partial struct C2S_PlayerIdentity : IPacketClientToServer<C2S_PlayerIdentity>
{
    [TightPackOrder(0)] public required string PlayerName { get; set; }
    [TightPackOrder(1)] public required string SelectedVehicle { get; set; }
    [TightPackOrder(2)] public required Color3 Color { get; set; }
}
