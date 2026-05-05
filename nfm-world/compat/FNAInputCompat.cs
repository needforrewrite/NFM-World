// FNA Input compatibility stubs.
// ModelEditor uses Microsoft.Xna.Framework.Input.* directly.
// These stubs let it compile until it's ported to MoonWorks.Input.
#pragma warning disable CS0618

namespace Microsoft.Xna.Framework.Input;

[Obsolete("Port to MoonWorks.Input")]
public static class Keyboard
{
    public static KeyboardState GetState() => default;
}

[Obsolete("Port to MoonWorks.Input")]
public static class Mouse
{
    public static MouseState GetState() => default;
}

[Obsolete("Port to MoonWorks.Input")]
public struct KeyboardState
{
    public bool IsKeyDown(Keys key) => false;
    public bool IsKeyUp(Keys key) => true;
}

[Obsolete("Port to MoonWorks.Input")]
public struct MouseState
{
    public ButtonState LeftButton => ButtonState.Released;
    public ButtonState RightButton => ButtonState.Released;
    public ButtonState MiddleButton => ButtonState.Released;
    public int X => 0;
    public int Y => 0;
    public int ScrollWheelValue => 0;
}

[Obsolete("Port to MoonWorks.Input")]
public enum ButtonState { Released, Pressed }

[Obsolete("Port to MoonWorks.Input")]
public enum Keys
{
    None, LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space, Enter, Escape, Tab, Back, Delete, Insert, Home, End, PageUp, PageDown,
    Left, Right, Up, Down, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}

#pragma warning restore CS0618
