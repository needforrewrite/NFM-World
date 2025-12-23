using ManagedBass;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad;

public class InRacePhase : BaseRacePhase
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;

    public InRacePhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
    }
    
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

        GameSparker.CurrentMusic?.Unload();
    }

    public void LoadStage(string stageName)
    {
        CurrentStage = new Stage(stageName, _graphicsDevice);

        GameSparker.CurrentMusic?.Unload();

        if(!CurrentStage.musicPath.IsNullOrEmpty())
        {
            GameSparker.CurrentMusic = IBackend.Backend.LoadMusic(new Util.File($"./data/music/{CurrentStage.musicPath}"), CurrentStage.musicTempoMul);
            GameSparker.CurrentMusic.SetFreqMultiplier(CurrentStage.musicFreqMul);
            GameSparker.CurrentMusic.SetVolume(IRadicalMusic.CurrentVolume);
            GameSparker.CurrentMusic.Play();   
        }
        
        RecreateScene();
    }

    private void RecreateScene()
    {
        current_scene = new Scene(
            _graphicsDevice,
            [CurrentStage, new ListRenderable(CarsInRace)],
            camera,
            lightCameras
        );
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
    }

    public override void Render()
    {
        base.Render();

        foreach (var lightCamera in lightCameras)
        {
            lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
            lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0); // 0,0,0 causes shadows to break
        }

        camera.Fov = CameraSettings.Fov;

        current_scene.Render(true);

        // DISPLAY SHADOW MAP
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
        _spriteBatch.Draw(Program.shadowRenderTargets[0], new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.Draw(Program.shadowRenderTargets[1], new Microsoft.Xna.Framework.Rectangle(0, 128, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.Draw(Program.shadowRenderTargets[2], new Microsoft.Xna.Framework.Rectangle(0, 256, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.End();

        _graphicsDevice.Textures[0] = null;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        FrameTrace.RenderMessages();
        
        G.SetColor(new Color(0, 0, 0));
        G.DrawString($"Render: {Program._lastFrameTime}ms", 100, 100);
        G.DrawString($"Tick: {Program._lastTickTime}μs", 100, 120);
        G.DrawString($"Power: {CarsInRace[0]?.Mad?.Power:0.00}", 100, 140);
        G.DrawString($"Ticks executed last frame: {Program._lastTickCount}", 100, 160);

        gamemodeInstance!.Render();
    }

    public override GameModes gamemode
    {
        get;
        set
        {
            field = value;
            gamemodeInstance = CreateGameMode();
            gamemodeInstance.Enter();
        }
    }

    public override void ReloadGamemode()
    {
        gamemodeInstance = CreateGameMode();
        gamemodeInstance.Enter();
    }

    private BaseGamemode CreateGameMode()
    {
        return gamemode switch
        {
            GameModes.Sandbox => new SandboxGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            GameModes.TimeTrial => new TimeTrialGamemode(playerCarName, playerCarIndex, CarsInRace, CurrentStage, current_scene),
            _ => throw new ArgumentOutOfRangeException(nameof(gamemode), gamemode, null)
        };
    }
}