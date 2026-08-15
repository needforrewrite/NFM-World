using NFMWorld.DriverInterface;
using NFMWorld.UI;
using NFMWorldLibrary;

namespace NFMWorld.Gameplay;

/// <summary>
/// Translates raw keyboard state into the client car's <see cref="Control"/>.
/// Owns the pressed-key set and binding lookups so the race phase stays free
/// of input plumbing.
/// </summary>
public sealed class RaceInputController
{
    private readonly HashSet<Key> _pressedKeys = new();

    /// <summary>Call on key press with the client car's control (may be null).</summary>
    public void KeyPressed(Key key, Control? control)
    {
        var bindings = SettingsMenu.Bindings;

        // Track pressed keys and update movement state before handling
        // non-movement keys.
        _pressedKeys.Add(key);
        UpdateControlState(control);

        if (control is null)
            return;

        if (key == bindings.Enter)
            control.Enter = true;

        if (key == bindings.LookBack)
            control.Lookback = -1;

        if (key == bindings.LookLeft)
            control.Lookback = 3;

        if (key == bindings.LookRight)
            control.Lookback = 2;

        if (key == bindings.ToggleMusic)
            control.Mutem = !control.Mutem;

        if (key == bindings.ToggleSFX)
            control.Mutes = !control.Mutes;

        if (key == bindings.ToggleArrace)
            control.Arrace = !control.Arrace;

        if (key == bindings.ToggleRadar)
            control.Radar = !control.Radar;
    }

    /// <summary>Call on key release with the client car's control (may be null).</summary>
    public void KeyReleased(Key key, Control? control)
    {
        var bindings = SettingsMenu.Bindings;

        _pressedKeys.Remove(key);
        UpdateControlState(control);

        if (control is null)
            return;

        if (key == Key.Escape)
        {
            // this seems to be currently unused
            control.Exit = false;
        }

        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
            control.Lookback = 0;
    }

    private void UpdateControlState(Control? control)
    {
        if (control is null)
            return;

        var bindings = SettingsMenu.Bindings;

        // determine base key states
        bool acceleratePressed = _pressedKeys.Contains(bindings.Accelerate);
        bool brakePressed = _pressedKeys.Contains(bindings.Brake);
        bool turnLeftPressed = _pressedKeys.Contains(bindings.TurnLeft);
        bool turnRightPressed = _pressedKeys.Contains(bindings.TurnRight);
        bool aerialBouncePressed = _pressedKeys.Contains(bindings.AerialBounce);
        bool aerialStrafePressed = _pressedKeys.Contains(bindings.AerialStrafe);
        bool handbrakePressed = _pressedKeys.Contains(bindings.Handbrake);

        // apply Up/Down controls
        control.Up = acceleratePressed || aerialBouncePressed;
        control.Down = brakePressed || aerialBouncePressed;
        control.Left = turnLeftPressed || aerialStrafePressed;
        control.Right = turnRightPressed || aerialStrafePressed;
        control.Handb = handbrakePressed;
    }
}
