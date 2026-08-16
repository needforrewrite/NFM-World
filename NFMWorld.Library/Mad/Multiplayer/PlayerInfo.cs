using System.Runtime.InteropServices;
using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Information about a player on the lobby or in-game.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[MemoryPackable]
[LuaVisible]
public partial struct PlayerInfo
{
    /// <summary>
    /// Unique player ID. 
    /// </summary>
    [MemoryPackOrder(0)] public required Guid Id { get; set; }

    [MemoryPackIgnore, LuaName("id")]
    public string LuaId
    {
        get => Id.ToString("D");
        set => Guid.Parse(value);
    }
    
    /// <summary>
    /// Player username. For instance, in chat.
    /// </summary>
    [MemoryPackOrder(1), LuaName] public required string Name { get; set; }
    
    /// <summary>
    /// The vehicle the player has selected.
    /// </summary>
    [MemoryPackOrder(2), LuaName] public required string Vehicle { get; set; }
    
    /// <summary>
    /// The color of the player's vehicle.
    /// </summary>
    [MemoryPackOrder(3), LuaName] public required Color3 Color { get; set; }
}