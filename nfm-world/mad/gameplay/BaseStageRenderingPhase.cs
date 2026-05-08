using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Camera;
using NFMWorld.DriverInterface;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public abstract class BaseStageRenderingPhase(GraphicsDevice graphicsDevice) : BasePhase
{
    protected int? FovOverride = null;
    public static bool DebugDisplay = false;

    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

    public readonly GraphicsDevice GraphicsDevice = graphicsDevice;

    public PerspectiveCamera camera = new();
    public Camera.Camera[] lightCameras = [
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

    public BackendStage CurrentStage = null!;
    public Scene current_scene = null!;

    public UnlimitedArray<IInGameCar> CarsInRace { get; protected set; } = [];
    public int playerCarIndex = 0;
    private ClientCarCollection clientCarCollection;
    public ClientStageRenderer clientStageRenderer;

    public override void Exit()
    {
        base.Exit();
        GameSparker.CurrentMusic?.Unload();
    }

    public virtual void LoadStageMusic(bool reloadIfLoaded = false)
    {
        if ((reloadIfLoaded && GameSparker.CurrentMusic != null) || GameSparker.CurrentMusic == null)
        {
            if(reloadIfLoaded && GameSparker.CurrentMusic != null)
            {
                GameSparker.CurrentMusic?.Unload();
            }

            Logging.Debug("playing stage music: " + clientStageRenderer.musicPath);

            bool useRemastered = GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(clientStageRenderer.remasteredMusicPath);
            // Dont shift pitch or tempo if using remastered
            string path = useRemastered ? clientStageRenderer.remasteredMusicPath : clientStageRenderer.musicPath;
            double tempoMul = !useRemastered ? clientStageRenderer.musicTempoMul : 0d;
            double freqMul = !useRemastered ? clientStageRenderer.musicFreqMul : 1d;

            GameSparker.CurrentMusic = IBackend.Backend.LoadMusic($"./data/music/{path}", tempoMul);
            GameSparker.CurrentMusic.SetFreqMultiplier(freqMul);
            GameSparker.CurrentMusic.SetVolume(IRadicalMusic.CurrentVolume);
            GameSparker.CurrentMusic.Play();
        }
    }

    public virtual void LoadStage(string stageName, bool loadMusic = true)
    {
        CurrentStage = new BackendStage(stageName);
        clientStageRenderer = new ClientStageRenderer(GraphicsDevice, CurrentStage);

        RecreateScene();

        if (loadMusic && (!string.IsNullOrEmpty(clientStageRenderer.musicPath) || (GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(clientStageRenderer.remasteredMusicPath))))
        {
            LoadStageMusic(true);
        }
    }

    public virtual void RecreateScene()
    {
        clientCarCollection = new ClientCarCollection(GraphicsDevice, CarsInRace);
        current_scene = new Scene(
            GraphicsDevice,
            [clientStageRenderer, clientCarCollection],
            camera,
            lightCameras
        );
    }
    public ClientCar GetClientCar(int index)
    {
        return clientCarCollection.GetCar(CarsInRace[index]);
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

        G.Scale = 1280f / width;

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

        if (DebugDisplay)
        {
            // DISPLAY SHADOW MAP
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
            _spriteBatch.Draw(Program.shadowRenderTargets[0], new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.Draw(Program.shadowRenderTargets[1], new Microsoft.Xna.Framework.Rectangle(0, 128, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.Draw(Program.shadowRenderTargets[2], new Microsoft.Xna.Framework.Rectangle(0, 256, 128, 128), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.End();
        }

        GraphicsDevice.Textures[0] = null;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
    }
}