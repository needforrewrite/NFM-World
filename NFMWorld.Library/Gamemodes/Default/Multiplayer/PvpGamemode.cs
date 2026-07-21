using System.Diagnostics;
using Maxine.Extensions;
using MemoryPack;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Client-side PvP racing gamemode. Runs full physics locally, sends
/// checkpoint events to the server, and receives authoritative standings.
/// Removes all <see cref="ClientServer.RunIfOnClient"/> gating — this
/// class is the client-only gamemode.
/// </summary>
public class PvpClientGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData, PvpConstraint constraint)
    : BaseGamemode(gamemodeParameters, gamemodeData)
{
    protected enum InnerRaceState
    {
        Countdown,
        InProgress,
        Finished
    }

    protected int _countdownTime = 3;
    private int _innerCountdownTicks = 0;
    protected InnerRaceState _currentState = InnerRaceState.Countdown;

    protected Stopwatch raceTimer = new Stopwatch();

    private int _newTick = 0;

    // ── Client-only fields (formerly [ClientOnly]) ────────────────
    private int _playerCarIndex;
    private int _lastSentCheckpoint = -1;
    private int _lastSentLap = 0;
    private uint _clientTicks;

    // ── Server-driven standings ───────────────────────────────────
    private RaceResults? _serverResults;

    // ── Lifecycle ─────────────────────────────────────────────────

    public override void Begin()
    {
        Reset();
    }

    public override void End()
    {
    }

    public override void Reset()
    {
        base.Reset();

        _countdownTime = 4;
        _innerCountdownTicks = 0;
        _lastSentCheckpoint = -1;
        _lastSentLap = 0;
        _serverResults = null;
        raceTimer.Reset();

        CarsInRace.Clear();

        foreach (var (idx, player) in Players.WithIndex())
        {
            CarsInRace[idx] = new BackendCar(player, idx, -500 + (400 * idx), 0);
            CarsInRace[idx].CurrentCheckpoint = 0;
            CarsInRace[idx].CurrentLap = 0;
            if (player.IsBot)
            {
                CarsInRace[idx].Bot = new ElStupido(this, GamemodeData);
            }
        }

        _currentState = InnerRaceState.Countdown;

        // Client-side reset (formerly ClientReset)
        _playerCarIndex = Players.FindIndex(p => p.IsClientPlayer);
        if (_playerCarIndex == -1)
        {
            Logging.Warning("Client player not found in players list, defaulting to index 0");
            _playerCarIndex = 0;
        }

        GamemodeData.ClientCallbacks.ResetCheckpointGlow();
        HudState = new HudStateData { Lap = 1, TotalLaps = CurrentStage.nlaps };
        IBackend.Backend.StopAllSounds();
    }

    // ── Main tick ─────────────────────────────────────────────────

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {CarsInRace[_playerCarIndex].Position.X:0.00}, contoz: {CarsInRace[_playerCarIndex].Position.Z:0.00}, contoy: {CarsInRace[_playerCarIndex].Position.Y:0.00}");

        if (GamemodeData.RaceState != RaceState.InProgress)
        {
            return;
        }

        switch (_currentState)
        {
            case InnerRaceState.Countdown:
                CountdownTick();
                break;
            case InnerRaceState.InProgress:
                InRace();
                break;
            case InnerRaceState.Finished:
                Finished();
                break;
        }
    }

    // ── Countdown ─────────────────────────────────────────────────

    protected virtual void CountdownTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                _currentState = InnerRaceState.InProgress;
                raceTimer.Start();
            }
        }

        UpdateCountdown(_countdownTime);
    }

    // ── In-race ───────────────────────────────────────────────────

    protected virtual void InRace()
    {
        for (var i = 0; i < CarsInRace.Count; i++)
        {
            var inGameCar = CarsInRace[i];
            if (inGameCar.Bot is { } bot)
            {
                bot.RunAi(inGameCar, i);
            }
        }

        // Inter-car collision at original tickrate (21.4 TPS)
        if (++_newTick == Physics.OriginalTicksPerNewTick)
        {
            for (int i = 0; i < CarsInRace.Count; i++)
            for (int j = 0; j < CarsInRace.Count; j++)
            {
                if (i != j)
                {
                    CarsInRace[i].Collide(CarsInRace[j]);
                }
            }

            _newTick = 0;
        }

        foreach (var inGameCar in CarsInRace)
        {
            inGameCar.Drive(CurrentStage);
        }

        if (CurrentStage.checkpoints.Count == 0)
        {
            return;
        }

        for (var i = 0; i < CarsInRace.Count; i++)
        {
            FixHoopHelper.HandleFixHoops(CurrentStage, CarsInRace[i]);
            CheckPointHelper.HandleCheckPoint(CurrentStage, CarsInRace[i]);
        }

        CheckPointHelper.CalculatePositions(CurrentStage, CarsInRace);

        // ── Local race finish detection (client-side fallback) ────
        for (var i = 0; i < CarsInRace.Count; i++)
        {
            if (CarsInRace[i].CurrentLap >= CurrentStage.nlaps)
            {
                _currentState = InnerRaceState.Finished;
                raceTimer.Stop();
            }
        }

        // ── Send checkpoint events to server ──────────────────────
        var myCar = CarsInRace[_playerCarIndex];
        if (myCar.CurrentCheckpoint != _lastSentCheckpoint ||
            myCar.CurrentLap != _lastSentLap)
        {
            var evt = new PvpCheckpointEvent
            {
                CheckpointIndex = myCar.CurrentCheckpoint,
                Lap = myCar.CurrentLap,
                ClientTick = _clientTicks
            };
            var payload = MemoryPackSerializer.Serialize<IPvpServerEvent>(evt);
            SendToServer(payload);

            _lastSentCheckpoint = myCar.CurrentCheckpoint;
            _lastSentLap = myCar.CurrentLap;
        }
        _clientTicks++;

        // ── HUD and sounds ────────────────────────────────────────
        UpdateHudAndSounds(myCar);
    }

    // ── Finished ──────────────────────────────────────────────────

    private void Finished()
    {
        foreach (var inGameCar in CarsInRace)
        {
            inGameCar.CarPhysics.Halted = true;
            inGameCar.Drive(GamemodeData.CurrentStage);
        }
    }

    // ── Server events ─────────────────────────────────────────────

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        // Handle gamemode-specific server events (e.g., standings updates).
    }

    public override void SetServerResults(RaceResults results)
    {
        _serverResults = results;
        _currentState = InnerRaceState.Finished;
        raceTimer.Stop();
    }

    // ── Results ───────────────────────────────────────────────────

    public override RaceResults? GetResults()
    {
        if (_serverResults != null)
            return _serverResults;

        if (_currentState != InnerRaceState.Finished)
            return null;

        return new RaceResults
        {
            GamemodeId = constraint switch
            {
                PvpConstraint.Racing => "nfmm/racing",
                PvpConstraint.Wasting => "nfmm/wasting",
                PvpConstraint.Both => "nfmm/both",
                _ => "nfmm/racing"
            },
            RaceDuration = raceTimer.Elapsed,
            Standings = []
        };
    }

    // ── Rendering ─────────────────────────────────────────────────

    public override void Render()
    {
        base.Render();

        if (_currentState == InnerRaceState.Finished)
        {
            HudState.StateText = $"Finished! Time: {raceTimer.Elapsed.Minutes:D2}:{raceTimer.Elapsed.Seconds:D2}.{raceTimer.Elapsed.Milliseconds:D3}";
            HudState.StateTextEndsAt = DateTime.Now + TimeSpan.FromSeconds(5);
        }
    }

    // ── Input ─────────────────────────────────────────────────────

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        if (key == Key.R)
        {
            Reset();
        }
    }
}