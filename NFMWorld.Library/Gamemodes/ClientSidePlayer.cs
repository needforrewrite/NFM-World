using Lua;
using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.AI;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// A player belonging to the client gamemode.
/// </summary>
/// <param name="info">The player parameters.</param>
/// <param name="index">The zero-based index of the player.</param>
/// <param name="isFake">True if the player was created by the gamemode, such as the ball in football.</param>
[LuaVisible]
public partial class ClientSidePlayer(ClientSidePlayerInfo info, int index, bool isFake = false)
{
    /// <summary>
    /// The player info.
    /// </summary>
    [LuaName] public ClientSidePlayerInfo Info { get; } = info;
    
    /// <summary>
    /// The zero-based index of the player.
    /// </summary>
    [LuaName] public int Index { get; } = index;

    /// <summary>
    /// Raised when <see cref="Car"/> is assigned a different car.
    /// The client stage uses this to create/swap the player's visual.
    /// </summary>
    public event Action<ClientSidePlayer, BackendCar?>? CarChanged;

    /// <summary>
    /// A reference to the car for the player if the player has a car.
    /// </summary>
    [LuaName] public BackendCar? Car
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;

            field = value;
            CarChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// If this player is a bot, the bot object.
    /// </summary>
    [LuaName] public BaseAi? Bot { get; set; }
    
    /// <summary>
    /// True if the player was created by the gamemode.
    /// </summary>
    [LuaName] public bool IsFake { get; } = isFake;
}