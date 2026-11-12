using System.Runtime.InteropServices;
using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Information about a player on the lobby or in-game.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[TightPackable]
[PluginVisible]
public partial struct ServerSidePlayerInfo
{
    /// <summary>
    /// Unique player ID. 
    /// </summary>
    [TightPackOrder(0), PluginVisible]
    public required Guid Id { get; set; }

    /// <summary>
    /// Player username. For instance, in chat.
    /// </summary>
    [TightPackOrder(1), PluginVisible]
    public required string PlayerName { get; set; }

    /// <summary>
    /// The vehicle the player has selected.
    /// </summary>
    [TightPackOrder(2), PluginVisible]
    public required string CarName { get; set; }

    /// <summary>
    /// The color of the player's vehicle.
    /// </summary>
    [TightPackOrder(3), PluginVisible]
    public required Color3 Color { get; set; }

    /// <summary>
    /// Archive account id, or 0 for a guest. The lobby fills this in from the authenticated
    /// token so a result can be attributed to an account without trusting the client's name.
    /// </summary>
    [TightPackOrder(4), PluginVisible]
    public int AccountId { get; set; }

    /// <summary>
    /// True when this slot was filled by a server-side bot rather than a connected client.
    /// </summary>
    [TightPackOrder(5), PluginVisible]
    public bool IsBot { get; set; }

    /// <summary>
    /// For a bot, the player whose client simulates it - that client relays the bot's state.
    /// Empty for a human. A client may only report state for a bot it owns.
    /// </summary>
    [TightPackOrder(6), PluginVisible]
    public Guid OwnerId { get; set; }
}
