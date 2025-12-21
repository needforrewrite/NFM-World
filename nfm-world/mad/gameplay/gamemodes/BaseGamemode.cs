using NFMWorld.Mad;
using NFMWorld.Util;

public abstract class BaseGamemode
{
    public virtual void Enter(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        
    }

    public virtual void Exit(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {
        
    }

    public virtual void GameTick(UnlimitedArray<InGameCar> carsInRace, Stage currentStage, Scene currentScene)
    {

    }

    public virtual void KeyPressed(Keys key)
    {
        
    }

    public virtual void KeyReleased(Keys key)
    {
        
    }

    public virtual void Render(UnlimitedArray<InGameCar> CarsInRace, Stage CurrentStage, Scene currentScene)
    {
        
    }

    public virtual void Reset()
    {
        
    }
}