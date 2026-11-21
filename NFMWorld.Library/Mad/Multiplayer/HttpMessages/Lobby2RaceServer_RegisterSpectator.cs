using TightPack;

namespace NFMWorldLibrary.Multiplayer.HttpMessages;

[TightPackable]
public partial struct Lobby2RaceServer_RegisterSpectator
{
    [TightPackOrder(0)] public Guid SessionToken { get; set; }
    [TightPackOrder(1)] public Guid PlayerId { get; set; }
    [TightPackOrder(2)] public required string Name { get; set; }
    [TightPackOrder(3)] public Color3 Color { get; set; }
    [TightPackOrder(4)] public bool Muted { get; set; }
}

[TightPackable]
public partial struct Lobby2RaceServer_RegisterSpectatorResponse
{
    [TightPackOrder(0)] public Guid JoinToken { get; set; }
}
