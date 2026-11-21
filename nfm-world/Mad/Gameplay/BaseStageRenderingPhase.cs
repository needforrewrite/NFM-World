using NFMWorld.Audio;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public abstract class BaseStageRenderingPhase : BasePhase
{
    protected int? FovOverride = null;

    public static bool DebugDisplay =
#if DEBUG
            true
#else
            false
#endif
        ;

    public PerspectiveCamera Camera = new();

    public Camera[] LightCameras =
    [
        new OrthoLightCamera { Width = 3000, Height = 3000 },
        new OrthoLightCamera { Width = 16384, Height = 16384 },
        new OrthoLightCamera { Width = 65536, Height = 65536 }
    ];

    public ClientStage? CurrentStage;
    public ObservableUnlimitedArray<BackendCar> CarsInRace { get; protected set; } = [];

    private IRadicalMusic? _stageMusic;
    protected IRadicalMusic? StageMusic => _stageMusic;
    private CancellationTokenSource? _stageCancellation;
    private MusicRequest? _musicRequest;
    private bool _disposed;
    private bool _active;
    protected bool IsStageLoading { get; private set; }

    private sealed class MusicRequest(BaseStageRenderingPhase owner)
    {
        public BaseStageRenderingPhase Owner = owner;
    }

    public string? StageName;

    // please don't pass null stage names except for stage select
    protected BaseStageRenderingPhase(string? stageName = null, BaseStageRenderingPhase? stageSource = null)
    {
        StageName = stageName;

        // Re-entering an overlay resumes the same stage and pending loads.
        if (stageSource != null)
            TakeStageFrom(stageSource, takeMusic: stageSource is not StageSelectPhase);
        else if (StageName != null)
            LoadStage(StageName);
    }

    internal void TakeStageFrom(BaseStageRenderingPhase source, bool takeMusic = true)
    {
        if (CurrentStage != null || source.CurrentStage == null || source.StageName != StageName)
            throw new InvalidOperationException("Stage handoff requires an empty destination and the same loaded stage.");

        CurrentStage = source.CurrentStage;
        if (takeMusic)
        {
            _stageMusic = source._stageMusic;
            source._stageMusic = null;
            _musicRequest = source._musicRequest;
            if (_musicRequest is not null) _musicRequest.Owner = this;
            source._musicRequest = null;
        }

        Camera = source.Camera;
        Camera.Fov = PerspectiveCamera.DefaultFov;
        LightCameras = source.LightCameras;
        source.CurrentStage = null;

        CurrentStage.SetCars(CarsInRace);
        CurrentStage.FadeFromOverride = null;
        CurrentStage.CarSoundsEnabled = true;
    }

    public override void Enter()
    {
        _active = true;
        base.Enter();

        Camera.Width = GameSparker.Game.Window.Width;
        Camera.Height = GameSparker.Game.Window.Height;
        CurrentStage?.Renderer.Activate();

        // Resume stage music that was paused by Exit().
        if (_stageMusic != null && !ReferenceEquals(GameSparker.CurrentMusic, _stageMusic))
            GameSparker.CurrentMusic = _stageMusic;
    }

    public override void Exit() => ExitStage(keepMusicPlaying: false);

    protected void ExitStage(bool keepMusicPlaying)
    {
        _active = false;
        base.Exit();

        CurrentStage?.StopSounds();
        IBackend.Backend.StopAllSounds();

        // Pause music while this phase is not displayed (buried in the stack).
        // Music is resumed in Enter() and unloaded in Dispose().
        if (!keepMusicPlaying && _stageMusic != null && ReferenceEquals(GameSparker.CurrentMusic, _stageMusic))
            GameSparker.CurrentMusic = null;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _disposed = true;
            UnloadStage();
            UnloadStageMusic();
        }
    }

    protected void UnloadStage()
    {
        _stageCancellation?.Cancel();
        _stageCancellation?.Dispose();
        _stageCancellation = null;
        IsStageLoading = false;
        CurrentStage?.Dispose();
        CurrentStage = null;
    }

    protected void UnloadStageMusic()
    {
        _musicRequest = null;
        if (_stageMusic != null && ReferenceEquals(GameSparker.CurrentMusic, _stageMusic))
            GameSparker.CurrentMusic = null;
        _stageMusic?.Dispose();
        _stageMusic = null;
    }

    protected virtual void LoadStage(string stageName, bool loadMusic = true, bool reloadIfLoaded = false)
    {
        BeginStageLoad(stageName, loadMusic, reloadIfLoaded);
    }

    protected void BeginStageLoad(
        string stageName,
        bool loadMusic = true,
        bool reloadIfLoaded = false,
        StageLoader? stageData = null
    )
    {
        _stageCancellation?.Cancel();
        _stageCancellation?.Dispose();
        _stageCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        StageName = stageName;
        IsStageLoading = true;
        _ = CompleteStageLoadAsync(stageName, loadMusic, reloadIfLoaded, stageData, _stageCancellation.Token);
    }

    private async Task CompleteStageLoadAsync(
        string stageName,
        bool loadMusic,
        bool reloadIfLoaded,
        StageLoader? stageData,
        CancellationToken token
    )
    {
        ClientStage? next = null;
        try
        {
            next = await ClientStage.CreateAsync(stageName, CarsInRace, Camera, LightCameras, token, stageData);
            await LoadingWork.NextFrame;
            token.ThrowIfCancellationRequested();
            if (_disposed)
            {
                next.Dispose();
                return;
            }

            OnStageReplacing();
            CurrentStage?.Dispose();
            CurrentStage = next;
            CurrentStage.Adopt();
            next = null;
            IsStageLoading = false;
            if (_active) CurrentStage.Renderer.Activate();
            OnStageLoaded();
            if (loadMusic) LoadStageMusic(reloadIfLoaded: reloadIfLoaded);
        }
        catch (OperationCanceledException)
        {
            await LoadingWork.NextFrame;
            next?.Dispose();
        }
        catch (Exception ex)
        {
            await LoadingWork.NextFrame;
            next?.Dispose();
            if (token.IsCancellationRequested || _disposed) return;
            IsStageLoading = false;
            Logging.Error($"Could not load stage '{stageName}': {ex.Message}");
            OnStageLoadFailed(ex);
        }
    }

    protected virtual void OnStageLoaded()
    {
    }

    protected virtual void OnStageReplacing()
    {
    }

    protected virtual void OnStageLoadFailed(Exception error)
        => GameSparker.MessageWindow.ShowMessage("Could not load stage", error.Message);

    protected virtual (string Path, double Tempo, double Frequency)? GetMusicTrack()
    {
        if (CurrentStage == null || string.IsNullOrEmpty(CurrentStage.MusicPath)) return null;
        var remastered = GameSparker.UseRemasteredMusic && !string.IsNullOrEmpty(CurrentStage.RemasteredMusicPath);
        return ($"data/music/{(remastered ? CurrentStage.RemasteredMusicPath : CurrentStage.MusicPath)}",
            remastered ? 1d : CurrentStage.MusicTempoMul,
            remastered ? 1d : CurrentStage.MusicFreqMul);
    }

    protected virtual void LoadStageMusic(bool reloadIfLoaded = false)
    {
        if (!reloadIfLoaded && (_stageMusic is not null || _musicRequest is not null)) return;
        _musicRequest = null;
        if (GetMusicTrack() is not { } track)
        {
            UnloadStageMusic();
            return;
        }

        byte[]? embedded = null;
        CurrentStage?.Backend.StageLoader.EmbeddedMusic?.TryGetValue(track.Path["data/music/".Length..], out embedded);
        var request = _musicRequest = new MusicRequest(this);
        _ = CompleteMusicLoadAsync(request, track, embedded);
    }

    private static async Task CompleteMusicLoadAsync(
        MusicRequest request,
        (string Path, double Tempo, double Frequency) track,
        byte[]? embedded
    )
    {
        IRadicalMusic? music = null;
        try
        {
            music = await MusicLoader.LoadAsync(track.Path, track.Tempo, embedded);
            await LoadingWork.NextFrame;
            var owner = request.Owner;
            if (owner._disposed || !ReferenceEquals(owner._musicRequest, request))
            {
                music.Dispose();
                return;
            }

            music.SetFreqMultiplier(track.Frequency);
            var previous = owner._stageMusic;
            owner._stageMusic = music;
            owner._musicRequest = null;
            if (owner._active) GameSparker.CurrentMusic = music;
            previous?.Dispose();
        }
        catch (Exception ex)
        {
            await LoadingWork.NextFrame;
            music?.Dispose();
            var owner = request.Owner;
            if (ReferenceEquals(owner._musicRequest, request)) owner._musicRequest = null;
            Logging.Error($"Could not load music '{track.Path}': {ex.Message}");
        }
    }

    public CarVisual GetCarVisual(int index)
    {
        return CurrentStage!.GetCarVisual(index);
    }

    public CarVisual GetCarVisual(BackendCar car)
    {
        return CurrentStage!.GetCarVisual(car);
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

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        if (CurrentStage == null)
            return;

        foreach (var lightCamera in LightCameras)
        {
            lightCamera.Position = Camera.Position + new Vector3(0, -5000, 0);
            lightCamera.LookAt = Camera.Position + new Vector3(1f, 0, 0);
        }

        Camera.Fov = FovOverride ?? Camera.Fov;

        CurrentStage.Render(cb, alpha, useShadowMapping: true);

        // Shadow-cascade debug overlay (r_debugdisplay). Restored off the back of the migration onto
        // ICommandBuffer + IGraphicsDevice.ReadTexture - see ShadowMapDebugView for why it is a
        // throttled CPU readback rather than the old SpriteBatch blit.
        if (DebugDisplay)
            ShadowMapDebugView.Capture(cb);
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        if (DebugDisplay)
            ShadowMapDebugView.DrawImgui();
    }
}
