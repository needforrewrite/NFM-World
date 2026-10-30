using MemoryPack;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Gamemodes.DotNet;

[PluginVisible]
public class PluginServerGamemode(
    ServerGamemodeParameters parameters,
    IServerGamemodeContext context,
    string gamemodeId,
    Dictionary<string, object>? config = null)
    : BaseServerGamemode
{
    internal GameStateSnapshot? _snapshot;

    [PluginVisible] public Dictionary<string, object>? Config { get; } = config;

    public override string GamemodeId { get; } = gamemodeId;

    [PluginVisible] public ServerGamemodeParameters Parameters { get; } = parameters;

    [PluginVisible] public IReadOnlyList<ServerSidePlayerInfo> Players => Parameters.Players;

    /// <summary>The stage being raced on (checkpoints, lap count, geometry).</summary>
    [PluginVisible] public BackendStage CurrentStage => context.CurrentStage;

    public override void Begin()
    {
        base.Begin();
        _snapshot = null;
    }

    /// <summary>
    /// Gets the latest relayed position for a player, or null if not yet received.
    /// Position data flows from <see cref="C2S_PlayerState"/> relay.
    /// </summary>
    [PluginVisible]
    public f64Vector3? GetPlayerPosition(Guid playerId) => context.GetPlayerPosition(playerId);

    /// <summary>
    /// Broadcast a payload to all clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    [PluginVisible]
    public void BroadcastEvent(ReadOnlyMemory<byte> payload) => context.BroadcastEvent(payload);

    /// <summary>
    /// Broadcast a payload to all clients.
    /// </summary>
    /// <param name="payload">The payload.</param>
    [PluginVisible]
    public void BroadcastEvent<T>(T payload) => BroadcastEvent((ReadOnlyMemory<byte>)MemoryPackSerializer.Serialize(payload));

    public override GameStateSnapshot? GetStateSnapshot() => _snapshot;

    [PluginVisible]
    public void FinishRace(TimeSpan raceDuration, RaceStanding[] standings) => _snapshot = new GameStateSnapshot()
    {
        IsFinished = true,
        Results = new RaceResults()
        {
            GamemodeId = GamemodeId,
            RaceDuration = raceDuration,
            Standings = standings
        }
    };
}