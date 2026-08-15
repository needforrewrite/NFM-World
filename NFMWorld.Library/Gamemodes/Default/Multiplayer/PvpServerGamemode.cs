using MemoryPack;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Server-side PvP racing gamemode. Validates checkpoint events from clients,
/// tracks authoritative lap counts and standings, and detects race finish.
///
/// Does NOT run physics — it validates discrete events with fuzzy proximity checks.
/// </summary>
public class PvpServerGamemode(PvpConstraint constraint) : BaseServerGamemode()
{
    private readonly Dictionary<Guid, PlayerServerState> _players = new();
    private int _totalLaps;
    private IReadOnlyList<(f64Vector3 Position, int Index)>? _checkpoints;

    private enum ServerGamemodeState { WaitingToStart, Countdown, InProgress, Finished }
    private ServerGamemodeState _state = ServerGamemodeState.WaitingToStart;
    private int _finishPositionCounter;
    
    private ServerCountdown _countdown = new();

    public override string GamemodeId => constraint switch
    {
        PvpConstraint.Racing => "nfmm/racing",
        PvpConstraint.Wasting => "nfmm/wasting",
        PvpConstraint.Both => "nfmm/both",
        _ => "nfmm/racing"
    };

    // ── Lifecycle ──────────────────────────────────────────────────

    public override void Begin(IServerGamemodeData data)
    {
        base.Begin(data);

        _totalLaps = Data.CurrentStage.nlaps;
        _checkpoints = Data.CurrentStage.checkpoints
            .Select((cp, i) => (cp.Position, i))
            .ToList();

        _players.Clear();
        foreach (var playerId in Data.PlayerIds)
            _players[playerId] = new PlayerServerState();

        _finishPositionCounter = 0;
        _resultsBroadcast = false;
        _state = ServerGamemodeState.WaitingToStart;
    }

    public override void StartRace()
    {
        _countdown = new ServerCountdown();
        _countdown.Go += () => _state = ServerGamemodeState.InProgress;
        _state = ServerGamemodeState.Countdown;
    }

    // ── Tick ───────────────────────────────────────────────────────

    public override void GameTick()
    {
        switch (_state)
        {
            case ServerGamemodeState.WaitingToStart:
                // Not yet started — waiting for all clients to load.
                break;
            case ServerGamemodeState.Countdown:
                _countdown.GameTick();
                break;
            case ServerGamemodeState.InProgress:
                // No per-tick logic — events drive state changes.
                break;
            case ServerGamemodeState.Finished:
                // No per-tick logic — finish was broadcast on transition.
                break;
        }
    }

    // ── Client events ──────────────────────────────────────────────

    // Called by RaceOrchestrator to associate a client index with a player ID
    public override void OnClientEvent(Guid playerId, ReadOnlySpan<byte> payload)
    {
        var evt = MemoryPackSerializer.Deserialize<IPvpServerEvent>(payload);
        switch (evt)
        {
            case PvpCheckpointEvent cpEvent:
                HandleCheckpointEvent(playerId, cpEvent);
                break;
        }
    }

    private void HandleCheckpointEvent(Guid playerId, PvpCheckpointEvent evt)
    {
        if (_state != ServerGamemodeState.InProgress) return;
        if (!_players.TryGetValue(playerId, out var ps)) return;

        // ── Validation ──────────────────────────────────────────
        // 1. Lap must be current or next
        if (evt.Lap != ps.CurrentLap && evt.Lap != ps.CurrentLap + 1)
            return;

        // 2. Checkpoint must be the next expected one
        int expectedCp = (ps.CurrentCheckpoint + 1) % (_checkpoints?.Count ?? 1);
        if (evt.CheckpointIndex != expectedCp)
            return;

        // ── Apply ───────────────────────────────────────────────
        ps.CurrentCheckpoint = evt.CheckpointIndex;

        // If we just completed a full loop, advance lap
        if (evt.CheckpointIndex == 0 && evt.Lap == ps.CurrentLap)
        {
            ps.CurrentLap++;
            if (ps.CurrentLap >= _totalLaps)
            {
                ps.Finished = true;
                ps.FinishPosition = _finishPositionCounter++;
                ps.FinishTick = evt.ClientTick;

                if (_finishPositionCounter == 1) // First finisher
                {
                    _state = ServerGamemodeState.Finished;
                }
            }
        }
    }

    // ── Results ───────────────────────────────────────────────────

    private bool _resultsBroadcast;
    
    public override GameStateSnapshot? GetStateSnapshot()
    {
        if (_state == ServerGamemodeState.Finished && !_resultsBroadcast)
        {
            _resultsBroadcast = true;

            var results = new RaceResults
            {
                GamemodeId = GamemodeId,
                RaceDuration = TimeSpan.Zero,
                Standings = _players.Select(kvp => new RaceStanding
                {
                    PlayerId = kvp.Key,
                    FinishPosition = kvp.Value.FinishPosition,
                    FinishTime = null,
                    IsClientPlayer = false
                }).OrderBy(s => s.FinishPosition).ToArray()
            };

            return new GameStateSnapshot
            {
                IsFinished = true,
                Results = results
            };
        }

        return new GameStateSnapshot
        {
            IsFinished = _state == ServerGamemodeState.Finished
        };
    }

    private class PlayerServerState
    {
        public int CurrentLap;
        public int CurrentCheckpoint;
        public bool Finished;
        public int FinishPosition = -1;
        public uint FinishTick;
    }
}
