extern alias SDL3New;

using NFMWorld.DriverInterface;
using SDL3New::SDL3;

namespace NFMWorld.Platform.SDL3;

/// <summary>
/// Translates SDL3's physical scancodes and mouse button indices into
/// NFMWorld.DriverInterface.Key/MouseButtons, replacing FNA's
/// Keyboard.GetState()/Mouse.GetState()-based translation
/// (nfm-world/Util/DriverInterfaceExtensions.cs) for the SDL3 platform layer.
/// Scancode (physical key position) is used rather than keycode (layout-dependent
/// character), matching what DriverInterfaceExtensions.FromScanCode already did via
/// FNA's Keyboard.GetKeyFromScancodeEXT.
/// </summary>
public static class SdlKeyMap
{
    public static Key FromScancode(SDL.SDL_Scancode scancode) => scancode switch
    {
        SDL.SDL_Scancode.SDL_SCANCODE_A => Key.A,
        SDL.SDL_Scancode.SDL_SCANCODE_B => Key.B,
        SDL.SDL_Scancode.SDL_SCANCODE_C => Key.C,
        SDL.SDL_Scancode.SDL_SCANCODE_D => Key.D,
        SDL.SDL_Scancode.SDL_SCANCODE_E => Key.E,
        SDL.SDL_Scancode.SDL_SCANCODE_F => Key.F,
        SDL.SDL_Scancode.SDL_SCANCODE_G => Key.G,
        SDL.SDL_Scancode.SDL_SCANCODE_H => Key.H,
        SDL.SDL_Scancode.SDL_SCANCODE_I => Key.I,
        SDL.SDL_Scancode.SDL_SCANCODE_J => Key.J,
        SDL.SDL_Scancode.SDL_SCANCODE_K => Key.K,
        SDL.SDL_Scancode.SDL_SCANCODE_L => Key.L,
        SDL.SDL_Scancode.SDL_SCANCODE_M => Key.M,
        SDL.SDL_Scancode.SDL_SCANCODE_N => Key.N,
        SDL.SDL_Scancode.SDL_SCANCODE_O => Key.O,
        SDL.SDL_Scancode.SDL_SCANCODE_P => Key.P,
        SDL.SDL_Scancode.SDL_SCANCODE_Q => Key.Q,
        SDL.SDL_Scancode.SDL_SCANCODE_R => Key.R,
        SDL.SDL_Scancode.SDL_SCANCODE_S => Key.S,
        SDL.SDL_Scancode.SDL_SCANCODE_T => Key.T,
        SDL.SDL_Scancode.SDL_SCANCODE_U => Key.U,
        SDL.SDL_Scancode.SDL_SCANCODE_V => Key.V,
        SDL.SDL_Scancode.SDL_SCANCODE_W => Key.W,
        SDL.SDL_Scancode.SDL_SCANCODE_X => Key.X,
        SDL.SDL_Scancode.SDL_SCANCODE_Y => Key.Y,
        SDL.SDL_Scancode.SDL_SCANCODE_Z => Key.Z,
        SDL.SDL_Scancode.SDL_SCANCODE_0 => Key.D0,
        SDL.SDL_Scancode.SDL_SCANCODE_1 => Key.D1,
        SDL.SDL_Scancode.SDL_SCANCODE_2 => Key.D2,
        SDL.SDL_Scancode.SDL_SCANCODE_3 => Key.D3,
        SDL.SDL_Scancode.SDL_SCANCODE_4 => Key.D4,
        SDL.SDL_Scancode.SDL_SCANCODE_5 => Key.D5,
        SDL.SDL_Scancode.SDL_SCANCODE_6 => Key.D6,
        SDL.SDL_Scancode.SDL_SCANCODE_7 => Key.D7,
        SDL.SDL_Scancode.SDL_SCANCODE_8 => Key.D8,
        SDL.SDL_Scancode.SDL_SCANCODE_9 => Key.D9,
        SDL.SDL_Scancode.SDL_SCANCODE_RETURN => Key.Enter,
        SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE => Key.Escape,
        SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE => Key.Back,
        SDL.SDL_Scancode.SDL_SCANCODE_TAB => Key.Tab,
        SDL.SDL_Scancode.SDL_SCANCODE_SPACE => Key.Space,
        SDL.SDL_Scancode.SDL_SCANCODE_MINUS => Key.OemMinus,
        SDL.SDL_Scancode.SDL_SCANCODE_EQUALS => Key.Oemplus,
        SDL.SDL_Scancode.SDL_SCANCODE_LEFTBRACKET => Key.OemOpenBrackets,
        SDL.SDL_Scancode.SDL_SCANCODE_RIGHTBRACKET => Key.OemCloseBrackets,
        SDL.SDL_Scancode.SDL_SCANCODE_BACKSLASH => Key.OemPipe,
        SDL.SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH => Key.OemBackslash,
        SDL.SDL_Scancode.SDL_SCANCODE_SEMICOLON => Key.OemSemicolon,
        SDL.SDL_Scancode.SDL_SCANCODE_APOSTROPHE => Key.OemQuotes,
        SDL.SDL_Scancode.SDL_SCANCODE_GRAVE => Key.Oemtilde,
        SDL.SDL_Scancode.SDL_SCANCODE_COMMA => Key.Oemcomma,
        SDL.SDL_Scancode.SDL_SCANCODE_PERIOD => Key.OemPeriod,
        SDL.SDL_Scancode.SDL_SCANCODE_SLASH => Key.OemQuestion,
        SDL.SDL_Scancode.SDL_SCANCODE_CAPSLOCK => Key.CapsLock,
        SDL.SDL_Scancode.SDL_SCANCODE_F1 => Key.F1,
        SDL.SDL_Scancode.SDL_SCANCODE_F2 => Key.F2,
        SDL.SDL_Scancode.SDL_SCANCODE_F3 => Key.F3,
        SDL.SDL_Scancode.SDL_SCANCODE_F4 => Key.F4,
        SDL.SDL_Scancode.SDL_SCANCODE_F5 => Key.F5,
        SDL.SDL_Scancode.SDL_SCANCODE_F6 => Key.F6,
        SDL.SDL_Scancode.SDL_SCANCODE_F7 => Key.F7,
        SDL.SDL_Scancode.SDL_SCANCODE_F8 => Key.F8,
        SDL.SDL_Scancode.SDL_SCANCODE_F9 => Key.F9,
        SDL.SDL_Scancode.SDL_SCANCODE_F10 => Key.F10,
        SDL.SDL_Scancode.SDL_SCANCODE_F11 => Key.F11,
        SDL.SDL_Scancode.SDL_SCANCODE_F12 => Key.F12,
        SDL.SDL_Scancode.SDL_SCANCODE_F13 => Key.F13,
        SDL.SDL_Scancode.SDL_SCANCODE_F14 => Key.F14,
        SDL.SDL_Scancode.SDL_SCANCODE_F15 => Key.F15,
        SDL.SDL_Scancode.SDL_SCANCODE_F16 => Key.F16,
        SDL.SDL_Scancode.SDL_SCANCODE_F17 => Key.F17,
        SDL.SDL_Scancode.SDL_SCANCODE_F18 => Key.F18,
        SDL.SDL_Scancode.SDL_SCANCODE_F19 => Key.F19,
        SDL.SDL_Scancode.SDL_SCANCODE_F20 => Key.F20,
        SDL.SDL_Scancode.SDL_SCANCODE_F21 => Key.F21,
        SDL.SDL_Scancode.SDL_SCANCODE_F22 => Key.F22,
        SDL.SDL_Scancode.SDL_SCANCODE_F23 => Key.F23,
        SDL.SDL_Scancode.SDL_SCANCODE_F24 => Key.F24,
        SDL.SDL_Scancode.SDL_SCANCODE_PRINTSCREEN => Key.PrintScreen,
        SDL.SDL_Scancode.SDL_SCANCODE_SCROLLLOCK => Key.Scroll,
        SDL.SDL_Scancode.SDL_SCANCODE_PAUSE => Key.Pause,
        SDL.SDL_Scancode.SDL_SCANCODE_INSERT => Key.Insert,
        SDL.SDL_Scancode.SDL_SCANCODE_HOME => Key.Home,
        SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP => Key.PageUp,
        SDL.SDL_Scancode.SDL_SCANCODE_DELETE => Key.Delete,
        SDL.SDL_Scancode.SDL_SCANCODE_END => Key.End,
        SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN => Key.PageDown,
        SDL.SDL_Scancode.SDL_SCANCODE_RIGHT => Key.Right,
        SDL.SDL_Scancode.SDL_SCANCODE_LEFT => Key.Left,
        SDL.SDL_Scancode.SDL_SCANCODE_DOWN => Key.Down,
        SDL.SDL_Scancode.SDL_SCANCODE_UP => Key.Up,
        SDL.SDL_Scancode.SDL_SCANCODE_NUMLOCKCLEAR => Key.NumLock,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_DIVIDE => Key.Divide,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_MULTIPLY => Key.Multiply,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_MINUS => Key.Subtract,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_PLUS => Key.Add,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER => Key.Enter,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_0 => Key.NumPad0,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_1 => Key.NumPad1,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_2 => Key.NumPad2,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_3 => Key.NumPad3,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_4 => Key.NumPad4,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_5 => Key.NumPad5,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_6 => Key.NumPad6,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_7 => Key.NumPad7,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_8 => Key.NumPad8,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_9 => Key.NumPad9,
        SDL.SDL_Scancode.SDL_SCANCODE_KP_PERIOD => Key.Decimal,
        SDL.SDL_Scancode.SDL_SCANCODE_APPLICATION => Key.Apps,
        SDL.SDL_Scancode.SDL_SCANCODE_EXECUTE => Key.Execute,
        SDL.SDL_Scancode.SDL_SCANCODE_HELP => Key.Help,
        SDL.SDL_Scancode.SDL_SCANCODE_MENU => Key.Menu,
        SDL.SDL_Scancode.SDL_SCANCODE_SELECT => Key.Select,
        SDL.SDL_Scancode.SDL_SCANCODE_CANCEL => Key.Cancel,
        SDL.SDL_Scancode.SDL_SCANCODE_CLEAR => Key.Clear,
        SDL.SDL_Scancode.SDL_SCANCODE_PRIOR => Key.Prior,
        SDL.SDL_Scancode.SDL_SCANCODE_SEPARATOR => Key.Separator,
        SDL.SDL_Scancode.SDL_SCANCODE_CRSEL => Key.Crsel,
        SDL.SDL_Scancode.SDL_SCANCODE_EXSEL => Key.Exsel,
        SDL.SDL_Scancode.SDL_SCANCODE_LCTRL => Key.LControlKey,
        SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT => Key.LShiftKey,
        SDL.SDL_Scancode.SDL_SCANCODE_LALT => Key.LMenu,
        SDL.SDL_Scancode.SDL_SCANCODE_LGUI => Key.LWin,
        SDL.SDL_Scancode.SDL_SCANCODE_RCTRL => Key.RControlKey,
        SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT => Key.RShiftKey,
        SDL.SDL_Scancode.SDL_SCANCODE_RALT => Key.RMenu,
        SDL.SDL_Scancode.SDL_SCANCODE_RGUI => Key.RWin,
        SDL.SDL_Scancode.SDL_SCANCODE_SLEEP => Key.Sleep,
        SDL.SDL_Scancode.SDL_SCANCODE_MEDIA_NEXT_TRACK => Key.MediaNextTrack,
        SDL.SDL_Scancode.SDL_SCANCODE_MEDIA_PREVIOUS_TRACK => Key.MediaPreviousTrack,
        SDL.SDL_Scancode.SDL_SCANCODE_MEDIA_STOP => Key.MediaStop,
        SDL.SDL_Scancode.SDL_SCANCODE_MEDIA_PLAY_PAUSE => Key.MediaPlayPause,
        SDL.SDL_Scancode.SDL_SCANCODE_MEDIA_SELECT => Key.SelectMedia,
        SDL.SDL_Scancode.SDL_SCANCODE_MUTE => Key.VolumeMute,
        SDL.SDL_Scancode.SDL_SCANCODE_VOLUMEUP => Key.VolumeUp,
        SDL.SDL_Scancode.SDL_SCANCODE_VOLUMEDOWN => Key.VolumeDown,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_BACK => Key.BrowserBack,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_FORWARD => Key.BrowserForward,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_REFRESH => Key.BrowserRefresh,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_STOP => Key.BrowserStop,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_SEARCH => Key.BrowserSearch,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_BOOKMARKS => Key.BrowserFavorites,
        SDL.SDL_Scancode.SDL_SCANCODE_AC_HOME => Key.BrowserHome,
        _ => Key.None,
    };

    /// <summary>
    /// SDL's SDL_MouseButtonEvent.button values: 1=left, 2=middle, 3=right, 4=x1, 5=x2.
    /// </summary>
    public static MouseButtons FromButtonIndex(byte button) => button switch
    {
        1 => MouseButtons.Primary,
        2 => MouseButtons.Middle,
        3 => MouseButtons.Secondary,
        4 => MouseButtons.XButton1,
        5 => MouseButtons.XButton2,
        _ => MouseButtons.None,
    };

    public static MouseButtons FromButtonFlags(SDL.SDL_MouseButtonFlags flags)
    {
        var buttons = MouseButtons.None;
        if ((flags & SDL.SDL_MouseButtonFlags.SDL_BUTTON_LMASK) != 0) buttons |= MouseButtons.Primary;
        if ((flags & SDL.SDL_MouseButtonFlags.SDL_BUTTON_RMASK) != 0) buttons |= MouseButtons.Secondary;
        if ((flags & SDL.SDL_MouseButtonFlags.SDL_BUTTON_MMASK) != 0) buttons |= MouseButtons.Middle;
        if ((flags & SDL.SDL_MouseButtonFlags.SDL_BUTTON_X1MASK) != 0) buttons |= MouseButtons.XButton1;
        if ((flags & SDL.SDL_MouseButtonFlags.SDL_BUTTON_X2MASK) != 0) buttons |= MouseButtons.XButton2;
        return buttons;
    }
}
