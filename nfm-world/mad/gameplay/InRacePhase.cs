using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Util;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad;

public class InRacePhase(GraphicsDevice graphicsDevice) : BaseRacePhase(graphicsDevice)
{
    public string playerCarName = "nfmm/radicalone";

    protected BaseGamemode? gamemodeInstance { get; set; }

    public GameModes gamemode
    {
        get;
        set
        {
            field = value;
            ReloadGamemode();
        }
    } = GameModes.Racing;

    public override void Enter()
    {
        base.Enter();

        LoadStage("nfm2/15_dwm");

        gamemodeInstance ??= CreateGameMode(new BaseGamemodeParameters
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

        base.GameTick();
        
        gamemodeInstance!.GameTick();

        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, CarsInRace[playerCarIndex], (float)CarsInRace[playerCarIndex].Mad.Cxz, CarsInRace[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                PlayerAroundCamera.Around(camera, CarsInRace[playerCarIndex]);
                break;
        }
        // camera.Position = new Vector3(0, 10000, 0);
        // camera.LookAt = new Vector3(1, 250, 0);

        current_scene.GameTick(CurrentStage);
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

    public override void Render()
    {
        base.Render();
        gamemodeInstance?.Render();
    }
    
    protected BaseGamemode CreateGameMode(BaseGamemodeParameters parameters)
    {
        return gamemode switch
        {
            GameModes.Sandbox => new SandboxGamemode(parameters, this),
            GameModes.TimeTrial => new TimeTrialGamemode(parameters, this),
            GameModes.Football => new FootballGamemode(parameters, this),
            GameModes.Racing => new RaceGamemode(parameters, this),
            _ => throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null)
        };
    }
}