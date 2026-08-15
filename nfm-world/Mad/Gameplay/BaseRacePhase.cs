﻿using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.UI;
using NFMWorld.UI.Cef;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public abstract class BaseRacePhase : BaseStageRenderingPhase, IGamemodeData, IClientCallbacks
{
    public readonly BaseGamemodeFactory Gamemode;
    public readonly IReadOnlyList<ClientSidePlayerParameters> Players;
    public IGamemode? GamemodeInstance { get; protected set; }

    BackendStage IGamemodeData.CurrentStage => CurrentStage.Backend;

    /// <summary>
    /// HUD bridge for in-race overlay. Set in constructor so base.Enter()
    /// registers it and navigates to the race HUD page.
    /// </summary>
    protected HudBridge HudBridge { get; } = new();

    public bool AllowPausing { get; protected set; }

    protected BaseRacePhase(GraphicsDevice graphicsDevice, string stageName, BaseGamemodeFactory gamemode, IReadOnlyList<ClientSidePlayerParameters> players) : base(graphicsDevice, stageName)
    {
        Gamemode = gamemode;
        Players = players;
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
    } = RaceState.InProgress;

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

    protected FollowCamera PlayerFollowCamera = new();
    protected AroundCamera PlayerAroundCamera = new();
    protected AroundStageCamera StageAroundCamera = new();

    // Track which keys are currently pressed to properly handle meta-bindings
    private HashSet<Key> _pressedKeys = new();

    // View modes
    public enum ViewMode
    {
        Follow,
        FollowStatic,
        Around,
        Watch
    }
    protected ViewMode currentViewMode = ViewMode.Follow;

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

    protected virtual IGamemode ReloadGamemode()
    {
        return CreateGameMode(new GamemodeParameters
        {
            Players = Players
        });
    }

    protected IGamemode CreateGameMode(GamemodeParameters parameters)
    {
        return Gamemode.CreateGameMode(parameters, this);
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
            else if (RaceState == RaceState.InProgress)
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

        var bindings = SettingsMenu.Bindings;

        // Track pressed keys
        _pressedKeys.Add(key);

        // Update control state based on all currently pressed keys
        UpdateControlState();

        // Handle non-movement keys
        if (GamemodeInstance != null)
        {
            var control = ClientCar?.Control;

            if (control != null)
            {
                if (key == bindings.Enter)
                {
                    control.Enter = true;
                }

                if (key == bindings.LookBack)
                {
                    control.Lookback = -1;
                }

                if (key == bindings.LookLeft)
                {
                    control.Lookback = 3;
                }

                if (key == bindings.LookRight)
                {
                    control.Lookback = 2;
                }

                if (key == bindings.ToggleMusic)
                {
                    control.Mutem = !control.Mutem;
                }

                if (key == bindings.ToggleSFX)
                {
                    control.Mutes = !control.Mutes;
                }

                if (key == bindings.ToggleArrace)
                {
                    control.Arrace = !control.Arrace;
                }

                if (key == bindings.ToggleRadar)
                {
                    control.Radar = !control.Radar;
                }

                if (key == bindings.CycleView)
                {
                    currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
                }
            }
        }

        GamemodeInstance?.KeyPressed(key, in keys);
    }

    public override void KeyTyped(char character, bool imguiWantsKeyboard)
    {
        base.KeyTyped(character, imguiWantsKeyboard);

        if (imguiWantsKeyboard) return;

        GamemodeInstance?.KeyTyped(character);
    }

    private void UpdateControlState()
    {
        var bindings = SettingsMenu.Bindings;

        if (GamemodeInstance != null)
        {
            var control = ClientCar?.Control;

            if (control != null)
            {
                // determine base key states
                bool acceleratePressed = _pressedKeys.Contains(bindings.Accelerate);
                bool brakePressed = _pressedKeys.Contains(bindings.Brake);
                bool turnLeftPressed = _pressedKeys.Contains(bindings.TurnLeft);
                bool turnRightPressed = _pressedKeys.Contains(bindings.TurnRight);
                bool aerialBouncePressed = _pressedKeys.Contains(bindings.AerialBounce);
                bool aerialStrafePressed = _pressedKeys.Contains(bindings.AerialStrafe);
                bool handbrakePressed = _pressedKeys.Contains(bindings.Handbrake);

                // apply Up/Down controls
                control.Up = acceleratePressed || aerialBouncePressed;
                control.Down = brakePressed || aerialBouncePressed;

                if (aerialStrafePressed)
                {

                }

                control.Left = turnLeftPressed || aerialStrafePressed;
                control.Right = turnRightPressed || aerialStrafePressed;
                control.Handb = handbrakePressed;
            }
        }
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);

        // While paused, skip all game control updates.
        if (IsPaused)
            return;

        var bindings = SettingsMenu.Bindings;

        // track released keys
        _pressedKeys.Remove(key);

        // update control state based on remaining pressed keys
        UpdateControlState();

        // handle special cases
        if (GamemodeInstance != null)
        {
            var control = ClientCar?.Control;

            if (control != null)
            {
                if (key == Key.Escape)
                {
                    // this seems to be currently unused
                    control.Exit = false;
                }

                if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
                {
                    control.Lookback = 0;
                }
            }
        }

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

        GamemodeInstance?.Render();
    }

    private static void RenderMessages()
    {
        if (!FrameTrace.IsEnabled) return;

        var y = 0f;
        const float x = 250;
        const float increment = 20;

        G.SetColor(new Color(0, 0, 0));
        G.DrawString(FrameTrace.GetMessageString(), (int)x, (int)y);
    }

    public override void GameTick()
    {
        if (IsPaused && AllowPausing)
            return;

        if (RaceState is RaceState.InProgress or RaceState.Finished)
        {
            GamemodeInstance?.GameTick();
        }

        if (GamemodeInstance != null)
        {
            var car = ClientCar;
            if (car != null)
            {
                switch (currentViewMode)
                {
                    case ViewMode.Follow:
                        PlayerFollowCamera.Follow(
                            Camera,
                            car,
                            (float)car.CarPhysics.Cxz,
                            car.Control.Lookback,
                            (float)car.CarPhysics.Speed,
                            car.Stats.Swits[2]
                        );
                        break;
                    case ViewMode.FollowStatic:
                        PlayerFollowCamera.Follow(
                            Camera,
                            car,
                            (float)car.CarPhysics.StaticCameraXz,
                            car.Control.Lookback,
                            (float)car.CarPhysics.Speed,
                            car.Stats.Swits[2]
                        );
                        break;
                    case ViewMode.Around:
                        PlayerAroundCamera.Around(Camera, car);
                        break;
                }
            }
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

    IClientCarCallbacks IClientCallbacks.GetClientCarCallbacks(int index)
    {
        return GetCarVisual(index).Visuals;
    }

    void IGamemodeData.SendServerEvent(ReadOnlySpan<byte> payload)
    {
        // No-op in singleplayer. The multiplayer phase injects a sender into
        // the gamemode via SetEventSender; a local host replaces this during
        // the single-path rework.
    }

    void IGamemodeData.UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
    {
        // Server-driven player roster updates; wired up with the local host.
    }

}