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
public class PvpServerGamemode(PvpConstraint constraint) : BaseServerGamemode
{
    private readonly Dictionary<Guid, PlayerServerState> _players = new();
    private int _totalLaps;
    private IReadOnlyList<(f64Vector3 Position, int Index)>? _checkpoints;

    private enum InnerRaceState { WaitingToStart, Countdown, InProgress, Finished }
    private InnerRaceState _state = InnerRaceState.WaitingToStart;
    private int _countdownTime = 4;
    private int _innerCountdownTicks;
    private int _finishPositionCounter;

    public override string GamemodeId => constraint switch
    {
        PvpConstraint.Racing => "nfmm/racing",
        PvpConstraint.Wasting => "nfmm/wasting",
        PvpConstraint.Both => "nfmm/both",
        _ => "nfmm/racing"
    };

    // ── Lifecycle ──────────────────────────────────────────────────

    public override void Begin(IServerGamemodeContext context)
    {
        _totalLaps = context.CurrentStage.nlaps;
        _checkpoints = context.CurrentStage.checkpoints
            .Select((cp, i) => (cp.Position, i))
            .ToList();

        _players.Clear();
        foreach (var playerId in context.PlayerIds)
            _players[playerId] = new PlayerServerState();

        _finishPositionCounter = 0;
        _resultsBroadcast = false;
        _state = InnerRaceState.WaitingToStart;
    }

    public override void StartRace()
    {
        _countdownTime = 4;
        _innerCountdownTicks = 0;
        _state = InnerRaceState.Countdown;
    }

    // ── Tick ───────────────────────────────────────────────────────

    public override void GameTick()
    {
        switch (_state)
        {
            case InnerRaceState.WaitingToStart:
                // Not yet started — waiting for all clients to load.
                break;
            case InnerRaceState.Countdown:
                CountdownTick();
                break;
            case InnerRaceState.InProgress:
                // No per-tick logic — events drive state changes.
                break;
            case InnerRaceState.Finished:
                // No per-tick logic — finish was broadcast on transition.
                break;
        }
    }

    private void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
                _state = InnerRaceState.InProgress;
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
        if (_state != InnerRaceState.InProgress) return;
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
                    _state = InnerRaceState.Finished;
                }
            }
        }
    }

    // ── Results ───────────────────────────────────────────────────

    private bool _resultsBroadcast;

    public override GameStateSnapshot? GetStateSnapshot()
    {
        if (_state == InnerRaceState.Finished && !_resultsBroadcast)
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
                Results = results,
                State = new Dictionary<string, object>
                {
                    ["countdownTime"] = _countdownTime,
                    ["raceState"] = _state.ToString()
                }
            };
        }

        return new GameStateSnapshot
        {
            IsFinished = _state == InnerRaceState.Finished,
            State = new Dictionary<string, object>
            {
                ["countdownTime"] = _countdownTime,
                ["raceState"] = _state.ToString()
            }
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
