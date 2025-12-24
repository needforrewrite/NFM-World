using NFMWorld.Mad;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Util;

public abstract class BaseGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase baseRacePhase)
{
    public int playerCarIndex => gamemodeParameters.PlayerCarIndex;
    public IReadOnlyList<PlayerParameters> players => gamemodeParameters.Players;
    public PlayerParameters player => gamemodeParameters.Players[playerCarIndex];
    public UnlimitedArray<InGameCar> carsInRace => baseRacePhase.CarsInRace;
    public Stage currentStage => baseRacePhase.CurrentStage;
    public Scene current_scene => baseRacePhase.current_scene;
    public int NumPlayers => players.Count;

    public virtual void Enter()
    {
        
    }

    public virtual void Exit()
    {
        
    }

    public virtual void GameTick()
    {

    }

    public virtual void KeyPressed(Keys key)
    {
        
    }

    public virtual void KeyReleased(Keys key)
    {
        
    }

    public virtual void Render()
    {
        
    }

    public virtual void Reset()
    {
        
    }
}