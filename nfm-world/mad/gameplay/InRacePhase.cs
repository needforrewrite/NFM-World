using ManagedBass;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad.UI;
using NFMWorld.Util;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad;

public class InRacePhase : BasePhase
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;

    public static PerspectiveCamera camera = new();
    public static Camera[] lightCameras = [
        new OrthoCamera()
        {
            Width = 3000,
            Height = 3000
        },
        new OrthoCamera()
        {
            Width = 16384,
            Height = 16384
        },
        new OrthoCamera()
        {
            Width = 65536,
            Height = 65536
        }
    ];

    public static Stage CurrentStage = null!;
    public static Scene current_scene;

    public InRacePhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
    }
    
    public static UnlimitedArray<InGameCar> CarsInRace = [];
    public static int playerCarIndex = 0;
    public static string playerCarName = "radicalone";

    public static FollowCamera PlayerFollowCamera = new();

    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    private static ViewMode currentViewMode = ViewMode.Follow;
    
    public static BaseGamemode gamemode = null!;

    public override void Enter()
    {
        base.Enter();

        gamemode = new TimeTrialGamemode();

        LoadStage("nfm2/15_dwm", _graphicsDevice);

        CarsInRace[playerCarIndex] = new InGameCar(playerCarIndex, GameSparker.GetCar(playerCarName).Car, 0, 0);
        current_scene = new Scene(
            _graphicsDevice,
            [CurrentStage, ..CarsInRace],
            camera,
            lightCameras
        );

        gamemode.Enter();
    }

    public override void Exit()
    {
        base.Exit();
        gamemode.Exit();

        GameSparker.CurrentMusic?.Unload();
    }

    public static void LoadStage(string stageName, GraphicsDevice graphicsDevice)
    {
        CurrentStage = new Stage(stageName, graphicsDevice);

        GameSparker.CurrentMusic?.Unload();

        if(!CurrentStage.musicPath.IsNullOrEmpty())
        {
            GameSparker.CurrentMusic = IBackend.Backend.LoadMusic(new Util.File($"./data/music/{CurrentStage.musicPath}"), CurrentStage.musicTempoMul);
            GameSparker.CurrentMusic.SetFreqMultiplier(CurrentStage.musicFreqMul);
            GameSparker.CurrentMusic.SetVolume(IRadicalMusic.CurrentVolume);
            GameSparker.CurrentMusic.Play();   
        }
    }

    public override void GameTick()
    {
        base.GameTick();
        
        gamemode.GameTick(CarsInRace, CurrentStage);

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

    public override void Render()
    {
        base.Render();

        foreach (var lightCamera in lightCameras)
        {
            lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
            lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0); // 0,0,0 causes shadows to break
        }

        current_scene.Render(true);

        // DISPLAY SHADOW MAP
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullClockwise);
        _spriteBatch.Draw(Program.shadowRenderTargets[0], new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.Draw(Program.shadowRenderTargets[1], new Microsoft.Xna.Framework.Rectangle(0, 128, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.Draw(Program.shadowRenderTargets[2], new Microsoft.Xna.Framework.Rectangle(0, 256, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.End();

        _graphicsDevice.Textures[0] = null;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        FrameTrace.RenderMessages();
        
        G.SetColor(new Color(0, 0, 0));
        G.DrawString($"Render: {Program._lastFrameTime}ms", 100, 100);
        G.DrawString($"Tick: {Program._lastTickTime}ms", 100, 120);
        G.DrawString($"Power: {CarsInRace[0]?.Mad?.Power:0.00}", 100, 140);
        G.DrawString($"Ticks executed last frame: {Program._lastTickCount}", 100, 160);

        gamemode.Render(CarsInRace, CurrentStage);
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;
        
        if (key == bindings.Accelerate)
        {
            CarsInRace[playerCarIndex].Control.Up = true;
        }
        if (key == bindings.Brake)
        {
            CarsInRace[playerCarIndex].Control.Down = true;
        }
        if (key == bindings.TurnRight)
        {
            CarsInRace[playerCarIndex].Control.Right = true;
        }
        if (key == bindings.TurnLeft)
        {
            CarsInRace[playerCarIndex].Control.Left = true;
        }
        if (key == bindings.Handbrake)
        {
            CarsInRace[playerCarIndex].Control.Handb = true;
        }
        if (key == bindings.Enter)
        {
            CarsInRace[playerCarIndex].Control.Enter = true;
        }
        if (key == bindings.LookBack)
        {
            CarsInRace[playerCarIndex].Control.Lookback = -1;
        }
        if (key == bindings.LookLeft)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 3;
        }
        if (key == bindings.LookRight)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 2;
        }
        if (key == bindings.ToggleMusic)
        {
            CarsInRace[playerCarIndex].Control.Mutem = !CarsInRace[playerCarIndex].Control.Mutem;
        }

        if (key == bindings.ToggleSFX)
        {
            CarsInRace[playerCarIndex].Control.Mutes = !CarsInRace[playerCarIndex].Control.Mutes;
        }

        if (key == bindings.ToggleArrace)
        {
            CarsInRace[playerCarIndex].Control.Arrace = !CarsInRace[playerCarIndex].Control.Arrace;
        }

        if (key == bindings.ToggleRadar)
        {
            CarsInRace[playerCarIndex].Control.Radar = !CarsInRace[playerCarIndex].Control.Radar;
        }
        if (key == bindings.CycleView)
        {
            currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
        }

        gamemode.KeyPressed(key);
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;
        
        if (CarsInRace[playerCarIndex].Control.Multion < 2)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.Brake)
            {
                CarsInRace[playerCarIndex].Control.Down = false;
            }
            if (key == bindings.TurnRight)
            {
                CarsInRace[playerCarIndex].Control.Right = false;
            }
            if (key == bindings.TurnLeft)
            {
                CarsInRace[playerCarIndex].Control.Left = false;
            }
            if (key == bindings.Handbrake)
            {
                CarsInRace[playerCarIndex].Control.Handb = false;
            }
        }
        if (key == Keys.Escape)
        {
            CarsInRace[playerCarIndex].Control.Exit = false;
        }
        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
        {
            CarsInRace[playerCarIndex].Control.Lookback = 0;
        }

        gamemode.KeyReleased(key);
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }
}