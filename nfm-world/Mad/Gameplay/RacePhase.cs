using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Gameplay.RaceHost;
using NFMWorld.UI;
using NFMWorld.UI.Cef;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// The single in-race phase for both singleplayer and multiplayer.
/// A gamemode (from <see cref="BaseGamemodeFactory"/>) drives gameplay while an
/// <see cref="IRaceHost"/> connects it to a host: in-process
/// (<see cref="LocalRaceHost"/>) for singleplayer, remote for multiplayer.
/// </summary>
public class RacePhase : BaseStageRenderingPhase, IGamemodeData, IClientCallbacks
{
    public readonly BaseGamemodeFactory Gamemode;
    public readonly IReadOnlyList<ClientSidePlayerParameters> Players;
    public IGamemode? GamemodeInstance { get; protected set; }

    private readonly IRaceHost _host;
    private uint _ticks;
    private readonly UnlimitedArray<uint> _lastTick = [];

    BackendStage IGamemodeData.CurrentStage => CurrentStage.Backend;

    /// <summary>
    /// HUD bridge for in-race overlay. Set in constructor so base.Enter()
    /// registers it and navigates to the race HUD page.
    /// </summary>
    protected HudBridge HudBridge { get; } = new();

    public bool AllowPausing { get; protected set; }

    public RacePhase(
        GraphicsDevice graphicsDevice,
        string stageName,
        BaseGamemodeFactory gamemode,
        IReadOnlyList<ClientSidePlayerParameters> players,
        IRaceHost host) : base(graphicsDevice, stageName)
    {
        Gamemode = gamemode;
        Players = players;
        _host = host;
        CefBridge = HudBridge;

        // Subscribe to pause-menu actions from the HUD bridge.
        HudBridge.ResumeRequested += () => ResumeRace();
        HudBridge.RestartRequested += () => QuitRace(); // Restart = quit for now; caller can re-push
        HudBridge.QuitRequested += () => QuitRace();
        HudBridge.SettingsCloseRequested += () =>
        {
            // Settings were dismissed — the JS PauseMenu handles its own
            // view transition back to the pause menu. No hash navigation needed.
        };

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
            RaceState = RaceState.Finished;
        };

        // Route gamemode → host events through the host, not the transport.
        GamemodeInstance?.SetEventSender(_host.SendServerEvent);

        // Singleplayer has no loading sync — start immediately.
        if (host is LocalRaceHost localHost)
            localHost.Start();
    }

    /// <summary>
    /// The local client player's car, or null if the gamemode hasn't assigned one yet.
    /// </summary>
    public IInGameCar? ClientCar => (GamemodeInstance as BaseClientGamemode)?.ClientPlayer.Car;

    public RaceState RaceState
    {
        get;
        set
        {
            field = value;
            RaceStateChanged?.Invoke(this, value);
        }
    } = RaceState.WaitingToStart;

    public RaceResults? RaceResults => GamemodeInstance?.GetResults();

    IClientCallbacks IGamemodeData.ClientCallbacks => this;

    public event EventHandler<RaceState>? RaceStateChanged;
    
    /// <summary>
    /// Called when the race is exited, to pop the phase.
    /// </summary>
    public event EventHandler? Exited;

    /// <summary>
    /// Whether the race is currently paused (pause menu is visible).
    /// While paused, CEF input is enabled, the gamemode does not tick,
    /// and the pause menu overlay is shown.
    /// </summary>
    public bool IsPaused { get; private set; }

    private readonly RaceInputController _input = new();
    private readonly RaceCameraDirector _cameraDirector = new();

    /// <summary>
    /// Push HUD state to the CEF race overlay each frame.
    /// Called by WorldGame.Update() when CefBridge is active.
    /// Skipped while paused — the pause menu handles its own rendering.
    /// </summary>
    public override void PushCefState()
    {
        base.PushCefState();

        if (IsPaused)
            return;

        if (GamemodeInstance is not BaseClientGamemode gm)
            return;

        HudBridge.PushHudState(gm.HudState);
    }

    // ── Pause / Resume / Quit ─────────────────────────────────────

    /// <summary>
    /// Pause the race: freeze gameplay, show the pause menu overlay,
    /// and enable CEF input so the player can interact with buttons.
    /// </summary>
    private void PauseRace()
    {
        if (IsPaused) return;
        IsPaused = true;
        RaceState = RaceState.Paused;
        GameSparker.CefRenderer?.SetInputEnabled(true);

        // Push pause context for the overlay (lap, position, stage name)
        if (GamemodeInstance is BaseClientGamemode gm)
        {
            var hud = gm.HudState;
            HudBridge.PushPauseState(hud.Lap, hud.TotalLaps, hud.Position, hud.TotalRacers, StageName ?? "");
        }
        else
        {
            HudBridge.PushPauseState(1, 1, 1, 1, StageName ?? "");
        }

        HudBridge.PushPausedEvent(true);
    }

    /// <summary>
    /// Resume the race: hide the pause menu, disable CEF input,
    /// and restore the race HUD.
    /// </summary>
    private void ResumeRace()
    {
        if (!IsPaused) return;
        IsPaused = false;
        RaceState = RaceState.InProgress;
        GameSparker.CefRenderer?.SetInputEnabled(false);
        HudBridge.PushPausedEvent(false);
        GameSparker.CefRenderer?.ConsumeKeyboardState();
    }

    /// <summary>
    /// Quit the race entirely. Fires the <see cref="Exited"/> event,
    /// which the caller (e.g., MainMenuPhase) uses to pop the phase group.
    /// </summary>
    private void QuitRace()
    {
        // Resume input forwarding state before exiting so the next phase
        // (main menu) gets clean input state.
        if (IsPaused)
        {
            IsPaused = false;
            GameSparker.CefRenderer?.SetInputEnabled(true); // Main menu needs input
        }
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public override void Enter()
    {
        // Gamemode is created in the constructor and survives across push/pop.
        // Enter/Exit only handle display activation/deactivation (CEF bridge,
        // camera, music) — no gamemode or stage reload.
        base.Enter();
    }

    protected IGamemode ReloadGamemode()
    {
        return Gamemode.CreateGameMode(new GamemodeParameters
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
            GamemodeInstance?.End();
            GamemodeInstance = null;
            _host.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard) return;

        // ── Pause toggle (Escape) ──────────────────────────────────
        if (key == Key.Escape)
        {
            if (IsPaused)
            {
                // If settings is open, let CEF handle Escape (it will dismiss settings via JS).
                // Otherwise, resume the race.
                if (!HudBridge.IsSettingsOpen)
                    ResumeRace();
            }
            else if (RaceState == RaceState.InProgress && AllowPausing)
            {
                PauseRace();
            }
            return;
        }

        // ── While paused, only forward keys to SettingsHandler ─────
        if (IsPaused)
        {
            HudBridge.Settings.TryHandleKeyPress(key);
            return;
        }

        if (key == SettingsMenu.Bindings.CycleView)
            _cameraDirector.CycleViewMode();

        _input.KeyPressed(key, ClientCar?.Control);

        GamemodeInstance?.KeyPressed(key, in keys);
    }

    public override void KeyTyped(char character, bool imguiWantsKeyboard)
    {
        base.KeyTyped(character, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;

        GamemodeInstance?.KeyTyped(character);
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);

        // While paused, skip all game control updates.
        if (IsPaused)
            return;

        _input.KeyReleased(key, ClientCar?.Control);

        GamemodeInstance?.KeyReleased(key, keys);
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        GamemodeInstance?.MousePressed(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        GamemodeInstance?.MouseReleased(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseScrolled(int x, int y, int delta, bool imguiWantsMouse, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        GamemodeInstance?.MouseScrolled(x, y, delta, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        GamemodeInstance?.MouseMoved(x, y, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void WindowSizeChanged(int width, int height)
    {
        base.WindowSizeChanged(width, height);

        Camera.Width = width;
        Camera.Height = height;
    }

    public override void Render(float alpha)
    {
        base.Render(alpha);

        if (DebugDisplay)
        {
            RenderMessages();
            G.SetColor(new Color(0, 0, 0));
            G.DrawString($"Render: {WorldGame.LastFrameTime}ms", 100, 100);
            G.DrawString($"Tick: {WorldGame.LastTickTime}μs", 100, 120);
            G.DrawString($"Power: {ClientCar?.CarPhysics?.Power:0.00}", 100, 140);
            G.DrawString($"Ticks executed last frame: {WorldGame.LastTickCount}", 100, 160);
        }

        if (RaceState == RaceState.WaitingToStart)
        {
            G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 26));
            G.SetColor(new Color(255, 255, 255));
            G.DrawStringAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);

            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStrokeAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);
        }

        GamemodeInstance?.Render();
    }

    private static void RenderMessages()
    {
        if (!FrameTrace.IsEnabled) return;

        const float x = 250;
        const float increment = 20;

        G.SetColor(new Color(0, 0, 0));
        G.DrawString(FrameTrace.GetMessageString(), (int)x, 0);
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
        // Pump host traffic first: race start signals, player states,
        // server events, and (for the local host) server gamemode ticks.
        _host.Update();

        if (IsPaused && AllowPausing)
            return;

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
        return GetCarVisual(car).Visuals;
    }

    void IGamemodeData.SendServerEvent(ReadOnlySpan<byte> payload)
    {
        _host.SendServerEvent(payload.ToArray());
    }

    void IGamemodeData.UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
    {
        // Server-driven player roster updates; wired up when the server can
        // change rosters mid-race.
    }

}
