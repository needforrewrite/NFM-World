using nfm_world_library.Lua;
using NFMWorldLibrary.Backend.AI;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// A player belonging to the client gamemode.
/// </summary>
/// <param name="parameters">The player parameters.</param>
/// <param name="index">The zero-based index of the player.</param>
/// <param name="isFake">True if the player was created by the gamemode, such as the ball in football.</param>
[LuaVisible]
public partial class ClientSidePlayer(ClientSidePlayerParameters parameters, int index, bool isFake = false)
{
    private IInGameCar? _car;

    /// <summary>
    /// The player parameters.
    /// </summary>
    public ClientSidePlayerParameters Parameters { get; } = parameters;
    
    /// <summary>
    /// The zero-based index of the player.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Raised when <see cref="Car"/> is assigned a different car.
    /// The client stage uses this to create/swap the player's visual.
    /// </summary>
    public event Action<ClientSidePlayer, IInGameCar?>? CarChanged;

    /// <summary>
    /// A reference to the car for the player if the player has a car.
    /// </summary>
    public IInGameCar? Car
    {
        get => _car;
        set
        {
            if (ReferenceEquals(_car, value))
                return;

            _car = value;
            CarChanged?.Invoke(this, value);
        }
    }
    
    /// <summary>
    /// If this player is a bot, the bot object.
    /// </summary>
    public BaseAi? Bot { get; set; }
    
    /// <summary>
    /// True if the player was created by the gamemode.
    /// </summary>
    public bool IsFake { get; } = isFake;
}