using NFMWorld.DriverInterface;
using NFMWorld.UI;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorld.Gameplay;

public partial class RacePhase
{
    private readonly RaceFinishState _finish = new();
    private bool _finishRequested;
    private bool _deathSpectating;
    private bool WatchingPlayers => _spectating || (_deathSpectating && ClientCar is { Wasted: true });
    private bool ShowingDeathResult => !_spectating
                                       && !_deathSpectating
                                       && UsesStandardRaceCompletion
                                       && ClientCar is { Wasted: true }
                                       && GamemodeInstance is BaseClientGamemode
                                           { HudState.Finished: false, RespawnsWastedCars: false };

    /// <summary>
    /// The gamemode asks for the standard race-completion UI (finish banner, death result)
    /// instead of opt-in special cases. Time Trial also uses it; a phaseless attract drive or
    /// any future mode that does not score a race can leave it off.
    /// </summary>
    private bool UsesStandardRaceCompletion =>
        GamemodeInstance is BaseClientGamemode gm && gm.UsesStandardRaceCompletion;

    private void ResetWithdrawnFinish()
    {
        if (GamemodeInstance is not BaseClientGamemode { HudState.Finished: false })

            return;
        if (ShowingDeathResult)

            return;
        _finishRequested = false;
        if (_finish.Active.Value)
            _finish.Reset();
    }

    internal bool HandleRaceFinishKey(Key key)
    {
        if (!_finish.Active.Value || IsPaused)

            return false;

        if (_finish.ShowSummary.Value && key == Key.Escape)
        {
            QuitRace();

            return true;
        }

        if (key != Key.Enter && key != SettingsMenu.Bindings.Enter)

            return false;
        ContinueRaceFinish();

        return true;
    }

    private void ContinueRaceFinish()
    {
        if (!_finish.CanContinue.Value)

            return;

        if (_finish.ShowSummary.Value)
        {
            QuitRace();

            return;
        }

        if (ShowingDeathResult)
        {
            CloseChat();
            _input.Reset(ClientCar?.Control);

            if (_host is NFMWorld.Gameplay.RaceHost.NetworkRaceHost)
            {
                _deathSpectating = true;
                _finish.Reset();
            }
            else
                _finish.TryShowSummary();

            return;
        }

        if (_host is NFMWorld.Gameplay.RaceHost.NetworkRaceHost
            && !_spectating
            && RaceState != NFMWorldLibrary.RaceState.Finished
            && GamemodeInstance is BaseClientGamemode gamemode)
        {
            _finishRequested = true;
            gamemode.ConfirmFinish();

            return;
        }

        CloseChat();
        _input.Reset(ClientCar?.Control);
        _finish.TryShowSummary();
    }
}
