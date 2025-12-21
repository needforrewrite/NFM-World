using NFMWorld.Mad;
using NFMWorld.Util;

public abstract class BaseGamemode
{
    public virtual void Enter(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        
    }

    public virtual void Exit(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {
        
    }

    public virtual void GameTick(UnlimitedArray<InGameCar> carsInRace, Stage currentStage)
    {

    }

    public virtual void KeyPressed(Keys key)
    {
        
    }

    public virtual void KeyReleased(Keys key)
    {
        
    }

    public virtual void Render(UnlimitedArray<InGameCar> CarsInRace, Stage CurrentStage)
    {
        
    }

    public virtual void Reset()
    {
        
    }
}