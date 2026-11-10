using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.PluginSystem;
using NFMWorld.Sfx;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Plugins;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

[PluginVisible(Visible.IncludingAllMembersOfType)]
public abstract class BaseClientGamemode : IGamemode
{
    /// <summary>Gamemode identifier (e.g., "nfmm/pvp").</summary>
    public abstract string GamemodeId { get; }

    [PluginVisible] public ClientGamemodeParameters Parameters { get; }
    internal IGamemodeContext GamemodeContext { get; }

    [PluginVisible] public ObservableUnlimitedArray<ClientSidePlayer> Players { get; }
    [PluginVisible] public BackendStage CurrentStage => GamemodeContext.CurrentStage;
    [PluginVisible] public int NumPlayers => Players.Count;

    /// <summary>
    /// Returns the race results if the race has finished, or null otherwise.
    /// </summary>
    [PluginVisible]
    public RaceResults? Results { get; protected internal set; }

    /// <summary>Per-frame HUD state for the client player.</summary>
    [PluginVisible]
    public HudStateData HudState { get; protected internal set; } = new();

    /// <summary>
    /// The player this client drives. Team modes can field more than one; a spectator drives
    /// none, which is why this is the first client player rather than a hard requirement.
    /// </summary>
    [PluginVisible]
    public ClientSidePlayer? ClientPlayer { get; }

    private readonly IClientCallbacks _clientCallbacks;

    /// <summary>
    /// The player this client drives, or null for a spectator whose roster has none. A team
    /// mode could legitimately hold several, which is why this is the first rather than a
    /// requirement that there be exactly one.
    /// </summary>
    public static ClientSidePlayer? FirstClientPlayer(IReadOnlyList<ClientSidePlayer> players)
    {
        foreach (var player in players)
        {
            if (player.Info.IsClientPlayer)
                return player;
        }

        return null;
    }

    protected BaseClientGamemode(ClientGamemodeParameters clientGamemodeParameters, IGamemodeContext gamemodeContext)
    {
        Parameters = clientGamemodeParameters;
        GamemodeContext = gamemodeContext;
        Players = new ObservableUnlimitedArray<ClientSidePlayer>();
        foreach (var (parameters, idx) in clientGamemodeParameters.Players.Select((p, idx) => (p, idx)))
            Players.Add(new ClientSidePlayer(parameters, idx));
        // A spectator roster has no client player, so this cannot be Single: it was, and a
        // spectator threw on construction before it could watch anything.
        ClientPlayer = FirstClientPlayer(Players.ToArray());
        _clientCallbacks = new PluginClientCallbacks(gamemodeContext.ClientCallbacks);
    }

    /// <summary>
    /// Called when an event is received from the server.
    /// The payload is a MemoryPack-serialized gamemode-specific event.
    /// </summary>
    public virtual void OnServerEvent(ReadOnlySpan<byte> payload)
    {
    }

    /// <summary>
    /// The gamemode's whole state, sent once after this client attached. Replaces rather than
    /// merges: it is a baseline, not a delta.
    /// </summary>
    public virtual void OnServerState(ReadOnlySpan<byte> payload)
    {
    }

    /// <summary>
    /// Called when the race is finished server-side.
    /// </summary>
    public virtual void OnServerRaceFinished(RaceResults results)
    {
    }

    /// <inheritdoc />
    public virtual void SetServerResults(RaceResults results)
    {
        Results = results;
        OnServerRaceFinished(results);
    }

    /// <summary>
    /// Sends a gamemode-specific event to the server using the sender injected
    /// by the host, falling back to <see cref="IGamemodeContext.SendServerEvent"/>.
    /// </summary>
    [PluginVisible]
    protected internal void SendServerEvent(ReadOnlySpan<byte> payload)
    {
        GamemodeContext.SendServerEvent(payload);
    }

    /// <summary>
    /// Sends a gamemode-specific event to the server using the sender injected
    /// by the host, falling back to <see cref="IGamemodeContext.SendServerEvent"/>.
    /// </summary>
    [PluginVisible]
    protected internal void SendServerEvent<T>(T payload)
    {
        SendServerEvent((ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(payload));
    }

    /// <summary>
    /// Called to awake the gamemode.
    /// </summary>
    public virtual void Begin()
    {
    }

    /// <summary>
    /// Called to deinitialize the gamemode.
    /// </summary>
    /// <remarks>
    /// Overrides must call the base implementation, which releases any AI plugins this
    /// gamemode attached - those hold a collectible load context each.
    /// </remarks>
    public virtual void End()
    {
        ReleaseBots();
    }

    /// <summary>
    /// Called every game tick to update the gamemode as long as all players have loaded.
    /// </summary>
    public virtual void GameTick()
    {
    }

    /// <summary>
    /// User-callable reset function that can be used to reset the gamemode state.
    /// </summary>
    /// <remarks>
    /// Releases any AI plugins first: overrides commonly call <see cref="Begin"/>, which
    /// re-attaches a bot per player, and an attach without a release leaks a load context
    /// per reset.
    /// </remarks>
    public virtual void Reset()
    {
        ReleaseBots();
        Results = null;
        GamemodeContext.ClientCallbacks.RaceRestarted();
    }

    /// <summary>
    /// Called when the player leaves the race before finishing. Gamemodes that tell the host
    /// about it override this; the default does nothing.
    /// </summary>
    public virtual void Forfeit()
    {
    }

    /// <summary>
    /// Invoked when a key is pressed.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyPressed(Key key, in Keys keys)
    {
    }

    /// <summary>
    /// Invoked when a key is released.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyReleased(Key key, in Keys keys)
    {
    }

    /// <summary>
    /// Invoked when a key character is typed.
    /// </summary>
    /// <param name="character">The character that was typed.</param>
    public virtual void KeyTyped(char character)
    {
    }

    /// <summary>
    /// Invoked when a mouse button is pressed.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="button">The button that was pressed.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MousePressed(
        int x,
        int y,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
    }

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="button">The button that was released.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseReleased(
        int x,
        int y,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
    }

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="delta">The delta Y change.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    /// <summary>
    /// Invoked when the mouse is moved.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
    }

    public virtual void Render()
    {
    }

    /// <summary>
    /// Rendering callbacks for client cars, such as ghost opacity and
    /// checkpoint glow.
    /// </summary>
    [PluginVisible]
    protected IClientCallbacks ClientCallbacks => _clientCallbacks;

    // One entry per attached AI: its plugin (which owns the load context) and the
    // instances it created, so End() can drop both in the right order.
    private readonly List<(AiPlugin Plugin, List<PluginAi> Instances)> _bots = [];

    /// <summary>
    /// Attaches an AI from <c>data/ais/&lt;aiId&gt;/ai.dll</c> to <paramref name="player"/>,
    /// which drives it from then on via <see cref="PhysicsController.GameTick"/>.
    /// </summary>
    /// <remarks>
    /// The plugin is loaded per gamemode instance and released in <see cref="End"/>, so it
    /// is resident only for this race - never share one between runs. Returns null if no
    /// plugin is present, which leaves the player driven by nothing rather than failing
    /// the race.
    /// </remarks>
    [RequiresUnreferencedCode("Loads a plugin assembly at run time.")]
    [PluginVisible]
    protected PluginAi? AttachBot(string aiId, ClientSidePlayer player)
    {
        var plugin = AiPlugin.Load(aiId, PluginPaths.For("ais", aiId, "ai.dll"));
        if (plugin is null)
        {
            Logging.Warning($"No AI plugin at data/ais/{aiId}/ai.dll; '{player.Info.PlayerName}' will not be driven.");
            return null;
        }

        var instance = plugin.Create(player, this);
        player.Bot = instance;
        _bots.Add((plugin, [instance]));
        return instance;
    }

    /// <summary>
    /// Drops every bot and releases the plugin assemblies that provided them.
    /// </summary>
    private void ReleaseBots()
    {
        if (_bots.Count == 0)
            return;

        // Drop the references the gamemode holds before releasing the assemblies: an
        // instance's type lives in the plugin's context, so anything still pointing at
        // one keeps the whole context resident.
        foreach (var (_, instances) in _bots)
        {
            foreach (var instance in instances)
            {
                foreach (var player in Players)
                {
                    if (ReferenceEquals(player.Bot, instance))
                        player.Bot = null;
                }
            }

            instances.Clear();
        }

        foreach (var (plugin, _) in _bots)
            plugin.Unload();

        _bots.Clear();
    }

    private readonly RaceAnnouncer Announcer = new();

    private int _lastClientCheckpoint = 0;
    private int _lastCountdownTime = 0;
    private fix64 _lcarx;
    private fix64 _lcarz;

    /// <summary>
    /// Convenience function to reset HUD state, visual FX and sounds.
    /// </summary>
    [PluginVisible]
    [ClientOnly]
    protected internal void ClientReset()
    {
        GamemodeContext.ClientCallbacks.ResetCheckpointGlow();

        HudState = new HudStateData { Lap = 1, TotalLaps = CurrentStage.Nlaps };
        Announcer.Reset();
        IBackend.Backend.StopAllSounds();
    }

    /// <summary>
    /// Convenience function to update HUD state and play sounds based on the given car's state.
    /// </summary>
    /// <param name="car">The client car</param>
    [PluginVisible]
    [ClientOnly]
    protected internal void UpdateHudAndSounds(BackendCar car)
    {
        var diffx = (float)(car.Position.X - _lcarx);
        var diffz = (float)(car.Position.Z - _lcarz);
        _lcarx = car.Position.X;
        _lcarz = car.Position.Z;

        HudState.Speed = MathF.Sqrt(diffx * diffx + diffz * diffz);
        HudState.Damage = ClientPlayer?.Car is { } own
            ? (float)car.CarPhysics.DamagePoints / own.Stats.Maxmag
            : 0f;
        HudState.Power = (float)car.CarPhysics.Power / 100f;

        if (car.CurrentCheckpoint != _lastClientCheckpoint)
        {
            _lastClientCheckpoint = car.CurrentCheckpoint;
            SfxLibrary.checkpoint?.Play();
        }

        GamemodeContext.ClientCallbacks.UpdateCheckpointGlow(
            car.CurrentCheckpoint,
            car.CurrentCheckpoint == CurrentStage.Checkpoints.Count - 1 && car.CurrentLap == CurrentStage.Nlaps - 1
        );

        Announcer.Tick(car, this);
        HudState.StatusMessage = Announcer.Status;
        HudState.StuntMessage = Announcer.Stunt;
    }

    /// <summary>
    /// Updates the countdown timer and plays the corresponding sound.
    /// </summary>
    /// <param name="countdownTime">The current countdown time.</param>
    [PluginVisible]
    [ClientOnly]
    protected void UpdateCountdown(int countdownTime)
    {
        if (countdownTime != _lastCountdownTime)
        {
            _lastCountdownTime = countdownTime;
            SfxLibrary.countdown[countdownTime].Play();
        }

        HudState.CountdownTimer = countdownTime;
    }
}
