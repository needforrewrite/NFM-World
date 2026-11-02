using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Audio;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorld.UI.Components;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Hud;

namespace NFMWorld.Gameplay;

/// <summary>
/// The single in-race phase for both singleplayer and multiplayer.
/// A gamemode (from <see cref="BaseGamemodeFactory"/>) drives gameplay while an
/// <see cref="IRaceHost"/> connects it to a host: in-process
/// (<see cref="LocalRaceHost"/>) for singleplayer, remote for multiplayer.
/// </summary>
public class RacePhase : BaseStageRenderingPhase, IGamemodeContext, IClientCallbacks
{
    /// <summary>The in-race HUD, with its pause menu wired to this phase.</summary>
    public override Node BuildUi() => RaceHudView.Build(new RaceHudView.Actions(
        Resume: ResumeRace,
        Restart: RestartRace,
        Quit: QuitRace));

    public readonly BaseGamemodeFactory Gamemode;
    public readonly IReadOnlyList<ClientSidePlayerInfo> Players;

    private readonly IRaceHost _host;
    private uint _ticks;
    private readonly UnlimitedArray<uint> _lastTick = [];

    private readonly RaceInputController _input = new();
    private readonly RaceCameraDirector _cameraDirector = new();
    private readonly SettingsCameraPreview _settingsCamera = new();
    private RaceState _stateBeforePause;
    public IGamemode? GamemodeInstance { get; protected set; }

    BackendStage IGamemodeContext.CurrentStage => CurrentStage.Backend;

    public bool AllowPausing { get; protected set; }

    public RacePhase(
        string stageName,
        BaseGamemodeFactory gamemode,
        IReadOnlyList<ClientSidePlayerInfo> players,
        IRaceHost host) : base(stageName)
    {
        Gamemode = gamemode;
        Players = players;
        _host = host;

        // Create the gamemode once at construction time. Enter/Exit only handle
        // display-level activation/deactivation; the gamemode survives across
        // push/pop cycles (e.g., opening Settings over a race).
        GamemodeInstance = ReloadGamemode();
        GamemodeInstance?.Begin();

        // The gamemode's Players array is the single source of truth for cars.
        // Point the client stage at it so CarVisuals are created per player.
        if (GamemodeInstance is BaseClientGamemode clientGamemode)
            CurrentStage.SetPlayers(clientGamemode.Players);

        // Host wiring: identical for local and network hosts.
        RaceState = RaceState.WaitingToStart;
        AllowPausing = host is LocalRaceHost;
        host.RaceCanStart += () => RaceState = RaceState.InProgress;
        host.RaceFailedToStart += () => RaceState = RaceState.FailedToStart;
        host.PlayerStateReceived += ApplyPlayerState;
        host.ServerEventReceived += payload => GamemodeInstance?.OnServerEvent(payload.Span);
        host.GameFinished += results =>
        {
            GamemodeInstance?.SetServerResults(results);
            RaceResults = results;
            RaceState = RaceState.Finished;
        };

        // Singleplayer has no loading sync — start immediately.
        if (host is LocalRaceHost localHost)
            localHost.Start();
    }

    /// <summary>
    /// The local client player's car, or null if the gamemode hasn't assigned one yet.
    /// </summary>
    public BackendCar? ClientCar => (GamemodeInstance as BaseClientGamemode)?.ClientPlayer.Car;

    public RaceState RaceState
    {
        get;
        set
        {
            field = value;
            RaceStateChanged?.Invoke(this, value);
        }
    } = RaceState.WaitingToStart;

    public RaceResults? RaceResults { get; private set; }

    IClientCallbacks IGamemodeContext.ClientCallbacks => this;

    public event EventHandler<RaceState>? RaceStateChanged;
    
    /// <summary>
    /// Called when the race is exited, to pop the phase.
    /// </summary>
    public event EventHandler? Exited;

    /// <summary>
    /// Whether gameplay is paused and the pause menu is visible.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Writes this frame's HUD values into the race view's signals.
    /// Skipped while paused - the pause menu shows its own snapshot.
    /// </summary>
    public override void PushUiState()
    {
        base.PushUiState();

        if (GamemodeInstance is not BaseClientGamemode gm)
            return;

        UiState.Race.Update(gm.HudState, IsPaused);
        UpdateHudPresentation(gm);
    }

    private void UpdateHudPresentation(BaseClientGamemode gm)
    {
        var hud = UiState.Race;
        hud.IsPreview = NFMWorld.UI.Components.SettingsView.HudPreviewActive;
        hud.Skin.Value = SettingsMenu.HudSkin;
        hud.Wasted = 0;
        hud.Opponents = 0;
        hud.Racers.Clear();
        hud.ArrowAngle = null;
        hud.ArrowTarget = "";
        hud.ArrowAtCars = false;
        hud.TimeTrial = gm.GamemodeId == "nfmm/timetrial";
        var client = ClientCar;
        BackendCar? targetCar = null;
        var nearest = double.PositiveInfinity;
        var targetOnscreen = false;
        foreach (var player in gm.Players)
        {
            if (player.IsFake || player.Car is not { } car) continue;
            var damage = car.Stats.Maxmag > 0 ? (float)car.CarPhysics.DamagePoints / car.Stats.Maxmag : 0;
            hud.Racers.Add(new HudRacer(car.Placement + 1, car.Stats.Name, damage,
                player.Info.IsClientPlayer, car.Wasted));
            if (player.Info.IsClientPlayer) continue;
            hud.Opponents++;
            if (car.Wasted) { hud.Wasted++; continue; }
            if (client is null) continue;
            var dx = (double)(car.Position.X - client.Position.X);
            var dz = (double)(car.Position.Z - client.Position.Z);
            var distance = dx * dx + dz * dz;
            var clip = Vector4.Transform(new Vector4((float)car.Position.X, (float)car.Position.Y,
                (float)car.Position.Z, 1), Camera.ViewProjectionMatrix);
            var onscreen = clip.W > 0 && MathF.Abs(clip.X) <= clip.W && MathF.Abs(clip.Y) <= clip.W;
            if ((targetOnscreen && !onscreen) || (onscreen == targetOnscreen && distance >= nearest)) continue;
            nearest = distance;
            targetCar = car;
            targetOnscreen = onscreen;
        }
        hud.Racers.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        if (client is not null && (targetCar is null || client.Wasted)) client.Control.Arrace = false;
        hud.ArrowAtCars = client?.Control.Arrace ?? false;
        if (client is null || client.Wasted) return;

        Vector3 target;
        if (hud.ArrowAtCars)
        {
            if (targetCar is null) return;
            target = new Vector3((float)targetCar.Position.X, (float)targetCar.Position.Y, (float)targetCar.Position.Z);
            hud.ArrowTarget = targetCar.Stats.Name;
        }
        else
        {
            var checkpoints = CurrentStage.Backend.Checkpoints;
            if (checkpoints.Count == 0) return;
            var checkpoint = checkpoints[Math.Clamp((int)client.CurrentCheckpoint, 0, checkpoints.Count - 1)];
            target = new Vector3((float)checkpoint.Position.X, (float)checkpoint.Position.Y, (float)checkpoint.Position.Z);
        }

        var direction = target - new Vector3((float)client.Position.X, (float)client.Position.Y, (float)client.Position.Z);
        var forward = Camera.LookAt - Camera.Position;
        if (direction.X * direction.X + direction.Z * direction.Z < 1 ||
            forward.X * forward.X + forward.Z * forward.Z < 1) return;
        var angle = MathF.Atan2(forward.Z * direction.X - forward.X * direction.Z,
            forward.X * direction.X + forward.Z * direction.Z) * (180 / MathF.PI);
        hud.ArrowAngle = Math.Clamp(angle, hud.ArrowAtCars ? -100 : -130, hud.ArrowAtCars ? 100 : 130);
    }

    // ── Pause / Resume / Quit ─────────────────────────────────────

    /// <summary>
    /// Freeze gameplay and show the pause menu.
    /// </summary>
    private void PauseRace()
    {
        if (IsPaused) return;
        _stateBeforePause = RaceState;
        _input.Reset(ClientCar?.Control);
        IsPaused = true;
        SoundEffects.SetPaused(true);
        RaceState = RaceState.Paused;

        // The pause menu reads the same signals the HUD does, so one write is enough.
        if (GamemodeInstance is BaseClientGamemode gm)
            UiState.Race.Update(gm.HudState, true);
        else
            UiState.Race.Paused.Value = true;
    }

    /// <summary>
    /// Hide the pause menu and restore the previous race state.
    /// </summary>
    private void ResumeRace()
    {
        if (!IsPaused) return;
        IsPaused = false;
        RaceState = _stateBeforePause;
        SoundEffects.SetPaused(false);
        UiState.Race.Paused.Value = false;
    }

    private void RestartRace()
    {
        if (!AllowPausing) return;
        GamemodeInstance?.Reset();
    }

    void IClientCallbacks.RaceRestarted()
    {
        if (_host is not LocalRaceHost localHost) return;
        localHost.Restart();
        RaceResults = null;
        RaceState = RaceState.InProgress;
        IsPaused = false;
        _input.Reset(ClientCar?.Control);
        _ticks = 0;
        SoundEffects.SetPaused(false);
        UiState.Race.Paused.Value = false;
    }

    /// <summary>
    /// Quit the race entirely. Fires the <see cref="Exited"/> event,
    /// which the caller (e.g., MainMenuPhase) uses to pop the phase group.
    /// </summary>
    private void QuitRace()
    {
        ResumeRace();
        _input.Reset(ClientCar?.Control);
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public override void Enter()
    {
        // Gamemode is created in the constructor and survives across push/pop.
        // Enter/Exit only handle display activation/deactivation (CEF bridge,
        // camera, music) — no gamemode or stage reload.
        base.Enter();
        var player = Players.FirstOrDefault(p => p.IsClientPlayer);
        if (player != null)
            MenuHistory.Remember(StageName, player.CarName);
    }

    protected IGamemode ReloadGamemode()
    {
        return Gamemode.CreateGameMode(new ClientGamemodeParameters
        {
            Players = Players
        }, this);
    }

    public override void Exit()
    {
        // Music pause is handled by BaseStageRenderingPhase.Exit().
        // Gamemode teardown (unload, exit cleanup) happens in Dispose().
        base.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host.Dispose();

            CurrentStage?.SetPlayers(null); // tears down visuals holding the gamemode's cars

            GamemodeInstance?.End();
            GamemodeInstance = null;

            // Last, and only after every reference this phase held into the gamemode is
            // gone: this is what lets the plugin's assembly actually be collected.
            Gamemode.End();
        }

        base.Dispose(disposing);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard) return;

        if (IsPaused && PauseMenuView.HandleKeyPressed(GameSparker.UiRenderer?.ActiveRoot, key))
            return;

        // ── Pause toggle (Escape) ──────────────────────────────────
        if (key == Key.Escape)
        {
            if (IsPaused)
                ResumeRace();
            else if (AllowPausing && RaceState is (RaceState.InProgress or RaceState.Finished))
                PauseRace();

            return;
        }

        // ── While paused, the UI owns input ────────────────────────
        if (IsPaused)
            return;

        if (key == SettingsMenu.Bindings.CycleView)
            _cameraDirector.CycleViewMode();

        var canTargetCars = key == SettingsMenu.Bindings.ToggleArrace &&
            GamemodeInstance is BaseClientGamemode gm && gm.Players.Any(player =>
                !player.IsFake && !player.Info.IsClientPlayer && player.Car is { Wasted: false });
        _input.KeyPressed(key, ClientCar?.Control, canTargetCars);

        GamemodeInstance?.KeyPressed(key, in keys);
    }

    public override void KeyTyped(char character, bool imguiWantsKeyboard)
    {
        base.KeyTyped(character, imguiWantsKeyboard);

        if (imguiWantsKeyboard || IsPaused) return;

        GamemodeInstance?.KeyTyped(character);
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);

        _input.KeyReleased(key, ClientCar?.Control);

        if (IsPaused) return;

        GamemodeInstance?.KeyReleased(key, keys);
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MousePressed(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MouseReleased(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseScrolled(int x, int y, int delta, bool imguiWantsMouse, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MouseScrolled(x, y, delta, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MouseMoved(x, y, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);

        Camera.Width = width;
        Camera.Height = height;
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        if (RaceState == RaceState.WaitingToStart)
        {
            G.SetFont(new Font(FontFamily.NotoSans, FontStyle.Plain, 26));
            G.SetColor(new Color(255, 255, 255));
            G.DrawStringOutlinedAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, new Color(0, 0, 0), 1f, TextHorizontalAlignment.Center);
        }

        GamemodeInstance?.Render();

        (G3D as ImmediateBatch3D)?.Flush(cb, Camera);
    }

    private void ApplyPlayerState(int carIndex, PlayerState state)
    {
        if (state.Ticks <= _lastTick[carIndex])
            return;

        _lastTick[carIndex] = state.Ticks;

        if ((GamemodeInstance as BaseClientGamemode)?.Players[carIndex].Car is { } car)
            PlayerState.ApplyTo(state, car);
    }

    public override void GameTick()
    {
        _settingsCamera.RestoreNormal(Camera);
        if (IsPaused && AllowPausing)
        {
            if (ClientCar is { } pausedCar &&
                _cameraDirector.CurrentViewMode is RaceCameraDirector.ViewMode.Follow or RaceCameraDirector.ViewMode.FollowStatic)
                _cameraDirector.Update(Camera, pausedCar);

            if (ClientCar is { } previewCar)
                _settingsCamera.Update(Camera, previewCar, NFMWorld.UI.Components.SettingsView.CameraPreviewActive);

            return;
        }

        _host.Update();

        _settingsCamera.Reset();

        if (RaceState is RaceState.InProgress or RaceState.Finished)
        {
            GamemodeInstance?.GameTick();
        }

        if (ClientCar is { } car)
            _cameraDirector.Update(Camera, car);

        if (RaceState == RaceState.InProgress)
        {
            var myCar = ClientCar;
            if (myCar is not null)
                _host.SendPlayerState(PlayerState.CreateFrom(_ticks++, myCar));
        }

        base.GameTick();
    }

    void IClientCallbacks.ResetCheckpointGlow()
    {
        CurrentStage.Renderer.ResetCheckpointGlow();
    }

    void IClientCallbacks.UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
    {
        CurrentStage.Renderer.UpdateCheckpointGlow(currentCheckpoint, isFinish);
    }

    IClientCarCallbacks IClientCallbacks.GetClientCarCallbacks(BackendCar car)
    {
        return GetCarVisual(car);
    }

    void IGamemodeContext.SendServerEvent(ReadOnlySpan<byte> payload)
    {
        _host.SendServerEvent(payload.ToArray());
    }

    void IGamemodeContext.UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
    {
        // Server-driven player roster updates; wired up when the server can
        // change rosters mid-race.
    }

}
