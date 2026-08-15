using NFMWorldLibrary.Backend.AI;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Bot driver that delegates decision-making to the gamemode's
/// <c>on_ai_tick(car, index)</c> Lua callback.
/// </summary>
public sealed class LuaBot : BaseAi
{
    private readonly LuaGamemode _gamemode;

    public LuaBot(LuaGamemode gamemode)
        => _gamemode = gamemode;

    public override void RunAi(IInGameCar car, int currentCarIndex)
        => _gamemode.OnAiTick(car, currentCarIndex);
}
