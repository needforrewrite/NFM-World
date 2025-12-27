using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class InMultiplayerRacePhase(
    GraphicsDevice graphicsDevice,
    IMultiplayerClientTransport transport,
    S2C_RaceStarted.GameSession session,
    uint playerClientId
)
    : BaseRacePhase(graphicsDevice)
{
    protected BaseGamemode? gamemodeInstance { get; set; }

    public override void Enter()
    {
        base.Enter();

        var player = session.Players
            .Select(c => (KeyValuePair<byte, S2C_RaceStarted.PlayerInfo>?) c)
            .FirstOrDefault(c => c!.Value.Value.Id == playerClientId);
        
        if (player is { Key: var index })
            playerCarIndex = index;
        else
        {
            playerCarIndex = 0;
            spectating = true;
        }

        LoadStage("nfm2/15_dwm");

        var parameters = new BaseGamemodeParameters()
        {
            PlayerCarIndex = playerCarIndex,
            Players = session.Players
                .Select(c => new PlayerParameters()
                {
                    CarName = c.Value.Vehicle,
                    Color = c.Value.Color,
                    PlayerName = c.Value.Name
                })
                .ToArray()
        };

        gamemodeInstance ??= session.Gamemode switch
        {
            GameModes.Sandbox => new SandboxGamemode(parameters, this),
            GameModes.TimeTrial => new TimeTrialGamemode(parameters, this),
            _ => throw new ArgumentOutOfRangeException(nameof(session.Gamemode), session.Gamemode, null)
        };;
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
        
        transport.SendPacketToServer(new C2S_PlayerState()
        {
            State = PlayerState.CreateFrom(CarsInRace[playerCarIndex])
        });
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);
        gamemodeInstance?.KeyPressed(key);
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);
        gamemodeInstance?.KeyPressed(key);
    }

    public override void Render()
    {
        base.Render();
        gamemodeInstance?.Render();
    }
}