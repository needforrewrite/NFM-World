using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad.UI;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public abstract class BaseRacePhase(GraphicsDevice graphicsDevice) : BasePhase
{
    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

    protected readonly GraphicsDevice _graphicsDevice = graphicsDevice;

    public PerspectiveCamera camera = new();
    public Camera[] lightCameras = [
        new OrthoCamera
        {
            Width = 3000,
            Height = 3000
        },
        new OrthoCamera
        {
            Width = 16384,
            Height = 16384
        },
        new OrthoCamera
        {
            Width = 65536,
            Height = 65536
        }
    ];

    public Stage CurrentStage = null!;
    public Scene current_scene = null!;

    public UnlimitedArray<InGameCar> CarsInRace = [];
    public int playerCarIndex = 0;
    protected FollowCamera PlayerFollowCamera = new();

    public bool spectating = false;
    
    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    protected ViewMode currentViewMode = ViewMode.Follow;
    
    public override void Exit()
    {
        base.Exit();
        GameSparker.CurrentMusic?.Unload();
    }

    public virtual void LoadStage(string stageName)
    {
        CurrentStage = new Stage(stageName, _graphicsDevice);

        GameSparker.CurrentMusic?.Unload();

        if(!string.IsNullOrEmpty(CurrentStage.musicPath) || (GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(CurrentStage.remasteredMusicPath)))
        {
            bool useRemastered = GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(CurrentStage.remasteredMusicPath);
            // Dont shift pitch or tempo if using remastered
            string path = useRemastered ? CurrentStage.remasteredMusicPath : CurrentStage.musicPath;
            double tempoMul = !useRemastered ? CurrentStage.musicTempoMul : 0d;
            double freqMul = !useRemastered ? CurrentStage.musicFreqMul : 1d;

            GameSparker.CurrentMusic = IBackend.Backend.LoadMusic(new Util.File($"./data/music/{path}"), tempoMul);
            GameSparker.CurrentMusic.SetFreqMultiplier(freqMul);
            GameSparker.CurrentMusic.SetVolume(IRadicalMusic.CurrentVolume);
            GameSparker.CurrentMusic.Play();   
        }
        
        RecreateScene();
    }

    protected virtual void RecreateScene()
    {
        current_scene = new Scene(
            _graphicsDevice,
            [CurrentStage, new ListRenderable(CarsInRace)],
            camera,
            lightCameras
        );
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
        
        var bindings = SettingsMenu.Bindings;

        if (!spectating)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = true;
            }
            if (key == bindings.AerialBounce)
            {
                CarsInRace[playerCarIndex].Control.Up = true;
                CarsInRace[playerCarIndex].Control.Down = true;
            }
            if (key == bindings.AerialStrafe)
            {
                if (CarsInRace[playerCarIndex].Control.Up && CarsInRace[playerCarIndex].Control.Down)
                {
                    CarsInRace[playerCarIndex].Control.Left = true;
                    CarsInRace[playerCarIndex].Control.Right = true;
                }
                if (CarsInRace[playerCarIndex].Control.Left)
                    CarsInRace[playerCarIndex].Control.Right = true;
                else if (CarsInRace[playerCarIndex].Control.Right)
                    CarsInRace[playerCarIndex].Control.Left = true;
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
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);

        var bindings = SettingsMenu.Bindings;
        
        if (!spectating)
        {
            if (key == bindings.Accelerate)
            {
                CarsInRace[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.AerialStrafe)
            {
                if (CarsInRace[playerCarIndex].Control.Up && CarsInRace[playerCarIndex].Control.Down)
                {
                    CarsInRace[playerCarIndex].Control.Left = false;
                    CarsInRace[playerCarIndex].Control.Right = false;
                }
                if (CarsInRace[playerCarIndex].Control.Left)
                    CarsInRace[playerCarIndex].Control.Right = false;
                else if (CarsInRace[playerCarIndex].Control.Right)
                    CarsInRace[playerCarIndex].Control.Left = false;
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
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);
        
        camera.Width = width;
        camera.Height = height;
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
    }
}