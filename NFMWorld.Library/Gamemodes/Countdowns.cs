using NFMWorld.DriverInterface;
using NFMWorld.Sfx;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class ClientCountdown
{
    private int _countdownTime = 4;
    private int _innerCountdownTicks = 0;

    public event Action? Go;

    /// <summary>
    /// Updates the countdown timer and plays the corresponding sound.
    /// Calls <see cref="Go"/> when the countdown hits zero.
    /// </summary>
    public void GameTick(HudStateData hudState)
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            SfxLibrary.countdown[_countdownTime].Play();
            if (_countdownTime <= 0)
            {
                Go?.Invoke();
            }
        }

        hudState.CountdownTimer = _countdownTime;
    }
}

public class ServerCountdown
{
    private int _countdownTime = 4;
    private int _innerCountdownTicks = 0;

    public event Action? Go;

    /// <summary>
    /// Updates the countdown timer and plays the corresponding sound.
    /// Calls <see cref="Go"/> when the countdown hits zero.
    /// </summary>
    public void GameTick()
    {
        _innerCountdownTicks--;
        if (_innerCountdownTicks <= 0)
        {
            _countdownTime--;
            _innerCountdownTicks = (int)(10 * (1 / Physics.PHYSICS_MULTIPLIER));
            if (_countdownTime <= 0)
            {
                Go?.Invoke();
            }
        }
    }
}