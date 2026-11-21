using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// One in-progress race snapshot: the order and progress a server gamemode is willing to publish
/// while cars are still running. The server orders it; clients render that order.
/// </summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct RaceLiveState
{
    [TightPackOrder(0)] public required RaceLiveStanding[] Standings { get; init; }
}

/// <summary>One car's live progress, in the order the gamemode reported it.</summary>
[TightPackable, PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly partial struct RaceLiveStanding
{
    [TightPackOrder(0)] public required Guid PlayerId { get; init; }
    [TightPackOrder(1)] public required string PlayerName { get; init; }
    [TightPackOrder(2)] public required string CarName { get; init; }
    [TightPackOrder(3)] public Color3 Color { get; init; }
    [TightPackOrder(4)] public bool IsBot { get; init; }
    [TightPackOrder(5)] public required int Position { get; init; }
    [TightPackOrder(6)] public required byte Lap { get; init; }
    [TightPackOrder(7)] public required ushort Checkpoint { get; init; }
    [TightPackOrder(8)] public bool Wasted { get; init; }
    [TightPackOrder(9)] public bool Forfeited { get; init; }
}
