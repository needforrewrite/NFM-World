extern alias SDL3New;

using System.Runtime.InteropServices;
using NFMWorld.DriverInterface;
using SDL3New::SDL3;

namespace NFMWorld.Platform.SDL3;

/// <summary>
/// SDL3-backed window/event-pump layer: window creation/resize/fullscreen, an event pump
/// translating SDL_EVENT_WINDOW_RESIZED/SDL_EVENT_TEXT_INPUT/keyboard/mouse events into
/// NFMWorld.DriverInterface.Key/Keys/MouseButtons, and the raw native window handle so
/// NFMWorld.Graphics.FNA3D (or another backend) can create its device against the same window.
/// Replaces FNA's Game/GraphicsDeviceManager window ownership (see WorldGame.cs).
/// </summary>
public sealed class SdlWindow : IDisposable
{
    private readonly IntPtr _window;
    private bool _disposed;
    private bool _fullscreenPending;
    private bool _fullscreenRetryOnFocus;
    private int _fullscreenWidth;
    private int _fullscreenHeight;
    private bool _borderlessDesktop;
    private bool _borderlessPending;

    public bool BorderlessDesktop
    {
        get => _borderlessDesktop;
        set
        {
            if (_borderlessDesktop == value) return;
            _borderlessDesktop = value;
            _borderlessPending = value;
        }
    }

    public void RefreshBorderlessDesktop() => _borderlessPending = _borderlessDesktop;

    public bool RequestedFullscreen { get; private set; }

    public IntPtr Handle => _window;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool ShouldQuit { get; private set; }

    public void RequestClose() => ShouldQuit = true;

    public void SetFullscreenResolution(int width, int height)
    {
        if (_fullscreenWidth == width && _fullscreenHeight == height) return;
        _fullscreenWidth = width;
        _fullscreenHeight = height;
        _fullscreenPending = true;
    }

    /// <summary>Raised on SDL_EVENT_WINDOW_RESIZED with the new client size, in pixels.</summary>
    public event Action<int, int>? Resized;

    /// <summary>Raised on SDL_EVENT_KEY_DOWN/SDL_EVENT_KEY_UP.</summary>
    public event Action<Key, bool /* down */, bool /* repeat */>? KeyChanged;

    /// <summary>Raised on SDL_EVENT_TEXT_INPUT, once per decoded character.</summary>
    public event Action<char>? TextInput;

    public event Action<MouseButtons, int, int>? MouseButtonDown;
    public event Action<MouseButtons, int, int>? MouseButtonUp;
    public event Action<int, int>? MouseMoved;

    /// <summary>Raised on SDL_EVENT_MOUSE_WHEEL with (x, y) scroll amounts.</summary>
    public event Action<float, float>? MouseWheel;

    // Cumulative scroll accumulator, mirroring XNA's MouseState.ScrollWheelValue (120 units per
    // notch, i.e. WHEEL_DELTA) - static since there is only ever one live SdlWindow per process,
    // matching GetKeyboardState/GetMouseState's existing static-polling pattern. Only the vertical
    // axis accumulates here (XNA's legacy MouseState has no horizontal scroll value); consumers
    // that want the raw per-event x/y delta (e.g. ImGui) should subscribe to MouseWheel directly.
    private static int _scrollWheelValue;

    private SdlWindow(IntPtr window, int width, int height)
    {
        _window = window;
        Width = width;
        Height = height;
        RequestedFullscreen = Fullscreen;
        MouseWheel += (_, y) => _scrollWheelValue += (int)(y * 120);
    }

    /// <summary>
    /// Creates an SDL3 window. <paramref name="extraFlags"/> lets a graphics backend inject the
    /// window flags it needs at creation time (e.g. SDL_WINDOW_OPENGL) without SdlWindow itself
    /// depending on any particular graphics backend.
    /// </summary>
    public static SdlWindow Create(string title, int width, int height, bool resizable = true,
        bool highPixelDensity = true, SDL.SDL_WindowFlags extraFlags = default)
    {
        if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");

        var flags = extraFlags;
        return CreateCore(title, width, height, resizable, highPixelDensity, flags);
    }

    /// <summary>
    /// Same as <see cref="Create(string,int,int,bool,bool,SDL.SDL_WindowFlags)"/>, but takes the
    /// extra window flags as a raw bitmask instead of <c>SDL3.Core</c>'s <c>SDL.SDL_WindowFlags</c>
    /// enum, so a graphics backend can forward what the native library hands it without this
    /// project's SDL3 types leaking into that backend's public API. See
    /// <c>NFMWorld.Graphics.FNA3D.FNA3DInterop.PrepareWindowAttributes</c>'s callers.
    /// </summary>
    public static SdlWindow Create(string title, int width, int height, ulong extraFlagsRaw,
        bool resizable = true, bool highPixelDensity = true)
    {
        if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");

        return CreateCore(title, width, height, resizable, highPixelDensity, (SDL.SDL_WindowFlags)extraFlagsRaw);
    }

    private static SdlWindow CreateCore(string title, int width, int height, bool resizable,
        bool highPixelDensity, SDL.SDL_WindowFlags flags)
    {
        if (resizable) flags |= SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        if (highPixelDensity) flags |= SDL.SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY;

        var window = SDL.SDL_CreateWindow(title, width, height, flags);
        if (window == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.SDL_GetError()}");

        SDL.SDL_StartTextInput(window);

        return new SdlWindow(window, width, height);
    }

    /// <summary>Whether the window currently has input focus. Replaces FNA's <c>Game.IsActive</c>.</summary>
    public bool HasFocus => (SDL.SDL_GetWindowFlags(_window) & SDL.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS) != 0;

    public bool Fullscreen
    {
        get => (SDL.SDL_GetWindowFlags(_window) & SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;
        set
        {
            RequestedFullscreen = value;
            if (OperatingSystem.IsBrowser())
            {
                if (!SDL.SDL_SetWindowFullscreen(_window, value))
                    throw new InvalidOperationException($"SDL_SetWindowFullscreen failed: {SDL.SDL_GetError()}");
                return;
            }

            // Apply after sizing and event processing, once focus permits fullscreen entry.
            _fullscreenPending = true;
            _fullscreenRetryOnFocus = false;
        }
    }

    private void ApplyPendingFullscreen()
    {
        if (OperatingSystem.IsBrowser() || !_fullscreenPending || (RequestedFullscreen && !HasFocus)) return;

        _fullscreenPending = false;
        if (RequestedFullscreen && _fullscreenWidth > 0 && _fullscreenHeight > 0)
        {
            if (!SDL.SDL_GetClosestFullscreenDisplayMode(SDL.SDL_GetPrimaryDisplay(),
                    _fullscreenWidth, _fullscreenHeight, 0, true, out var mode))
                throw new InvalidOperationException($"Cannot select fullscreen resolution: {SDL.SDL_GetError()}");
            if (!SDL.SDL_SetWindowFullscreenMode(_window, ref mode))
                throw new InvalidOperationException($"SDL_SetWindowFullscreenMode failed: {SDL.SDL_GetError()}");
        }
        if (!SDL.SDL_SetWindowFullscreen(_window, RequestedFullscreen))
            throw new InvalidOperationException($"SDL_SetWindowFullscreen failed: {SDL.SDL_GetError()}");

        var synchronized = SDL.SDL_SyncWindow(_window);
        _fullscreenRetryOnFocus = !synchronized || Fullscreen != RequestedFullscreen;
        RefreshSize();
    }

    private void RefreshSize()
    {
        if (!SDL.SDL_GetWindowSize(_window, out var width, out var height) || width <= 0 || height <= 0)
            return;
        if (Width == width && Height == height) return;
        Width = width;
        Height = height;
        Resized?.Invoke(Width, Height);
    }

    private void ApplyPendingBorderless()
    {
        if (OperatingSystem.IsBrowser() || !_borderlessDesktop || !_borderlessPending || Fullscreen)
            return;
        var flags = SDL.SDL_GetWindowFlags(_window);
        if ((flags & SDL.SDL_WindowFlags.SDL_WINDOW_MINIMIZED) != 0) return;
        _borderlessPending = false;

        if (!SDL.SDL_GetDisplayBounds(SDL.SDL_GetPrimaryDisplay(), out var bounds))
            throw new InvalidOperationException($"SDL_GetDisplayBounds failed: {SDL.SDL_GetError()}");

        if ((flags & SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0)
        {
            if (!SDL.SDL_RestoreWindow(_window))
                throw new InvalidOperationException($"SDL_RestoreWindow failed: {SDL.SDL_GetError()}");
            SDL.SDL_SyncWindow(_window);
        }

        if (!SDL.SDL_GetWindowPosition(_window, out var x, out var y)
            || !SDL.SDL_GetWindowSize(_window, out var width, out var height))
            throw new InvalidOperationException($"Cannot query window bounds: {SDL.SDL_GetError()}");

        if (x == bounds.x && y == bounds.y && width == bounds.w && height == bounds.h) return;
        if (!SDL.SDL_SetWindowPosition(_window, bounds.x, bounds.y)
            || !SDL.SDL_SetWindowSize(_window, bounds.w, bounds.h))
            throw new InvalidOperationException($"Cannot fit borderless window to primary display: {SDL.SDL_GetError()}");
        SDL.SDL_SyncWindow(_window);
        RefreshSize();
    }

    /// <summary>Whether the window has no title bar/border. Replaces FNA's <c>GameWindow.IsBorderlessEXT</c>.</summary>
    public bool IsBorderlessEXT
    {
        get => (SDL.SDL_GetWindowFlags(_window) & SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS) != 0;
        set
        {
            if (!SDL.SDL_SetWindowBordered(_window, !value))
                throw new InvalidOperationException($"SDL_SetWindowBordered failed: {SDL.SDL_GetError()}");
        }
    }

    public void SetSize(int width, int height)
    {
        if (!SDL.SDL_SetWindowSize(_window, width, height))
            throw new InvalidOperationException($"SDL_SetWindowSize failed: {SDL.SDL_GetError()}");
    }

    public void SetTitle(string title) => SDL.SDL_SetWindowTitle(_window, title);

    /// <summary>Polls all pending SDL events and dispatches them; sets <see cref="ShouldQuit"/> on quit/close.</summary>
    public unsafe void PumpEvents()
    {
        while (SDL.SDL_PollEvent(out var e))
        {
            switch (e.type)
            {
                case (uint)SDL.SDL_EventType.SDL_EVENT_QUIT:
                case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                    ShouldQuit = true;
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                    RefreshSize();
                    RefreshBorderlessDesktop();
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_MOVED:
                case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_CHANGED:
                    RefreshBorderlessDesktop();
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                    if (!OperatingSystem.IsBrowser() && (_fullscreenRetryOnFocus || RequestedFullscreen != Fullscreen))
                        _fullscreenPending = true;
                    RefreshBorderlessDesktop();
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_KEY_DOWN:
                    KeyChanged?.Invoke(SdlKeyMap.FromScancode(e.key.scancode), true, e.key.repeat);
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_KEY_UP:
                    KeyChanged?.Invoke(SdlKeyMap.FromScancode(e.key.scancode), false, false);
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_TEXT_INPUT:
                    if (TextInput is not null && e.text.text is not null)
                    {
                        var text = Marshal.PtrToStringUTF8((IntPtr)e.text.text);
                        if (text is not null)
                        {
                            foreach (var c in text)
                                TextInput(c);
                        }
                    }
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                    MouseMoved?.Invoke((int)e.motion.x, (int)e.motion.y);
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                    MouseButtonDown?.Invoke(SdlKeyMap.FromButtonIndex(e.button.button), (int)e.button.x, (int)e.button.y);
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                    MouseButtonUp?.Invoke(SdlKeyMap.FromButtonIndex(e.button.button), (int)e.button.x, (int)e.button.y);
                    break;

                case (uint)SDL.SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                    MouseWheel?.Invoke(e.wheel.x, e.wheel.y);
                    break;
            }
        }

        ApplyPendingFullscreen();
        ApplyPendingBorderless();
        RefreshSize();
    }

    /// <summary>Polling-style full keyboard state, replacing FNA's Keyboard.GetState().</summary>
    public static Keys GetKeyboardState()
    {
        var state = SDL.SDL_GetKeyboardState();
        var keys = default(Keys);
        for (var i = 0; i < state.Length; i++)
        {
            if (state[i])
            {
                var key = SdlKeyMap.FromScancode((SDL.SDL_Scancode)i);
                if (key != Key.None)
                    keys |= key;
            }
        }
        return keys;
    }

    /// <summary>Polling-style mouse state, replacing FNA's Mouse.GetState().</summary>
    public static (MouseButtons Buttons, int X, int Y) GetMouseState()
    {
        var flags = SDL.SDL_GetMouseState(out var x, out var y);
        return (SdlKeyMap.FromButtonFlags(flags), (int)x, (int)y);
    }

    /// <summary>Cumulative scroll value, replacing FNA's <c>MouseState.ScrollWheelValue</c>.</summary>
    public static int GetScrollWheelValue() => _scrollWheelValue;

    /// <summary>The primary display's desktop resolution in pixels.</summary>
    public static unsafe (int Width, int Height) GetPrimaryDesktopResolution()
    {
        var mode = SDL.SDL_GetDesktopDisplayMode(SDL.SDL_GetPrimaryDisplay());
        if (mode == null)
            throw new InvalidOperationException($"SDL_GetDesktopDisplayMode failed: {SDL.SDL_GetError()}");
        return (mode->w, mode->h);
    }

    /// <summary>
    /// The primary display's supported fullscreen modes, deduplicated per (width, height) - SDL
    /// reports one entry per refresh rate - and in pixels, matching what
    /// <see cref="SetSize"/> takes. Replaces FNA's
    /// <c>GraphicsAdapter.DefaultAdapter.SupportedDisplayModes</c>: FNA built that list from this
    /// same SDL call (<c>SDL3_FNAPlatform.GetGraphicsAdapters</c>), so the entries are equivalent.
    /// </summary>
    public static unsafe IReadOnlyList<(int Width, int Height)> GetFullscreenDisplayModes()
    {
        // FNA's GraphicsAdapter only worked because touching it ran its platform class's static
        // ctor, which initializes SDL video; do the same here since the settings menu builds its
        // resolution list from a static field initializer that can run before Create(). SDL_Init
        // is idempotent, and Create() re-initializes anyway.
        if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");

        var modes = (SDL.SDL_DisplayMode**)SDL.SDL_GetFullscreenDisplayModes(SDL.SDL_GetPrimaryDisplay(), out var count);
        try
        {
            var seen = new HashSet<(int Width, int Height)>();
            var result = new List<(int Width, int Height)>(count);
            for (var i = 0; i < count; i++)
            {
                var mode = modes[i];
                if (mode == null || mode->w <= 0 || mode->h <= 0) continue;
                if (seen.Add((mode->w, mode->h)))
                    result.Add((mode->w, mode->h));
            }
            return result;
        }
        finally
        {
            SDL.SDL_free((IntPtr)modes);
        }
    }

    /// <summary>
    /// <c>SDL_SetHint</c>. The renderer-selection code sets FNA3D's driver hints before the window
    /// (and therefore the device) exists, and this project is the one that owns the SDL3-CS extern
    /// alias - so hint access is exposed here rather than making every caller alias the assembly
    /// itself. See this project's csproj for why the alias exists at all.
    /// </summary>
    public static void SetHint(string name, string value) => SDL.SDL_SetHint(name, value);

    /// <summary><c>SDL_GetHint</c> - null when the hint has never been set.</summary>
    public static string? GetHint(string name) => SDL.SDL_GetHint(name);

    /// <summary>
    /// The window's Win32 <c>HWND</c>, or <see cref="IntPtr.Zero"/> when it has none (a non-Windows
    /// build, or a window SDL did not create through the Win32 video driver).
    ///
    /// This is <c>SDL_GetWindowProperties</c> + <c>SDL_GetPointerProperty</c> for
    /// <c>SDL_PROP_WINDOW_WIN32_HWND_POINTER</c>, wrapped here for the same reason
    /// <see cref="Create(string,int,int,ulong,bool,bool)"/> takes a raw bitmask: a graphics backend
    /// that needs a native window handle should not have to alias this project's SDL3-CS references
    /// to get it. The return is a raw <see cref="IntPtr"/>, so nothing SDL-shaped leaks out.
    ///
    /// A backend that creates its own device against the window needs this; the GL path does not,
    /// because <c>SDL_GL_CreateContext</c> takes the <see cref="Handle"/> directly.
    /// </summary>
    public IntPtr NativeWindowHandle => SDL.SDL_GetPointerProperty(
        SDL.SDL_GetWindowProperties(_window),
        SDL.SDL_PROP_WINDOW_WIN32_HWND_POINTER,
        IntPtr.Zero);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SDL.SDL_DestroyWindow(_window);
        SDL.SDL_Quit();
    }
}
