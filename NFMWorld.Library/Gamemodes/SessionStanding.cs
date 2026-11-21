using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// A final placing that carries everything the match lobby needs. The session roster can lose a
/// player after the race, so the row must not depend on looking the id up later.
/// </summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct SessionStanding
{
    [TightPackOrder(0)] public required Guid PlayerId { get; init; }
    [TightPackOrder(1)] public required string PlayerName { get; init; }
    [TightPackOrder(2)] public required string CarName { get; init; }
    [TightPackOrder(3)] public Color3 Color { get; init; }
    [TightPackOrder(4)] public bool IsBot { get; init; }
    [TightPackOrder(5)] public required int Position { get; init; }
    [TightPackOrder(6)] public bool Dnf { get; init; }
    [TightPackOrder(7)] public byte Slot { get; init; }
    [TightPackOrder(8)] public Color3?[]? CarColors { get; init; }
    [TightPackOrder(9)] public byte Lap { get; init; }
    [TightPackOrder(10)] public ushort TotalLaps { get; init; }
    [TightPackOrder(11)] public bool Wasted { get; init; }
    [TightPackOrder(12)] public byte? PowerPercent { get; init; }
    [TightPackOrder(13)] public byte? DamagePercent { get; init; }
}
