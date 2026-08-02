using System.Collections.ObjectModel;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public abstract class BaseStageRenderingPhase : BasePhase
{
    protected int? FovOverride = null;
    public static bool DebugDisplay = false;

    private readonly SpriteBatch _spriteBatch;

    public readonly GraphicsDevice GraphicsDevice;

    public PerspectiveCamera Camera = new();
    public Camera[] LightCameras = [
        new OrthoLightCamera { Width = 3000, Height = 3000 },
        new OrthoLightCamera { Width = 16384, Height = 16384 },
        new OrthoLightCamera { Width = 65536, Height = 65536 }
    ];

    public ClientStage CurrentStage = null!;
    public ObservableUnlimitedArray<IInGameCar> CarsInRace { get; protected set; } = [];

    private IRadicalMusic? _stageMusic;
    public string? StageName;
    
    // please don't pass null except for stage select
    protected BaseStageRenderingPhase(GraphicsDevice graphicsDevice, string? stageName = null)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        GraphicsDevice = graphicsDevice;
        StageName = stageName;

        // Stage loading happens once at construction time, not on every Enter().
        // This prevents phases from resetting when an overlay (e.g., Settings) is
        // pushed and popped over them.
        if (StageName != null)
            LoadStage(StageName);
    }

    public override void Enter()
    {
        base.Enter();

        Camera.Width = GameSparker.Game.GraphicsDevice.Viewport.Width;
        Camera.Height = GameSparker.Game.GraphicsDevice.Viewport.Height;

        // Resume stage music that was paused by Exit().
        if (_stageMusic != null)
            GameSparker.CurrentMusic = _stageMusic;
    }

    public override void Exit()
    {
        base.Exit();

        // Pause music while this phase is not displayed (buried in the stack).
        // Music is resumed in Enter() and unloaded in Dispose().
        GameSparker.CurrentMusic = null;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            CurrentStage?.Dispose();
            CurrentStage = null!;
            
            _stageMusic?.Dispose();
            _stageMusic = null;
        }
    }

    protected virtual void LoadStage(string stageName, bool loadMusic = true, bool reloadIfLoaded = false)
    {
        StageName = stageName;
        CurrentStage?.Dispose();
        CurrentStage = new ClientStage(GraphicsDevice, stageName, CarsInRace, Camera, LightCameras);

        if (loadMusic && !string.IsNullOrEmpty(CurrentStage.MusicPath))
            LoadStageMusic(reloadIfLoaded: reloadIfLoaded);
    }

    protected virtual void LoadStageMusic(bool reloadIfLoaded = false)
    {
        if ((reloadIfLoaded && GameSparker.CurrentMusic != null) || _stageMusic == null)
        {
            Logging.Debug("playing stage music: " + CurrentStage.MusicPath);

            bool useRemastered = GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(CurrentStage.RemasteredMusicPath);
            string path = useRemastered ? CurrentStage.RemasteredMusicPath : CurrentStage.MusicPath;
            double tempoMul = !useRemastered ? CurrentStage.MusicTempoMul : 1d;
            double freqMul = !useRemastered ? CurrentStage.MusicFreqMul : 1d;

            _stageMusic = IBackend.Backend.LoadMusic($"./data/music/{path}", tempoMul);
            _stageMusic.SetFreqMultiplier(freqMul);
        }
    }

    public CarVisual GetCarVisual(int index)
    {
        return CurrentStage.GetCarVisual(index);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        if (imguiWantsKeyboard) return;
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);

        G.Scale = 1280f / width;

        Camera.Width = width;
        Camera.Height = height;
    }

    public override void BeginGameTick()
    {
        CurrentStage?.OnBeforeGameTick();
        base.BeginGameTick();
    }

    public override void GameTick()
    {
        base.GameTick();
        CurrentStage?.GameTick();
    }

    public override void Render(float alpha)
    {
        base.Render(alpha);

        if (CurrentStage == null)
            return;

        foreach (var lightCamera in LightCameras)
        {
            lightCamera.Position = Camera.Position + new Vector3(0, -5000, 0);
            lightCamera.LookAt = Camera.Position + new Vector3(1f, 0, 0);
        }

        Camera.Fov = FovOverride ?? Camera.Fov;

        CurrentStage.Render(alpha, useShadowMapping: true);

        if (DebugDisplay)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
            if (WorldGame.ShadowRenderTargets[0] != null) _spriteBatch.Draw(WorldGame.ShadowRenderTargets[0], new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Color.White);
            if (WorldGame.ShadowRenderTargets[1] != null) _spriteBatch.Draw(WorldGame.ShadowRenderTargets[1], new Microsoft.Xna.Framework.Rectangle(0, 128, 128, 128), Color.White);
            if (WorldGame.ShadowRenderTargets[2] != null) _spriteBatch.Draw(WorldGame.ShadowRenderTargets[2], new Microsoft.Xna.Framework.Rectangle(0, 256, 128, 128), Color.White);
            _spriteBatch.End();
        }

        GraphicsDevice.Textures[0] = null;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
    }
}