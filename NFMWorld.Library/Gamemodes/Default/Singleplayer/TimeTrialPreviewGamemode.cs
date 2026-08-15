using Microsoft.Xna.Framework;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;

namespace NFMWorld.Gameplay.Gamemodes;

public class TimeTrialPreviewGamemode(
    GamemodeParameters gamemodeParameters,
    IGamemodeData gamemodeData,
    SavedTimeTrial timeTrial)
    : TimeTrialClientGamemode1(gamemodeParameters, gamemodeData)
{
    private int _tick = 0;
    private bool _paused;
    private bool _slow;
    private int _slowTicks;
    private bool _shift;
    private bool _ctrl;
    private bool _simulating;

    public override void Reset()
    {
        base.Reset();
        _tick = 0;
    }

    protected override BackendCar LoadPlayerCar(int x, int z)
    {
        return new BackendCar(timeTrial.CarData ?? BackendGameSparker.GetCar(Players[0].Parameters.CarName).Rad!, 0, x, z, true);
    }

    protected override void TimeTrialInRace()
    {
        if (!_simulating || _paused)
        {
            if (_tick < timeTrial.DemoData.Ticks.Count && _tick > 0)
            {
                timeTrial.DemoData.Ticks[_tick - 1].ApplyToCar(Players[0].Car!);
            }
        }

        Players[PlayerCarIndex].Car!.Control
            .Decode(timeTrial.GetTick(_tick) ?? (false, false, false, false, false));
        base.TimeTrialInRace();

        if (_slow && !_paused)
        {
            _slowTicks++;
            if (_slowTicks % 3 == 0)
            {
                _tick++;
            }
        }
        else
        {
            if (!_paused)
            {
                _tick++;
            }
        }
    }

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);

        if (key is Key.ShiftKey or Key.LShiftKey or Key.RShiftKey)
        {
            _shift = true;
        }

        if (key is Key.ControlKey or Key.LControlKey or Key.RControlKey)
        {
            _ctrl = true;
        }

        if (key == Key.Space)
        {
            _paused = !_paused;
        }

        if (key == Key.W)
        {
            if (_ctrl)
            {
                _tick += 63 * 60;
            }
            else if (_shift)
            {
                _tick += 63;
            }
            else
            {
                _tick++;
            }
        }

        if (key == Key.S)
        {
            if (_ctrl)
            {
                _tick -= 63 * 60;
            }
            else if (_shift)
            {
                _tick -= 63;
            }
            else
            {
                _tick--;
            }
        }

        if (key == Key.A)
        {
            _slow = true;
            _slowTicks = 0;
        }

        if (key == Key.M)
        {
            _simulating = !_simulating;
        }
    }

    public override void KeyReleased(Key key, in Keys keys)
    {
        base.KeyReleased(key, keys);

        if (key is Key.ShiftKey or Key.LShiftKey or Key.RShiftKey)
        {
            _shift = false;
        }

        if (key is Key.ControlKey or Key.LControlKey or Key.RControlKey)
        {
            _ctrl = false;
        }
    }

    public override void Render()
    {
        base.Render();

        G.SetFont(new Font(FontFamily.RobotoMono, FontStyle.Plain, 16));
        G.SetColor(Color.Black);
        G.DrawStringStroke($"Tick: {_tick} / {timeTrial.DemoData.Ticks.Count} ({_currentState}) (Simulating: {_simulating})", 10, 250);
        G.SetColor(Color.White);
        G.DrawString($"Tick: {_tick} / {timeTrial.DemoData.Ticks.Count} ({_currentState}) (Simulating: {_simulating})", 10, 250);
    }
}