using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class InMultiplayerRacePhase(
    GraphicsDevice graphicsDevice,
    IMultiplayerClientTransport transport,
    S2C_LobbyState.GameSession session,
    uint playerClientId
)
    : BaseRacePhase(graphicsDevice)
{
    public string playerCarName = "nfmm/radicalone";
    
    public override void Enter()
    {
        base.Enter();

        var player = session.PlayerClientIds
            .Select(c => (KeyValuePair<byte, uint>?) c)
            .FirstOrDefault(c => c!.Value.Value == playerClientId);
        
        if (player is { Key: var index })
            playerCarIndex = index;
        else
        {
            playerCarIndex = 0;
            spectating = true;
        }

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
                PlayerFollowCamera.Follow(camera, CarsInRace[playerCarIndex].CarRef, CarsInRace[playerCarIndex].Mad.Cxz, CarsInRace[playerCarIndex].Control.Lookback);
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
        
        transport.SendPacketToServer(new C2S_PlayerState()
        {
            State = PlayerState.CreateFrom(CarsInRace[playerCarIndex])
        });
    }

    protected override BaseGamemode CreateGameMode()
    {
        return gamemode switch
        {
            GameModes.Sandbox => new SandboxGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            GameModes.TimeTrial => new TimeTrialGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            _ => throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null)
        };
    }
}