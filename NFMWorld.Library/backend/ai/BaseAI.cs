using NFMWorldLibrary.Mad;

namespace NFMWorldLibrary.Backend.AI;

/// <summary>
/// Base AI class for gamemode-specific AI implementations.
/// </summary>
public abstract class BaseAi
{
    public abstract void RunAi(IInGameCar car, int currentCarIndex);
}

// End of ReLitAi class