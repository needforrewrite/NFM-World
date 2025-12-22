using MessagePack;

namespace NFMWorld.Mad;

[MessagePackObject]
public struct S2C_RaceStarted : IPacketServerToClient<S2C_RaceStarted>
{
    [Key(0)] public required S2C_LobbyState.GameSession Session { get; set; }
}