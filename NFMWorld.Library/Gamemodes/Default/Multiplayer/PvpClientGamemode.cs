using System.Diagnostics;
using Maxine.Extensions;
using MemoryPack;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Helpers;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Client-side PvP racing gamemode. Runs full physics locally, sends
/// checkpoint events to the server, and receives authoritative standings.
/// </summary>
public class PvpClientGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData, PvpConstraint constraint)
    : BaseClientGamemode(gamemodeParameters, gamemodeData)
{
    protected enum GamemodeState
    {
        Countdown,
        InProgress,
        Finished
    }

    protected GamemodeState CurrentState { get; set; } = GamemodeState.Countdown;

    protected Stopwatch RaceTimer { get; } = new Stopwatch();

    protected ClientCountdown Countdown { get; private set; }
    protected PhysicsController PhysicsController { get; private set; }

    // ── Client-only fields (formerly [ClientOnly]) ────────────────
    private int _clientCarIndex;
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

        _lastSentCheckpoint = -1;
        _lastSentLap = 0;
        _serverResults = null;
        RaceTimer.Reset();

        Countdown = new ClientCountdown();
        Countdown.Go += () =>
        {
            CurrentState = GamemodeState.InProgress;
            RaceTimer.Start();
        };
        PhysicsController = new PhysicsController(Players, CurrentStage);
        
        foreach (var (idx, player) in Players.WithIndex())
        {
            player.Car = new BackendCar(player.Parameters, idx, -500 + (400 * idx), 0)
            {
                CurrentCheckpoint = 0,
                CurrentLap = 0
            };
            if (player.Parameters.IsBot)
            {
                player.Bot = new ElStupido(this, GamemodeData);
            }
        }

        CurrentState = GamemodeState.Countdown;

        // Client-side reset (formerly ClientReset)
        GamemodeData.ClientCallbacks.ResetCheckpointGlow();
        HudState = new HudStateData { Lap = 1, TotalLaps = CurrentStage.nlaps };
        IBackend.Backend.StopAllSounds();
    }

    // ── Main tick ─────────────────────────────────────────────────

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {Players[_clientCarIndex].Car?.Position.X:0.00}, contoz: {Players[_clientCarIndex].Car?.Position.Z:0.00}, contoy: {Players[_clientCarIndex].Car?.Position.Y:0.00}");

        if (GamemodeData.RaceState != RaceState.InProgress)
        {
            return;
        }

        switch (CurrentState)
        {
            case GamemodeState.Countdown:
                Countdown.GameTick(HudState);
                break;
            case GamemodeState.InProgress:
                InRace();
                break;
            case GamemodeState.Finished:
                Finished();
                break;
        }
    }

    // ── Countdown ─────────────────────────────────────────────────

    // ── In-race ───────────────────────────────────────────────────

    protected virtual void InRace()
    {
        PhysicsController.GameTick();

        if (CurrentStage.checkpoints.Count != 0)
        {
            for (var i = 0; i < Players.Count; i++)
            {
                if (Players[i].Car is { } car)
                {
                    FixHoopHelper.HandleFixHoops(CurrentStage, car);
                    CheckPointHelper.HandleCheckPoint(CurrentStage, car);
                }
            }

            CheckPointHelper.CalculatePositions(CurrentStage, Players);

            // ── Local race finish detection (client-side fallback) ────
            for (var i = 0; i < Players.Count; i++)
            {
                if (Players[i].Car is { } car)
                {
                    if (car.CurrentLap >= CurrentStage.nlaps)
                    {
                        CurrentState = GamemodeState.Finished;
                        RaceTimer.Stop();
                    }
                }
            }

            // ── Send checkpoint events to server ──────────────────────
            var myCar = ClientPlayer.Car;

            if (myCar != null)
            {
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
                    SendServerEvent(payload);

                    _lastSentCheckpoint = myCar.CurrentCheckpoint;
                    _lastSentLap = myCar.CurrentLap;
                }
            }
        }

        _clientTicks++;

        // ── HUD and sounds ────────────────────────────────────────
        {
            if (ClientPlayer.Car is { } myCar)
                UpdateHudAndSounds(myCar);
        }
    }

    // ── Finished ──────────────────────────────────────────────────

    private void Finished()
    {
        foreach (var player in Players)
        {
            if (player.Car is { } car)
            {
                car.CarPhysics.Halted = true;
                car.Drive(GamemodeData.CurrentStage);
            }
        }
    }

    // ── Server events ─────────────────────────────────────────────

    public override void OnServerEvent(ReadOnlySpan<byte> payload)
    {
        // Handle gamemode-specific server events (e.g., standings updates).
    }

    public override void OnServerRaceFinished(RaceResults results)
    {
        Results = results;
        CurrentState = GamemodeState.Finished;
        RaceTimer.Stop();
    }

    // ── Rendering ─────────────────────────────────────────────────

    public override void Render()
    {
        base.Render();

        if (CurrentState == GamemodeState.Finished)
        {
            HudState.StateText = $"Finished! Time: {RaceTimer.Elapsed.Minutes:D2}:{RaceTimer.Elapsed.Seconds:D2}.{RaceTimer.Elapsed.Milliseconds:D3}";
            HudState.StateTextEndsAt = DateTime.Now + TimeSpan.FromSeconds(5);
        }
    }

    // ── Input ─────────────────────────────────────────────────────

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
    }
}