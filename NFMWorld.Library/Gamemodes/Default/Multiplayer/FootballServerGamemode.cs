using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Server-side football gamemode. Manages the countdown and game state.
/// Ball authority will be added later (currently host-client handles the ball).
/// </summary>
public class FootballServerGamemode : BaseServerGamemode
{
    private enum InnerRaceState { WaitingToStart, Countdown, InProgress, Finished }
    private InnerRaceState _state = InnerRaceState.WaitingToStart;
    private int _countdownTime = 4;
    private int _innerCountdownTicks;

    public override string GamemodeId => "nfmm/football";

    // ── Lifecycle ──────────────────────────────────────────────────

    public override void Begin(IServerGamemodeContext context)
    {
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
                break;
            case InnerRaceState.Countdown:
                CountdownTick();
                break;
            case InnerRaceState.InProgress:
            case InnerRaceState.Finished:
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

    // ── State ──────────────────────────────────────────────────────

    public override GameStateSnapshot? GetStateSnapshot()
    {
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
}
