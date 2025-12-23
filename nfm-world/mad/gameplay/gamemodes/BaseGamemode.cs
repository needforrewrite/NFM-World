using NFMWorld.Mad;
using NFMWorld.Util;

public abstract class BaseGamemode(int playerCarIndex, UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
{
    protected int playerCarIndex = playerCarIndex;
    protected UnlimitedArray<InGameCar> carsInRace;
    protected Stage currentStage;
    protected Scene currentScene;
    
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