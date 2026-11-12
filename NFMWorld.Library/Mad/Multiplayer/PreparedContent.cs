using TightPack;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// One canonical archive asset a race has to hold before it can load. <see cref="Kind"/> is an
/// <c>AssetKind</c>; the lobby sends the same list to every participant and to the game master.
/// </summary>
[TightPackable]
public partial struct PreparedContent
{
    [TightPackOrder(0)] public required string Id { get; set; }
    [TightPackOrder(1)] public required byte Kind { get; set; }
}
