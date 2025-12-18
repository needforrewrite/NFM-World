using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad.UI;
using NFMWorld.Util;
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

    public static Stage current_stage = null!;
    public static Scene current_scene;

    public InRacePhase(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
    }
    
    public static UnlimitedArray<InGameCar> cars_in_race = [];
    public static int playerCarIndex = 0;
    public static int playerCarID = 14;

    public static FollowCamera PlayerFollowCamera = new();

    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    private static ViewMode currentViewMode = ViewMode.Follow;
    
    public override void Enter()
    {
        base.Enter();
        
        current_stage = new Stage("nfm2/15_dwm", _graphicsDevice);
        cars_in_race[playerCarIndex] = new InGameCar(playerCarID, GameSparker.cars[playerCarID], 0, 0);
        current_scene = new Scene(
            _graphicsDevice,
            [current_stage, ..cars_in_race],
            camera,
            lightCameras
        );
    }

    public override void GameTick()
    {
        base.GameTick();
        
        cars_in_race[playerCarIndex].Drive();
        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, cars_in_race[playerCarIndex].CarRef, cars_in_race[playerCarIndex].Mad.Cxz, cars_in_race[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                // Medium.Around(cars_in_race[playerCarIndex].Conto, true);
                break;
        }
        // camera.Position = new Vector3(0, 10000, 0);
        // camera.LookAt = new Vector3(1, 250, 0);
        
        foreach (var element in current_stage.pieces)
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
        _spriteBatch.Begin(0, BlendState.Opaque, SamplerState.PointClamp);
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
        G.DrawString($"Power: {cars_in_race[0]?.Mad?.Power:0.00}", 100, 140);
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;
        
        if (key == bindings.Accelerate)
        {
            cars_in_race[playerCarIndex].Control.Up = true;
        }
        if (key == bindings.Brake)
        {
            cars_in_race[playerCarIndex].Control.Down = true;
        }
        if (key == bindings.TurnRight)
        {
            cars_in_race[playerCarIndex].Control.Right = true;
        }
        if (key == bindings.TurnLeft)
        {
            cars_in_race[playerCarIndex].Control.Left = true;
        }
        if (key == bindings.Handbrake)
        {
            cars_in_race[playerCarIndex].Control.Handb = true;
        }
        if (key == bindings.Enter)
        {
            cars_in_race[playerCarIndex].Control.Enter = true;
        }
        if (key == bindings.LookBack)
        {
            cars_in_race[playerCarIndex].Control.Lookback = -1;
        }
        if (key == bindings.LookLeft)
        {
            cars_in_race[playerCarIndex].Control.Lookback = 3;
        }
        if (key == bindings.LookRight)
        {
            cars_in_race[playerCarIndex].Control.Lookback = 2;
        }
        if (key == bindings.ToggleMusic)
        {
            cars_in_race[playerCarIndex].Control.Mutem = !cars_in_race[playerCarIndex].Control.Mutem;
        }

        if (key == bindings.ToggleSFX)
        {
            cars_in_race[playerCarIndex].Control.Mutes = !cars_in_race[playerCarIndex].Control.Mutes;
        }

        if (key == bindings.ToggleArrace)
        {
            cars_in_race[playerCarIndex].Control.Arrace = !cars_in_race[playerCarIndex].Control.Arrace;
        }

        if (key == bindings.ToggleRadar)
        {
            cars_in_race[playerCarIndex].Control.Radar = !cars_in_race[playerCarIndex].Control.Radar;
        }
        if (key == bindings.CycleView)
        {
            currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
        }
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;
        
        if (cars_in_race[playerCarIndex].Control.Multion < 2)
        {
            if (key == bindings.Accelerate)
            {
                cars_in_race[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.Brake)
            {
                cars_in_race[playerCarIndex].Control.Down = false;
            }
            if (key == bindings.TurnRight)
            {
                cars_in_race[playerCarIndex].Control.Right = false;
            }
            if (key == bindings.TurnLeft)
            {
                cars_in_race[playerCarIndex].Control.Left = false;
            }
            if (key == bindings.Handbrake)
            {
                cars_in_race[playerCarIndex].Control.Handb = false;
            }
        }
        if (key == Keys.Escape)
        {
            cars_in_race[playerCarIndex].Control.Exit = false;
        }
        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
        {
            cars_in_race[playerCarIndex].Control.Lookback = 0;
        }
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
    }
}