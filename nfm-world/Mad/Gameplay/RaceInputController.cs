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

    private bool _freelook;

    /// <summary>
    /// Q/E only apply while airborne. On the ground they press both halves of a pair
    /// (bounce = up+down, strafe = left+right), which cancels normal driving — holding E
    /// through a landing is what makes left steering look dead.
    /// </summary>
    private bool _airborne;

    /// <summary>
    /// While free-looking, the driving keys fly the camera instead of the car (Q/E climb and
    /// descend), so the car coasts.
    /// </summary>
    public void SetFreelook(bool freelook, Control? control)
    {
        _freelook = freelook;
        UpdateControlState(control);
    }

    public FreelookInput Navigation
    {
        get
        {
            var bindings = SettingsMenu.Bindings;
            return new FreelookInput(
                _pressedKeys.Contains(bindings.Accelerate),
                _pressedKeys.Contains(bindings.Brake),
                _pressedKeys.Contains(bindings.TurnLeft),
                _pressedKeys.Contains(bindings.TurnRight),
                _pressedKeys.Contains(bindings.AerialBounce),
                _pressedKeys.Contains(bindings.AerialStrafe));
        }
    }

    public void Reset(Control? control)
    {
        _pressedKeys.Clear();
        UpdateControlState(control);
        if (control is null) return;
        control.Enter = false;
        control.Lookback = 0;
    }

    /// <summary>
    /// Synchronizes movement keys with physical state, including releases missed by event routing.
    /// </summary>
    public void Refresh(in Keys keys, Control? control, bool airborne)
    {
        _airborne = airborne;
        if (control is null)
            return;

        var bindings = SettingsMenu.Bindings;
        Hold(bindings.Accelerate, in keys);
        Hold(bindings.Brake, in keys);
        Hold(bindings.TurnLeft, in keys);
        Hold(bindings.TurnRight, in keys);
        Hold(bindings.AerialBounce, in keys);
        Hold(bindings.AerialStrafe, in keys);
        Hold(bindings.Handbrake, in keys);

        UpdateControlState(control);
    }

    /// <summary>
    /// Applies both pressed and released states from the same snapshot used for key events.
    /// </summary>
    private void Hold(Key key, in Keys keys)
    {
        if (keys[key])
            _pressedKeys.Add(key);
        else
            _pressedKeys.Remove(key);
    }

    /// <summary>Call on key press with the client car's control (may be null).</summary>
    /// <param name="lockTarget">
    /// Resolves a lock slot (0-based, in sidebar order) to the opponent's stable player index, or
    /// -1 when that slot holds no lockable opponent. Null when there is nothing to lock onto.
    /// </param>
    public void KeyPressed(Key key, Control? control, bool canTargetCars, Func<int, int>? lockTarget)
    {
        var bindings = SettingsMenu.Bindings;

        // Track pressed keys and update movement state before handling
        // non-movement keys.
        var firstPress = _pressedKeys.Add(key);
        UpdateControlState(control);

        if (firstPress && key == bindings.ToggleMusic)
            SettingsMenu.ToggleMusicMute();

        if (firstPress && key == bindings.ToggleSFX)
            SettingsMenu.ToggleSfxMute();

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

        if (key == bindings.ToggleArrace)
        {
            control.Arrace = canTargetCars && !control.Arrace;
            // Turning the car targeting on by hand starts from the automatic pick, not from
            // whatever slot was locked when it was last switched off.
            if (!control.Arrace) control.ArrowLock = -1;
        }

        if (firstPress && canTargetCars)
        {
            var slots = bindings.LockSlots;
            for (var slot = 0; slot < slots.Length; slot++)
            {
                if (key != slots[slot]) continue;
                var target = lockTarget?.Invoke(slot) ?? -1;
                if (target < 0) break;
                // Locking implies targeting; pressing the same digit again releases it.
                control.ArrowLock = control.ArrowLock == target ? -1 : target;
                control.Arrace = true;
                break;
            }
        }

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
        bool aerialBouncePressed = _airborne && _pressedKeys.Contains(bindings.AerialBounce);
        bool aerialStrafePressed = _airborne && _pressedKeys.Contains(bindings.AerialStrafe);
        bool handbrakePressed = _pressedKeys.Contains(bindings.Handbrake);

        // apply Up/Down controls
        control.Up = !_freelook && (acceleratePressed || aerialBouncePressed);
        control.Down = !_freelook && (brakePressed || aerialBouncePressed);
        control.Left = !_freelook && (turnLeftPressed || aerialStrafePressed);
        control.Right = !_freelook && (turnRightPressed || aerialStrafePressed);
        control.Handb = handbrakePressed;
    }
}
