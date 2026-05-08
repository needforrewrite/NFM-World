using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_PlayerIdentity : IPacketClientToServer<C2S_PlayerIdentity>
{
    [Key(0)] public required string PlayerName { get; set; }
    [Key(1)] public required string SelectedVehicle { get; set; }
    [Key(2)] public required Color3 Color { get; set; }
}