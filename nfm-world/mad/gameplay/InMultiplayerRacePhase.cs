using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Mad.Multiplayer;
using NFMWorldLibrary.Mad.Multiplayer.packets.c2s;
using NFMWorldLibrary.Mad.Multiplayer.packets.s2c;
using NFMWorldLibrary.Util;
using S2C_PlayerState = NFMWorldLibrary.Mad.Multiplayer.packets.s2c.S2C_PlayerState;

namespace NFMWorld.Gameplay;

public class InMultiplayerRacePhase(
    GraphicsDevice graphicsDevice,
    IMultiplayerClientTransport transport,
    S2C_RaceStarted.GameSession session,
    uint playerClientId
)
    : BaseRacePhase(graphicsDevice)
{
    protected IClientGamemode? gamemodeInstance { get; set; }
    private uint _ticks = 0; // overflows after ~497 days at 60 ticks per second
    private UnlimitedArray<uint> _lastTick = [];
    
    public override void Enter()
    {
        base.Enter();

        raceState = RaceState.WaitingToStart;

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

        LoadStage(session.StageName);

        var parameters = new BaseGamemodeParameters()
        {
            PlayerCarIndex = playerCarIndex,
            Players = session.Players
                .Select(c => new PlayerParameters
                {
                    CarName = c.Value.Vehicle,
                    Color = c.Value.Color,
                    PlayerName = c.Value.Name,
                    IsBot = false
                })
                .ToArray()
        };

        gamemodeInstance ??= session.Gamemode switch
        {
            GameModes.Sandbox => new SandboxClientGamemode(parameters, this),
            GameModes.Football => new FootballClientGamemode(parameters, this),
            GameModes.Racing => new RaceClientGamemode(parameters, this),
            GameModes.TimeTrial => new TimeTrialClientGamemode(parameters, this),
            _ => throw new ArgumentOutOfRangeException(nameof(session.Gamemode), session.Gamemode, null)
        };
        gamemodeInstance.Enter();
        
        transport.SendPacketToServer(new C2S_RaceLoaded());
    }

    public override void Exit()
    {
        base.Exit();
        gamemodeInstance?.Exit();
    }

    public override void GameTick()
    {
        base.GameTick();
        
        FrameTrace.AddMessage($"race state: {raceState}, player car index: {playerCarIndex}, spectating: {spectating}");
        
        foreach (var packet in transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_RaceCanStart raceCanStart:
                    raceState = RaceState.InProgress;
                    break;
                case S2C_RaceFailedToStart raceFailedToStart:
                    raceState = RaceState.FailedToStart;
                    break;
                case S2C_PlayerState playerState:
                    var carIndex = session.Players.First(e => e.Value.Id == playerState.PlayerClientId).Key;
                    var car = CarsInRace[carIndex];
                    if (playerState.State.Ticks <= _lastTick[carIndex])
                        break;
                    _lastTick[carIndex] = playerState.State.Ticks;
                    PlayerState.ApplyTo(playerState.State, car);
                    break;
            }
        }

        gamemodeInstance!.GameTick();

        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, CarsInRace[playerCarIndex], (float)CarsInRace[playerCarIndex].Mad.Cxz, CarsInRace[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                // Medium.Around(CarsInRace[playerCarIndex].Conto, true);
                break;
        }
        // camera.Position = new Vector3(0, 10000, 0);
        // camera.LookAt = new Vector3(1, 250, 0);
        
        current_scene.GameTick(CurrentStage);

        if (raceState == RaceState.InProgress)
        {
            transport.SendPacketToServer(new C2S_PlayerState()
            {
                State = PlayerState.CreateFrom(_ticks++, CarsInRace[playerCarIndex])
            });
        }
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
        if (raceState == RaceState.WaitingToStart)
        {
            G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 26));
            G.SetColor(new Color(255, 255, 255));
            G.DrawStringAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);
            
            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStrokeAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);
        }
    }
}