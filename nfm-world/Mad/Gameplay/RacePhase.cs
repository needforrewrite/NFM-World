using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Audio;
using NFMWorld.Gameplay.RaceHost;
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
using NFMWorldLibrary.Helpers;
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
    public override Node BuildUi() => RaceHudView.Build(
        new RaceHudView.Actions(
            Resume: ResumeRace,
            Restart: _host is LocalRaceHost ? RestartRace : null,
            Quit: QuitRace
        )
    );

    public readonly BaseGamemodeFactory Gamemode;
    public readonly IReadOnlyList<ClientSidePlayerInfo> Players;

    private readonly IRaceHost _host;
    private uint _ticks;
    private readonly UnlimitedArray<uint> _lastTick = [];

    private readonly RaceInputController _input = new();
    private readonly RaceCameraDirector _cameraDirector = new();
    private readonly SettingsCameraPreview _settingsCamera = new();

    /// <summary>This client's player id in the match, so it can tell which bots it simulates.</summary>
    private readonly Guid _localPlayerId;

    public IGamemode? GamemodeInstance { get; protected set; }

    BackendStage IGamemodeContext.CurrentStage => CurrentStage.Backend;
    bool IGamemodeContext.IsMultiplayer => _host is NetworkRaceHost;

    /// <summary>
    /// Watching a race this client has no car in. There is no client player, so input drives
    /// nothing and the camera rides the watched car.
    /// </summary>
    private readonly bool _spectating;

    /// <summary>Player index a spectator is following, or -1 to follow the leader.</summary>
    private int _spectateLock = -1;

    /// <summary>
    /// When an online race started waiting for the other players to load, or null if it never
    /// had to. A client that never reaches the Game Master gets no failure packet from anyone -
    /// the GM only knows connections it received - so this screen has to time out here too.
    /// </summary>
    private long? _waitingSince;

    /// <summary>How long a race waits to start before it gives up. Matches the server's own deadline.</summary>
    private const int LoadTimeoutMs = 20_000;

    public RacePhase(
        string stageName,
        BaseGamemodeFactory gamemode,
        IReadOnlyList<ClientSidePlayerInfo> players,
        IRaceHost host,
        bool spectating = false,
        BaseStageRenderingPhase? stageSource = null
    ) : base(stageName, stageSource)
    {
        Gamemode = gamemode;
        Players = players;
        _host = host;
        _spectating = spectating;
        _localPlayerId = players.FirstOrDefault(p => p.IsClientPlayer)?.Id ?? Guid.Empty;

        try
        {
            LoadStageMusic();
            InitializeRace(host);
        }
        catch
        {
            _host.Dispose();
            CurrentStage?.SetPlayers(null);
            GamemodeInstance?.End();
            GamemodeInstance = null;
            if (stageSource != null)
                stageSource.TakeStageFrom(this);
            else
                base.Dispose(true);
            Gamemode.End();
            throw;
        }
    }

    private void InitializeRace(IRaceHost host)
    {
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
        // Only an online race can wait on other people; a local one starts below.
        _waitingSince = host is LocalRaceHost ? null : System.Environment.TickCount64;
        host.RaceCanStart += () => RaceState = RaceState.InProgress;
        host.RaceFailedToStart += () => RaceState = RaceState.FailedToStart;
        host.PlayerStateReceived += ApplyPlayerState;
        host.ServerEventReceived += payload => GamemodeInstance?.OnServerEvent(payload.Span);
        host.ServerStateReceived += payload => GamemodeInstance?.OnServerState(payload.Span);
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
    /// The local client player's car, or null if the gamemode hasn't assigned one yet - or if
    /// there is no client player at all, which is every spectator.
    /// </summary>
    public BackendCar? ClientCar => (GamemodeInstance as BaseClientGamemode)?.ClientPlayer?.Car;

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
        UiState.Race.PresentationRevision.Value++;
    }

    private void UpdateHudPresentation(BaseClientGamemode gm)
    {
        var hud = UiState.Race;
        hud.IsPreview = NFMWorld.UI.Components.SettingsView.HudPreviewActive;
        hud.Skin.Value = SettingsMenu.HudSkin;
        hud.Wasted = 0;
        hud.Opponents = 0;
        hud.Racers.Clear();
        hud.Markers.Clear();
        hud.ClientPosition = null;
        hud.ClientHeading = 0;
        hud.TargetCheckpoint = null;
        hud.Checkpoints.Clear();
        foreach (var checkpoint in CurrentStage.Backend.Checkpoints)
            hud.Checkpoints.Add(((float)checkpoint.Position.X, (float)checkpoint.Position.Z));
        hud.TrackCenter = default;
        hud.ViewProjection = Camera.ViewProjectionMatrix;
        if (CurrentStage is { Minimap.IsBaked: true } stage)
        {
            hud.MapImage = stage.Minimap.Image;
            hud.MapFrame = (stage.Minimap.Bounds.MinX, stage.Minimap.Bounds.MaxZ, stage.Minimap.UnitsPerPixel);
        }
        else
        {
            hud.MapImage = null;
            hud.MapFrame = default;
        }

        hud.ArrowAngle = null;
        hud.ArrowTarget = "";
        hud.ArrowAtCars = false;
        hud.ArrowLocked = false;
        // A spectator has no own car, so it names whoever it is watching. The digit on that
        // racer's row is the key that switches away from them.
        hud.Spectating = _spectating;
        if (_spectating && SpectateCar() is { } watched)
            hud.ArrowTarget = OrderedPlayers(gm).FirstOrDefault(p => ReferenceEquals(p.Car, watched))?.Info.PlayerName ?? "";
        hud.TimeTrial = gm.GamemodeId == "nfmm/timetrial";
        var client = ClientCar;
        InvalidateOrdering();
        // One ordering for the sidebar and the lock keys, so the digit printed on a row is the
        // slot that locks that row's car. Every car is listed here (wasted ones included) so the
        // two never disagree about which index a car occupies.
        var lockSlot = 0;
        foreach (var player in OrderedPlayers(gm))
        {
            var car = player.Car!;
            var damage = car.Stats.Maxmag > 0 ? (float)car.CarPhysics.DamagePoints / car.Stats.Maxmag : 0;
            // Opponents are numbered in listing order; that is what the lock keys address.
            var slot = player.Info.IsClientPlayer ? 0 : ++lockSlot;
            // The player's name, not the car's: every car in a race can be the same model.
            var name = player.Info.PlayerName;
            hud.Racers.Add(
                new HudRacer(
                    car.Placement + 1,
                    name,
                    damage,
                    player.Info.IsClientPlayer,
                    car.Wasted,
                    slot
                )
            );
            hud.Markers.Add(
                new HudRacerMarker(
                    name,
                    (float)car.Position.X,
                    (float)car.Position.Y,
                    (float)car.Position.Z,
                    car.Placement + 1,
                    player.Info.IsClientPlayer,
                    car.Wasted
                )
            );
            if (player.Info.IsClientPlayer) continue;
            hud.Opponents++;
            if (car.Wasted) hud.Wasted++;
        }

        hud.Racers.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        var targetCar = client is null ? null : TargetCarFor(client);
        if (client is not null)
        {
            hud.ClientPosition = ((float)client.Position.X, (float)client.Position.Y, (float)client.Position.Z);
            hud.ClientHeading = (float)client.Rotation.Xz.Degrees * MathF.PI / 180f;
            // Centre the minimap on the average checkpoint, so stages whose pieces sit away
            // from the declared bounds still map sensibly. Falls back to the stage bounds.
            var checkpoints = CurrentStage.Backend.Checkpoints;
            hud.TrackCenter = checkpoints.Count > 0 ? AverageCheckpoint(checkpoints) : StageCenter();
            if (checkpoints.Count > 0)
            {
                var next = checkpoints[Math.Clamp((int)client.CurrentCheckpoint, 0, checkpoints.Count - 1)];
                hud.TargetCheckpoint = ((float)next.Position.X, (float)next.Position.Z);
            }
        }
        else if (_spectating && SpectateCar() is { } followed)
        {
            // No car of our own, so the radar follows the one being watched; the client
            // position stays null, which is what keeps the other overlays off.
            hud.TrackCenter = ((float)followed.Position.X, (float)followed.Position.Z);
        }

        // If the client is out, or there is no car left to chase, car targeting ends. Losing the
        // locked car alone is not enough - TargetCarFor then falls back to the nearest opponent,
        // and toggling the arrow off/on should start from that fallback rather than a dead lock.
        if (client is not null && (targetCar is null || client.Wasted))
            client.Control.Arrace = false;
        if (client is not null
            && client.Control.ArrowLock >= 0
            && !OrderedOpponents(gm).Any(p => p.Index == client.Control.ArrowLock && p.Car is { Wasted: false }))
            client.Control.ArrowLock = -1;
        // Wasted: the camera rides whoever is targeted, so the HUD names them even though the
        // arrow itself is off (a dead car has no heading to point it from). A spectator's name
        // was set above and must survive this.
        if (!_spectating)
            hud.Spectating = client?.Wasted ?? false;
        hud.ArrowAtCars = client?.Control.Arrace ?? false;
        hud.ArrowLocked = hud.ArrowAtCars && (client?.Control.ArrowLock ?? -1) >= 0;
        if (client is not null && (hud.ArrowAtCars || hud.Spectating))
        {
            hud.ArrowTarget = targetCar is null
                ? ""
                : OrderedOpponents(gm).FirstOrDefault(p => ReferenceEquals(p.Car, targetCar))?.Info.PlayerName
                  ?? "";
        }

        if (ArrowTarget(client, targetCar) is not { } target) return;

        var direction = target - new Vector3((float)client.Position.X, (float)client.Position.Y, (float)client.Position.Z);
        var forward = Camera.LookAt - Camera.Position;
        if (direction.X * direction.X + direction.Z * direction.Z < 1
            || forward.X * forward.X + forward.Z * forward.Z < 1) return;
        var angle = MathF.Atan2(
                        forward.Z * direction.X - forward.X * direction.Z,
                        forward.X * direction.X + forward.Z * direction.Z
                    )
                    * (180 / MathF.PI);
        hud.ArrowAngle = Math.Clamp(angle, hud.ArrowAtCars ? -100 : -130, hud.ArrowAtCars ? 100 : 130);
    }

    /// <summary>
    /// What the guidance arrow points at: the targeted car in arrace mode, else the next
    /// checkpoint. Null when there is nothing to aim at, or the stage turned the arrow off with
    /// <c>noarrow()</c>. Shared with the car camera so it cannot aim somewhere the arrow doesn't.
    /// </summary>
    private Vector3? ArrowTarget(BackendCar? client, BackendCar? targetCar)
    {
        if (client is null || client.Wasted) return null;
        if (CurrentStage.Backend.Physics.NoArrow) return null;
        if (client.Control.Arrace)
            return targetCar is null
                ? null
                : new Vector3((float)targetCar.Position.X, (float)targetCar.Position.Y, (float)targetCar.Position.Z);
        var checkpoints = CurrentStage.Backend.Checkpoints;
        if (checkpoints.Count == 0) return null;
        var checkpoint = checkpoints[Math.Clamp((int)client.CurrentCheckpoint, 0, checkpoints.Count - 1)];
        return new Vector3((float)checkpoint.Position.X, (float)checkpoint.Position.Y, (float)checkpoint.Position.Z);
    }

    // Both orderings are rebuilt at most once per tick: the HUD push and the camera tick each ask
    // for them, and sorting the roster several times a tick is wasted work.
    private ClientSidePlayer[]? _orderedPlayers;
    private ClientSidePlayer[]? _orderedOpponents;

    private void InvalidateOrdering()
    {
        _orderedPlayers = null;
        _orderedOpponents = null;
    }

    /// <summary>
    /// Players with a car, in the one order the sidebar and the lock keys agree on: the client
    /// first, then opponents by race position, ties broken by roster index. The tie-break matters
    /// because every car has <c>Placement = 0</c> before the first checkpoint, and a stable sort
    /// over an unordered roster would otherwise pick a different order each frame.
    /// </summary>
    private ClientSidePlayer[] OrderedPlayers(BaseClientGamemode gm)
        => _orderedPlayers ??= gm.Players
            .Where(p => !p.IsFake && p.Car is not null)
            .OrderByDescending(p => p.Info.IsClientPlayer)
            .ThenBy(p => p.Car!.Placement)
            .ThenBy(p => p.Index)
            .ToArray();

    /// <summary>The opponents in sidebar order: everyone in <see cref="OrderedPlayers"/> but the client.</summary>
    private ClientSidePlayer[] OrderedOpponents(BaseClientGamemode gm)
        => _orderedOpponents ??= OrderedPlayers(gm).Where(p => !p.Info.IsClientPlayer).ToArray();

    /// <summary>
    /// The car the arrow targets: the car locked by <see cref="Control.ArrowLock"/> when it is
    /// still a live opponent, otherwise the nearest onscreen opponent, falling back to the nearest
    /// at all. Called by both the HUD and the car camera, so they always agree on who is chased.
    /// </summary>
    private BackendCar? TargetCarFor(BackendCar client)
    {
        if (GamemodeInstance is not BaseClientGamemode gm) return null;
        var opponents = OrderedOpponents(gm);
        if (opponents.Length == 0) return null;

        // ArrowLock is a player index, so other cars dying cannot shift it onto a different car.
        if (LockTargetHelper.LockedCar(opponents, client.Control.ArrowLock) is { } locked) return locked;

        BackendCar? best = null;
        var nearest = double.PositiveInfinity;
        var bestOnscreen = false;
        foreach (var player in opponents)
        {
            if (player.Car is not { Wasted: false } car) continue;
            var dx = (double)(car.Position.X - client.Position.X);
            var dz = (double)(car.Position.Z - client.Position.Z);
            var distance = dx * dx + dz * dz;
            var clip = Vector4.Transform(
                new Vector4(
                    (float)car.Position.X,
                    (float)car.Position.Y,
                    (float)car.Position.Z,
                    1
                ),
                Camera.ViewProjectionMatrix
            );
            var onscreen = clip.W > 0 && MathF.Abs(clip.X) <= clip.W && MathF.Abs(clip.Y) <= clip.W;
            if ((bestOnscreen && !onscreen) || (onscreen == bestOnscreen && distance >= nearest)) continue;
            nearest = distance;
            best = car;
            bestOnscreen = onscreen;
        }

        return best;
    }

    /// <summary>Opponent count in sidebar order, the number of lockable slots.</summary>
    private int LockableOpponents()
        => GamemodeInstance is BaseClientGamemode gm ? OrderedOpponents(gm).Length : 0;

    /// <summary>Sidebar slot → player index, for whichever list is being locked.</summary>
    private int LockTargetAt(int slot)
        => _spectating ? SpectateTargetAt(slot) : OpponentTargetAt(slot);

    private int SpectateTargetAt(int slot)
    {
        if (GamemodeInstance is not BaseClientGamemode gm) return -1;
        var players = OrderedPlayers(gm);
        return slot >= 0 && slot < players.Length ? players[slot].Index : -1;
    }

    /// <summary>
    /// The car a spectator follows. The lock keys pick it (the same Ctrl+digit a racer uses);
    /// with nothing locked it is whoever is leading, so the view is never empty. It follows the
    /// car through a death too - a spectator is watching the racer, not a live car.
    /// </summary>
    private BackendCar? SpectateCar()
    {
        if (GamemodeInstance is not BaseClientGamemode gm) return null;
        var players = OrderedPlayers(gm);
        if (players.Length == 0) return null;
        foreach (var player in players)
        {
            if (player.Index == _spectateLock && player.Car is { } locked)
                return locked;
        }

        // OrderedPlayers puts the client first, then by placement; with no client, first is the leader.
        foreach (var player in players)
        {
            if (player.Car is { } car)
                return car;
        }

        return null;
    }

    /// <summary>
    /// Resolves a lock slot (0-based, sidebar order) to that opponent's stable player index, or -1
    /// when the slot holds no live opponent. The digit prints from <see cref="HudRacer.LockSlot"/>
    /// using the same ordering, so a key always locks the car whose row shows it.
    /// </summary>
    private int OpponentTargetAt(int slot)
    {
        if (GamemodeInstance is not BaseClientGamemode gm) return -1;
        var opponents = OrderedOpponents(gm);
        return slot >= 0 && slot < opponents.Length && opponents[slot].Car is { Wasted: false }
            ? opponents[slot].Index
            : -1;
    }

    private (float X, float Z) StageCenter()
    {
        var stage = CurrentStage.Backend;
        return ((float)stage.Sx + (float)stage.Ncx / 2, (float)stage.Sz + (float)stage.Ncz / 2);
    }

    private static (float X, float Z) AverageCheckpoint(IReadOnlyList<StageObject> checkpoints)
    {
        var x = 0f;
        var z = 0f;
        foreach (var checkpoint in checkpoints)
        {
            x += (float)checkpoint.Position.X;
            z += (float)checkpoint.Position.Z;
        }

        return (x / checkpoints.Count, z / checkpoints.Count);
    }

    // ── Pause / Resume / Quit ─────────────────────────────────────

    /// <summary>
    /// Show the pause menu. Online this only stops this client driving - the race keeps
    /// running for everyone else, so <see cref="RaceState"/> is left alone.
    /// </summary>
    private void PauseRace()
    {
        if (IsPaused) return;
        _input.Reset(ClientCar?.Control);
        IsPaused = true;
        SoundEffects.SetPaused(true);

        // The pause menu reads the same signals the HUD does, so one write is enough.
        if (GamemodeInstance is BaseClientGamemode gm)
            UiState.Race.Update(gm.HudState, true);
        else
            UiState.Race.Paused.Value = true;
    }

    /// <summary>
    /// Hide the pause menu.
    /// </summary>
    private void ResumeRace()
    {
        if (!IsPaused) return;
        IsPaused = false;
        SoundEffects.SetPaused(false);
        UiState.Race.Paused.Value = false;
    }

    private void RestartRace()
    {
        if (_host is not LocalRaceHost) return;
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

        // Online, leaving mid-race is a forfeit: the Game Master keeps the race going for
        // everyone else, and the transport is dropped by Dispose as if the game had closed.
        // A spectator is racing nothing, so it has nothing to forfeit - and a forfeit packet
        // from it would be read as one of the players walking out.
        if (!_spectating && _host is NetworkRaceHost && RaceState == RaceState.InProgress)
            GamemodeInstance?.Forfeit();

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
            MenuHistory.Remember(stage: StageName);
    }

    protected IGamemode ReloadGamemode()
    {
        return Gamemode.CreateGameMode(
            new ClientGamemodeParameters
            {
                Players = Players
            },
            this
        );
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
            // FailedToStart is always escapable: it is the only way out of a race whose Game
            // Master went away, and the pause menu's Quit is outside the host.
            else if (RaceState is RaceState.FailedToStart or RaceState.InProgress or RaceState.Finished)
                PauseRace();

            return;
        }

        // ── While paused, the UI owns input ────────────────────────
        if (IsPaused)
            return;

        if (key == SettingsMenu.Bindings.CycleView)
        {
            _cameraDirector.CycleViewMode();
            _input.SetFreelook(_cameraDirector.CurrentViewMode == RaceCameraDirector.ViewMode.Freelook, ClientCar?.Control);
        }

        // A spectator has no car and so no Control to write the lock into; the key drives the
        // spectate target instead. Everyone else locks through the normal arrow path.
        if (_spectating)
        {
            var slots = SettingsMenu.Bindings.LockSlots;
            for (var slot = 0; slot < slots.Length; slot++)
            {
                if (key != slots[slot]) continue;
                var target = SpectateTargetAt(slot);
                if (target < 0) break;
                // The same key again releases the lock, falling back to the leader.
                _spectateLock = _spectateLock == target ? -1 : target;
                break;
            }

            GamemodeInstance?.KeyPressed(key, in keys);
            return;
        }

        // The arrow can only target cars while there is one to target.
        var lockable = LockableOpponents();
        _input.KeyPressed(key, ClientCar?.Control, lockable > 0, LockTargetAt);

        GamemodeInstance?.KeyPressed(key, in keys);
    }

    public override void KeyTyped(char character, bool imguiWantsKeyboard)
    {
        base.KeyTyped(character, imguiWantsKeyboard);

        if (imguiWantsKeyboard || IsPaused) return;

        GamemodeInstance?.KeyTyped(character);
    }

    public override void KeyHeld(bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyHeld(imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard || IsPaused || GameSparker.devConsole.IsOpen()) return;

        _input.Refresh(in keys, ClientCar?.Control, ClientCar?.CarPhysics.Mtouch == false);
    }

    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyReleased(key, imguiWantsKeyboard, keys);

        _input.KeyReleased(key, ClientCar?.Control);

        if (IsPaused) return;

        GamemodeInstance?.KeyReleased(key, keys);
    }

    public override void MousePressed(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MousePressed(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseReleased(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MouseReleased(x, y, button, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseScrolled(
        int x,
        int y,
        int delta,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        if (IsPaused) return;

        GamemodeInstance?.MouseScrolled(x, y, delta, buttons, ctrlKey, shiftKey, altKey);
    }

    public override void MouseMoved(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
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
            G.DrawStringOutlinedAligned(
                "Waiting for other players to load...",
                0,
                150,
                (int)G.Viewport.X,
                (int)G.Viewport.Y,
                new Color(0, 0, 0),
                1f,
                TextHorizontalAlignment.Center
            );
        }
        else if (RaceState == RaceState.FailedToStart)
        {
            // Nothing else says anything when the Game Master is unreachable, so the player
            // was left on a dead screen. The pause menu's Quit is the way out.
            G.SetFont(new Font(FontFamily.NotoSans, FontStyle.Plain, 26));
            G.SetColor(new Color(255, 180, 180));
            G.DrawStringOutlinedAligned(
                "Lost connection to the Game Master.",
                0,
                130,
                (int)G.Viewport.X,
                (int)G.Viewport.Y,
                new Color(0, 0, 0),
                1f,
                TextHorizontalAlignment.Center
            );
            G.SetFont(new Font(FontFamily.NotoSans, FontStyle.Plain, 16));
            G.SetColor(new Color(230, 230, 230));
            G.DrawStringOutlinedAligned(
                "Press Escape to quit the race.",
                0,
                170,
                (int)G.Viewport.X,
                (int)G.Viewport.Y,
                new Color(0, 0, 0),
                1f,
                TextHorizontalAlignment.Center
            );
        }

        GamemodeInstance?.Render();

        (G3D as ImmediateBatch3D)?.Flush(cb, Camera);

        // The minimap bake is a separate scene pass, so it goes after the main one and restores
        // the backbuffer itself. One bake per stage; the HUD picks the image up next tick.
        if (SettingsMenu.HudRadar && CurrentStage.Minimap is { IsAttempted: false } minimap)
            minimap.Bake(cb, GameSparker.Game.Window.Width, GameSparker.Game.Window.Height);
    }

    private void ApplyPlayerState(int carIndex, PlayerState state)
    {
        if (GamemodeInstance is not BaseClientGamemode gm)
            return;

        // A car this client simulates is authoritative here; relaying it back would fight
        // the local physics with its own history.
        if (IsLocallySimulated(gm.Players[carIndex]))
            return;

        if (state.Ticks <= _lastTick[carIndex])
            return;

        _lastTick[carIndex] = state.Ticks;

        if (gm.Players[carIndex].Car is { } car)
            PlayerState.ApplyTo(state, car);
    }

    /// <summary>True for the client's own car and any bot it fielded.</summary>
    private bool IsLocallySimulated(ClientSidePlayer player)
        => player.Info.IsClientPlayer
           || player.Info.IsBot && player.Info.OwnerId == _localPlayerId;

    public override void GameTick()
    {
        _settingsCamera.RestoreNormal(Camera);
        // Only a local race actually stops when paused. Online the world keeps ticking: the
        // pause menu just means this client has stopped driving.
        if (IsPaused && _host is LocalRaceHost)
        {
            InvalidateOrdering();
            if (ClientCar is { } pausedCar
                && _cameraDirector.CurrentViewMode is RaceCameraDirector.ViewMode.Follow
                    or RaceCameraDirector.ViewMode.FollowStatic
                    or RaceCameraDirector.ViewMode.CarCam)
            {
                _cameraDirector.ArrowTarget = ArrowTarget(pausedCar, TargetCarFor(pausedCar));
                _cameraDirector.Update(Camera, pausedCar);
            }

            if (ClientCar is { } previewCar)
                _settingsCamera.Update(Camera, previewCar, NFMWorld.UI.Components.SettingsView.CameraPreviewActive);

            return;
        }

        _host.Update();

        // The Game Master is gone and reconnection gave up. WaitingToStart used to sit on the
        // "waiting for other players" screen forever in this case.
        if (!_host.IsConnected
            && _host is NetworkRaceHost { ReconnectFailed: true }
            && RaceState is RaceState.WaitingToStart or RaceState.InProgress)
        {
            RaceState = RaceState.FailedToStart;
            return;
        }

        // Still waiting on other players, and the deadline has passed. The GM abandons the race
        // from its side, but a client that never reached the GM hears nothing, so this is what
        // stops "waiting for other players to load" from being permanent.
        if (RaceState == RaceState.WaitingToStart
            && _waitingSince is { } waitingSince
            && System.Environment.TickCount64 - waitingSince >= LoadTimeoutMs)
        {
            RaceState = RaceState.FailedToStart;
            return;
        }

        _settingsCamera.Reset();
        // Positions and placement just changed; the camera below reads them first.
        InvalidateOrdering();

        if (RaceState is RaceState.InProgress or RaceState.Finished)
        {
            GamemodeInstance?.GameTick();
        }

        if (_spectating)
        {
            // Watching a race with no car of our own: the camera rides the chosen car. A race
            // with no cars yet (still loading) leaves the camera where it is.
            _cameraDirector.Navigation = _input.Navigation;
            if (SpectateCar() is { } watched)
                _cameraDirector.Update(Camera, watched);
        }
        else if (ClientCar is { } car)
        {
            // Per tick, not per UI push: the camera runs here, before PushUiState, so the target
            // is resolved fresh rather than read from the HUD a frame late.
            _cameraDirector.ArrowTarget = ArrowTarget(car, TargetCarFor(car));
            _cameraDirector.Navigation = _input.Navigation;
            // A wasted car is spectated, not followed: the lock keys (and the nearest-live
            // fallback in TargetCarFor) choose who the camera rides. A revive clears Wasted, so
            // the camera comes back to the client without any further action.
            _cameraDirector.Update(Camera, car.Wasted ? TargetCarFor(car) ?? car : car);
        }

        if (RaceState == RaceState.InProgress)
        {
            // The cars this client simulates: its own, and any bot it fielded. Each reports
            // under its own player id, because the sender alone does not say which car it is.
            var tick = _ticks++;
            if (ClientCar is { } myCar)
                _host.SendPlayerState(PlayerState.CreateFrom(tick, myCar));

            // Only online: singleplayer's local host invents ids of its own and needs no relay.
            // A spectator owns nothing; with no client id, an ownerless bot would look like one
            // it fielded, and relaying it would fight the client that really drives it.
            if (!_spectating && GamemodeInstance is BaseClientGamemode gm && _host is NetworkRaceHost)
            {
                foreach (var player in gm.Players)
                {
                    if (!player.Info.IsBot || player.Car is not { } botCar)
                        continue;
                    // An empty owner is a bot this build drives itself (singleplayer roster
                    // reused online); treat it as ours rather than leaving it undriven.
                    if (player.Info.OwnerId != Guid.Empty && player.Info.OwnerId != _localPlayerId)
                        continue;
                    _host.SendPlayerState(PlayerState.CreateFrom(tick, botCar), player.Info.Id);
                }
            }
        }

        base.GameTick();
    }

    void IClientCallbacks.ResetCheckpointGlow()
    {
        CurrentStage.Renderer.ResetCheckpointGlow();
    }

    void IClientCallbacks.TimeTrialCompleted(TimeTrialCompletion completion) =>
        TimeTrialUploader.Enqueue(completion);

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
