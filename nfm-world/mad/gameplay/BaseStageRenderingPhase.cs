using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Mad.UI;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public abstract class BaseStageRenderingPhase(GraphicsDevice graphicsDevice) : BasePhase
{
    protected int? FovOverride = null;
    protected bool ShadowmapDisplay = true;

    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

    protected readonly GraphicsDevice _graphicsDevice = graphicsDevice;

    public PerspectiveCamera camera = new();
    public Camera[] lightCameras = [
        new OrthoLightCamera
        {
            Width = 3000,
            Height = 3000
        },
        new OrthoLightCamera
        {
            Width = 16384,
            Height = 16384
        },
        new OrthoLightCamera
        {
            Width = 65536,
            Height = 65536
        }
    ];

    public Stage CurrentStage = null!;
    public Scene current_scene = null!;

    public UnlimitedArray<InGameCar> CarsInRace = [];
    public int playerCarIndex = 0;

    public override void Exit()
    {
        base.Exit();
        GameSparker.CurrentMusic?.Unload();
    }

    public virtual void LoadStage(string stageName, bool loadMusic = true)
    {
        CurrentStage = new Stage(stageName, _graphicsDevice);

        GameSparker.CurrentMusic?.Unload();

        if (loadMusic && (!string.IsNullOrEmpty(CurrentStage.musicPath) || (GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(CurrentStage.remasteredMusicPath))))
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
        else
        {
            GameSparker.CurrentMusic?.Unload();
        }

        RecreateScene();
    }

    protected virtual void RecreateScene()
    {
        current_scene = new Scene(
            _graphicsDevice,
            [CurrentStage, new GameObject() { Children = CarsInRace }],
            camera,
            lightCameras
        );
    }

    public override void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyPressed(key, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;
    }

    public override void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
        base.KeyReleased(key, imguiWantsKeyboard);
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

        camera.Fov = FovOverride ?? CameraSettings.Fov;

        current_scene.Render(true);

        if (ShadowmapDisplay)
        {
            // DISPLAY SHADOW MAP
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
            _spriteBatch.Draw(Program.shadowRenderTargets[0], new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.Draw(Program.shadowRenderTargets[1], new Microsoft.Xna.Framework.Rectangle(0, 128, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.Draw(Program.shadowRenderTargets[2], new Microsoft.Xna.Framework.Rectangle(0, 256, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.End();
        }

        _graphicsDevice.Textures[0] = null;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
    }
}