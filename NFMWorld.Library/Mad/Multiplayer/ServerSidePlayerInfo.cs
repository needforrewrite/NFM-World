using System.Runtime.InteropServices;
using MemoryPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Information about a player on the lobby or in-game.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[MemoryPackable]
[PluginVisible]
public partial struct ServerSidePlayerInfo
{
    /// <summary>
    /// Unique player ID. 
    /// </summary>
    [MemoryPackOrder(0), PluginVisible] public required Guid Id { get; set; }
    
    /// <summary>
    /// Player username. For instance, in chat.
    /// </summary>
    [MemoryPackOrder(1), PluginVisible] public required string PlayerName { get; set; }
    
    /// <summary>
    /// The vehicle the player has selected.
    /// </summary>
    [MemoryPackOrder(2), PluginVisible] public required string CarName { get; set; }
    
    /// <summary>
    /// The color of the player's vehicle.
    /// </summary>
    [MemoryPackOrder(3), PluginVisible] public required Color3 Color { get; set; }
}