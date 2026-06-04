using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorld.Util;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

public class InRacePhase(GraphicsDevice graphicsDevice) : BaseRacePhase(graphicsDevice)
{
    public string playerCarName = "nfmm/radicalone";

    protected IClientGamemode? gamemodeInstance { get; set; }

    public GameModes gamemode
    {
        get;
        set;
    } = GameModes.Racing;

    public void SetGamemode(GameModes mode)
    {
        gamemode = mode;
        ReloadGamemode();
    }

    public override void Enter()
    {
        base.Enter();

        RecreateScene();
        ReloadGamemode();
    }

    public override void Exit()
    {
        base.Exit();
        gamemodeInstance?.Exit();
    }

    public void ReloadGamemode()
    {
        gamemodeInstance = CreateGameMode(new BaseGamemodeParameters
        {
            Players =
            [
                new PlayerParameters
                {
                    CarName = playerCarName,
                    Color = new Color3(255, 0, 0),
                    PlayerName = "Player",
                    IsBot = false
                },
                new PlayerParameters()
                {
                    CarName = "nfmm/audir8",
                    Color = new Color3(255, 0, 0),
                    PlayerName = "Player2",
                    IsBot = true
                }
            ],
            PlayerCarIndex = playerCarIndex
        });
        gamemodeInstance.Enter();
    }

    public override void GameTick()
    {
        gamemodeInstance!.GameTick();

        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, CarsInRace[playerCarIndex], (float)(CarsInRace[playerCarIndex].Mad.Cxz), CarsInRace[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                PlayerAroundCamera.Around(camera, CarsInRace[playerCarIndex]);
                break;
        }
        
        base.GameTick();
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);
        gamemodeInstance?.KeyPressed(key);
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);
        gamemodeInstance?.KeyReleased(key);
    }

    public override void Render(float alpha)
    {
        base.Render(alpha);
        gamemodeInstance?.Render();
    }
    
    protected IClientGamemode CreateGameMode(BaseGamemodeParameters parameters)
    {
        return gamemode switch
        {
            GameModes.Sandbox => new SandboxClientGamemode(parameters, this),
            GameModes.TimeTrial => new TimeTrialClientGamemode(parameters, this),
            GameModes.Football => new FootballClientGamemode(parameters, this),
            GameModes.Racing => new RaceClientGamemode(parameters, this),
            _ => throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null)
        };
    }
}