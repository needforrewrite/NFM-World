using Microsoft.Xna.Framework.Graphics;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad;

public class InRacePhase(GraphicsDevice graphicsDevice) : BaseRacePhase(graphicsDevice)
{
    public string playerCarName = "nfmm/radicalone";
    
    public override void Enter()
    {
        base.Enter();

        LoadStage("nfm2/15_dwm");

        gamemodeInstance ??= CreateGameMode();
        gamemodeInstance.Enter();
    }

    public override void Exit()
    {
        base.Exit();
        gamemodeInstance?.Exit();
    }

    public override void GameTick()
    {
        base.GameTick();
        
        gamemodeInstance!.GameTick();

        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, CarsInRace[playerCarIndex].CarRef, (float)CarsInRace[playerCarIndex].Mad.Cxz, CarsInRace[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                // Medium.Around(CarsInRace[playerCarIndex].Conto, true);
                break;
        }
        // camera.Position = new Vector3(0, 10000, 0);
        // camera.LookAt = new Vector3(1, 250, 0);
        
        foreach (var element in CurrentStage.pieces)
        {
            element.GameTick();
        }
    }

    protected override BaseGamemode CreateGameMode()
    {
        return gamemode switch
        {
            GameModes.Sandbox => new SandboxGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            GameModes.TimeTrial => new TimeTrialGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            GameModes.Football => new FootballGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            _ => throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null)
        };
    }
}