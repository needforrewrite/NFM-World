using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Hud;

public sealed class SpectatorRaceFeedback
{
    private readonly RaceAnnouncer _announcer = new();
    private BackendCar? _car;
    private int _lastProgress;

    public HudMessage? Status => _announcer.Status;
    public HudMessage? Stunt => _announcer.Stunt;
    public bool PassedCheckpoint { get; private set; }
    public bool PlayPowerUp { get; private set; }
    public ushort? NextCheckpoint { get; private set; }
    public bool FinishCheckpoint { get; private set; }

    public void Reset()
    {
        _announcer.Reset();
        _car = null;
        _lastProgress = 0;
        PassedCheckpoint = false;
        PlayPowerUp = false;
        NextCheckpoint = null;
        FinishCheckpoint = false;
    }

    public void Tick(BackendCar? car, BaseClientGamemode gamemode)
    {
        var changed = !ReferenceEquals(car, _car);
        PassedCheckpoint = car is not null && !changed && car.TotalCheckpoint > _lastProgress;
        _lastProgress = car?.TotalCheckpoint ?? 0;
        _car = car;
        NextCheckpoint = car is not null
                         && car.CurrentLap < gamemode.CurrentStage.Nlaps
                         && car.CurrentCheckpoint < gamemode.CurrentStage.Checkpoints.Count
            ? car.CurrentCheckpoint : null;
        FinishCheckpoint = NextCheckpoint == gamemode.CurrentStage.Checkpoints.Count - 1
                           && car?.CurrentLap
                           == gamemode.CurrentStage.Nlaps - 1;
        PlayPowerUp = _announcer.TickSpectating(car, gamemode, changed);
    }
}
