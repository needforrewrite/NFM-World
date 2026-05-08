namespace NFMWorld.Graphics.Core;

/// <summary>
/// Abstraction over platform input (keyboard, mouse, gamepad).
/// </summary>
public interface IInputProvider
{
    IKeyboard Keyboard { get; }
    IMouse Mouse { get; }
    IGamepad? GetGamepad(int index);
}

public interface IKeyboard
{
    bool IsPressed(ScanCode scanCode);
    bool IsHeld(ScanCode scanCode);
    bool IsReleased(ScanCode scanCode);
}

public interface IMouse
{
    int X { get; }
    int Y { get; }
    int DeltaX { get; }
    int DeltaY { get; }
    int Wheel { get; }
    IButtonState LeftButton { get; }
    IButtonState RightButton { get; }
    IButtonState MiddleButton { get; }
}

public interface IButtonState
{
    bool IsPressed { get; }
    bool IsHeld { get; }
    bool IsReleased { get; }
}

public interface IGamepad
{
    IButtonState South { get; }
    IButtonState East { get; }
    IButtonState West { get; }
    IButtonState North { get; }
    IButtonState Start { get; }
    IButtonState Back { get; }
    IButtonState LeftShoulder { get; }
    IButtonState RightShoulder { get; }
    IButtonState LeftStick { get; }
    IButtonState RightStick { get; }
    IButtonState DpadUp { get; }
    IButtonState DpadDown { get; }
    IButtonState DpadLeft { get; }
    IButtonState DpadRight { get; }
    float LeftX { get; }
    float LeftY { get; }
    float RightX { get; }
    float RightY { get; }
    float LeftTrigger { get; }
    float RightTrigger { get; }
}

// Scan codes — physical key positions. Mirrors SDL scancode for simplicity.
// Extend as needed; only the ones actually used in the project are listed.
public enum ScanCode
{
    Unknown = 0,
    A = 4, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Num1 = 30, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9, Num0,
    Return = 40, Escape, Backspace, Tab, Space,
    F1 = 58, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Right = 79, Left, Down, Up,
    LeftControl = 224, LeftShift, LeftAlt, LeftGui,
    RightControl, RightShift, RightAlt, RightGui,
    Delete = 76,
    Home = 74,
    End = 77,
    PageUp = 75,
    PageDown = 78,
    Insert = 73,
}
