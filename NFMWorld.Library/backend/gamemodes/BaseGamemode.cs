using nfm_world_library.mad;
using nfm_world_library.util;

namespace nfm_world_library.backend.gamemodes;

public abstract class BaseGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues) : IGamemode
{
    public int playerCarIndex => gamemodeParameters.PlayerCarIndex;
    public IReadOnlyList<PlayerParameters> players => gamemodeParameters.Players;
    public PlayerParameters player => gamemodeParameters.Players[playerCarIndex];
    public UnlimitedArray<IInGameCar> carsInRace => raceValues.CarsInRace;
    public BackendStage currentStage => raceValues.CurrentStage;
    public int NumPlayers => players.Count;
    public RaceState RaceState => raceValues.raceState;

    /// <summary>
    /// Arguments: byte[] player standings indexed by player index
    /// </summary>
    public abstract event EventHandler<byte[]>? RaceFinished;

    public virtual void Enter()
    {
        
    }

    public virtual void Exit()
    {
        
    }

    public virtual void GameTick()
    {

    }

    public virtual void Reset()
    {
        
    }
}