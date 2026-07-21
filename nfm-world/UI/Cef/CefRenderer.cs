using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Shared;

namespace NFMWorld.UI.Cef;

/// <summary>
/// High-level CEF renderer for NFM-World, modeled on ImGuiRenderer.
/// Manages CEF lifecycle, input forwarding, and compositing the off-screen
/// browser texture into the FNA draw pipeline.
///
/// Usage:
///   _cefRenderer.Initialize();        // once, after GraphicsDevice is ready
///   _cefRenderer.Update(gameTime);    // each frame in Update()
///   _cefRenderer.Render();            // each frame in Draw(), between 3D and ImGui
///   _cefRenderer.Shutdown();          // once, in Dispose/UnloadContent
/// </summary>
public sealed class CefRenderer(Game game, string initialUrl, int browserWidth = 1280, int browserHeight = 720)
    : IDisposable
{
    private readonly Game _game = game ?? throw new ArgumentNullException(nameof(game));
    private readonly GraphicsDevice _graphicsDevice = game.GraphicsDevice;

    // CEF components
    private NfmwCefRenderHandler? _renderHandler;
    private NfmwCefClient? _cefClient;
    private CefBrowserHost? _browserHost;
    private CefBrowser? _browser;
    private bool _isInitialized;

    /// <summary>
    /// The JS ↔ C# message bridge. Exposed internally for NfmwCefClient
    /// to route process messages. Phases register handlers via
    /// <see cref="RegisterMessageHandler"/> / <see cref="UnregisterMessageHandler"/>.
    /// </summary>
    internal GameBridge Bridge { get; } = new();

    // Rendering
    private SpriteBatch? _spriteBatch;

    // Input
    private int _scrollWheelValue;
    private MouseState _lastMouseState;
    private KeyboardState _lastKeyboardState;
    private static readonly Keys[] AllKeys = Enum.GetValues<Keys>();

    // Settings
    private bool _inputEnabled = true;

    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initialize CEF and create the off-screen browser. Must be called after
    /// the game's GraphicsDevice is ready (Initialize/LoadContent).
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // 1. Load CEF native runtime
        CefRuntime.Load();

        // 2. Create settings
        var settings = new CefSettings
        {
            WindowlessRenderingEnabled = true,
            MultiThreadedMessageLoop = false,
            ExternalMessagePump = false,
            NoSandbox = true,
            BackgroundColor = new CefColor(0, 0, 0, 0), // Transparent
            RootCachePath = Path.Combine(Path.GetTempPath(), $"NFMW_CefCache_{System.Environment.ProcessId}"),
            LogSeverity = CefLogSeverity.Warning,
        };

        // 3. Create handlers
        _renderHandler = new NfmwCefRenderHandler(_graphicsDevice);
        _renderHandler.SetViewSize(browserWidth, browserHeight);
        _cefClient = new NfmwCefClient(_renderHandler, this);

        // 4. Initialize CEF
        InitializeCef(settings);

        // 5. Create browser
        var windowInfo = CefWindowInfo.Create();
        windowInfo.SetAsWindowless(IntPtr.Zero, true); // transparent = true

        var browserSettings = new CefBrowserSettings
        {
            WindowlessFrameRate = 60,
            BackgroundColor = new CefColor(0, 0, 0, 0),
        };
        
        CefRuntimeLoader.Initialize();

        _browser = CefBrowserHost.CreateBrowserSync(windowInfo, _cefClient, browserSettings, initialUrl);
        _browserHost = _browser?.GetHost();

        TextInputEXT.TextInput += ForwardTextInput;

        _isInitialized = true;
    }

    // Mostly copied from CefRuntimeLoader
    private static void InitializeCef(CefSettings? settings = null, KeyValuePair<string, string>[]? flags = null)
    {
        if (settings == null)
        {
            settings = new CefSettings();
        }
        
        var basePath = AppContext.BaseDirectory;
        
        settings.UncaughtExceptionStackSize = 100; // for uncaught exception event work properly

        settings.BrowserSubprocessPath = Path.Combine(AppContext.BaseDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NFMWorld.BrowserProcess.exe" : "NFMWorld.BrowserProcess");

        switch (CefRuntime.Platform)
        {
            case CefRuntimePlatform.Windows:
                // settings.MultiThreadedMessageLoop = true;
                break;

            case CefRuntimePlatform.MacOS:
                var resourcesPath = Path.Combine(basePath, "Resources");
                if (!Directory.Exists(resourcesPath))
                {
                    throw new FileNotFoundException("Unable to find Resources folder");
                }

                settings.NoSandbox = true;
                settings.MultiThreadedMessageLoop = false;
                settings.ExternalMessagePump = true;
                settings.MainBundlePath = basePath;
                settings.FrameworkDirPath = basePath;
                settings.ResourcesDirPath = resourcesPath;
                break;
                
            case CefRuntimePlatform.Linux:
                settings.NoSandbox = true;
                settings.MultiThreadedMessageLoop = true;
                break;
        }

        AppDomain.CurrentDomain.ProcessExit += delegate { CefRuntime.Shutdown(); };
        
        // Fix crash with youtube https://github.com/chromiumembedded/cef/issues/3643
        flags = (flags ?? []).Append(KeyValuePair.Create("disable-features", "FirstPartySets")).ToArray();

        var nfmwSchemeHandlerFactory = new NfmwSchemeHandlerFactory();

        CustomScheme[] customSchemes =
        [
            new()
            {
                SchemeName = "nfmw",
                DomainName = "",
                IsStandard = true,
                IsLocal = true,
                IsSecure = true,
                IsCorsEnabled = true,
                IsFetchEnabled = true,
                SchemeHandlerFactory = nfmwSchemeHandlerFactory,
            },
        ];
        CefRuntime.Initialize(new CefMainArgs([System.Environment.ProcessPath]), settings, new BrowserCefApp(customSchemes, flags), IntPtr.Zero);

        foreach (var scheme in customSchemes)
        {
            CefRuntime.RegisterSchemeHandlerFactory(scheme.SchemeName, scheme.DomainName, scheme.SchemeHandlerFactory);
        }
    }

    /// <summary>
    /// Pump CEF message loop and forward input. Call each frame in Update().
    /// </summary>
    public void Update(GameTime gameTime)
    {
        if (!_isInitialized) return;

        CefRuntime.DoMessageLoopWork();

        ForwardInput();
    }

    /// <summary>
    /// Draw the browser texture as a full-screen overlay, then composite
    /// any active popup (e.g., &lt;select&gt; dropdown) on top.
    /// Auto-resizes the browser to match the viewport if dimensions change.
    /// Call in Draw().
    /// </summary>
    public void Render()
    {
        if (!_isInitialized || _renderHandler?.BrowserTexture == null)
            return;

        // Auto-resize to match viewport so CSS layout and mouse
        // coordinates are 1:1 with the game window.
        var viewport = _graphicsDevice.Viewport;
        if (_renderHandler.ViewWidth != viewport.Width
            || _renderHandler.ViewHeight != viewport.Height)
        {
            Resize(viewport.Width, viewport.Height);
        }

        _spriteBatch ??= new SpriteBatch(_graphicsDevice);

        var texture = _renderHandler.BrowserTexture;

        var oldBlend = _graphicsDevice.BlendState;
        var oldDepth = _graphicsDevice.DepthStencilState;
        var oldRaster = _graphicsDevice.RasterizerState;

        _graphicsDevice.BlendState = BlendState.AlphaBlend;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

        // Main browser texture — scaled to full viewport
        _spriteBatch.Draw(texture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);

        // Popup overlay (e.g., <select> dropdown)
        if (_renderHandler.PopupVisible && _renderHandler.PopupTexture is { } popupTex)
        {
            var popupRect = _renderHandler.PopupRect;
            _spriteBatch.Draw(popupTex,
                new Rectangle(popupRect.X, popupRect.Y, popupRect.Width, popupRect.Height),
                Color.White);
        }

        _spriteBatch.End();

        _graphicsDevice.BlendState = oldBlend;
        _graphicsDevice.DepthStencilState = oldDepth;
        _graphicsDevice.RasterizerState = oldRaster;
    }

    /// <summary>
    /// Navigate the browser to a new URL.
    /// </summary>
    public void Navigate(string url)
    {
        _browser?.GetMainFrame().LoadUrl(url);
    }

    /// <summary>
    /// Execute JavaScript in the browser. Use for C# → JS push updates.
    /// </summary>
    public void ExecuteJavaScript(string code)
    {
        _browser?.GetMainFrame().ExecuteJavaScript(code, null, 0);
    }

    /// <summary>
    /// Enable or disable input forwarding to the browser.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    /// <summary>
    /// Consume the current keyboard state so that keys currently held down
    /// are not forwarded to CEF as new key-down events on the next frame.
    /// Call this after a phase transition that was triggered by a key press,
    /// to prevent key bleeding from one CEF page to the next.
    /// </summary>
    public void ConsumeKeyboardState()
    {
        _lastKeyboardState = Keyboard.GetState();
    }

    /// <summary>
    /// Resize the browser viewport.
    /// </summary>
    public void Resize(int width, int height)
    {
        _renderHandler?.SetViewSize(width, height);
        _browserHost?.WasResized();
    }

    public CefBrowser? GetBrowser() => _browser;

    /// <summary>
    /// Open the Chromium DevTools window for debugging the webview.
    /// </summary>
    public void ShowDevTools()
    {
        if (_browserHost != null)
        {
            var wi = CefWindowInfo.Create();
            wi.SetAsPopup(IntPtr.Zero, "DevTools");
            _browserHost.ShowDevTools(wi, _cefClient!, new CefBrowserSettings(), new CefPoint(0, 0));
        }
    }

    /// <summary>
    /// Close the Chromium DevTools window.
    /// </summary>
    public void CloseDevTools()
    {
        _browserHost?.CloseDevTools();
    }

    /// <summary>
    /// Reload the current CEF page.
    /// </summary>
    public void Reload()
    {
        _browser?.Reload();
    }

    /// <summary>
    /// Resolve the base page URL for the single-page app. All phases share
    /// one index.html; navigation uses hash fragments (#/main-menu, etc.).
    /// </summary>
    public static string ResolveBasePageUrl()
    {
        // Check for dev mode: NFMW_VITE_DEV env var or .vite-dev marker file
        var isDev = System.Environment.GetEnvironmentVariable("NFMW_VITE_DEV") == "1"
                    || File.Exists(Path.Combine(AppContext.BaseDirectory, "data", "html", ".vite-dev"));

        if (isDev)
        {
            return "http://localhost:5173/";
        }

        // Production: load via custom nfmw:// scheme (served from data/html/dist/)
        return "nfmw://app/index.html";
    }

    /// <summary>
    /// Register a per-phase message handler. Called by <see cref="PhaseBridge.Register"/>.
    /// </summary>
    public void RegisterMessageHandler(string phaseId, GameBridge.MessageHandler handler)
    {
        Bridge.Register(phaseId, handler);
    }

    /// <summary>
    /// Unregister a per-phase message handler. Called by <see cref="PhaseBridge.Unregister"/>.
    /// </summary>
    public void UnregisterMessageHandler(string phaseId)
    {
        Bridge.Unregister(phaseId);
    }

    /// <summary>
    /// Push an event from C# to JS for a specific phase.
    /// The JS side receives this via window.__nfmwDispatch("{phaseId}:{eventType}", data).
    /// </summary>
    public void PushToJs(string phaseId, string eventType, object? data)
    {
        GameBridge.PushToJs(_browser, phaseId, eventType, data);
    }

    /// <summary>
    /// Push an event from C# to JS for a specific phase.
    /// The JS side receives this via window.__nfmwDispatch("{phaseId}:{eventType}", data).
    /// </summary>
    public void PushToJs(string phaseId, string eventType, byte[] binary)
    {
        GameBridge.PushToJs(_browser, phaseId, eventType, binary);
    }

    #region Input Forwarding

    private void ForwardTextInput(char c)
    {
        var host = _browserHost!;

        var keyboard = Keyboard.GetState();
        var isShiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        var isCtrlDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var isAltDown = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);

        var mods = CefEventFlags.None;
        if (isShiftDown) mods |= CefEventFlags.ShiftDown;
        if (isCtrlDown) mods |= CefEventFlags.ControlDown;
        if (isAltDown) mods |= CefEventFlags.AltDown;

        var charEvent = new CefKeyEvent
        {
            WindowsKeyCode = c,
            NativeKeyCode = c,
            Modifiers = mods,
            IsSystemKey = false,
            EventType = CefKeyEventType.Char,
        };
        host.SendKeyEvent(charEvent);
    }

    private void ForwardInput()
    {
        if (!_inputEnabled || _browserHost == null || !_game.IsActive)
            return;

        var mouse = Mouse.GetState();
        ForwardMouseInput(mouse);
        ForwardKeyboardInput();
        _lastMouseState = mouse;
    }

    private void ForwardMouseInput(MouseState mouse)
    {
        var host = _browserHost!;
        var viewport = _graphicsDevice.Viewport;

        // Scale mouse coordinates from screen space to CEF browser view space.
        var scaleX = (float)_renderHandler!.ViewWidth / viewport.Width;
        var scaleY = (float)_renderHandler.ViewHeight / viewport.Height;
        var browserX = (int)(mouse.X * scaleX);
        var browserY = (int)(mouse.Y * scaleY);
        var lastBrowserX = (int)(_lastMouseState.X * scaleX);
        var lastBrowserY = (int)(_lastMouseState.Y * scaleY);

        // Build event flags from current button state so CEF knows
        // which buttons are held — critical for drag tracking (sliders, etc.)
        var flags = GetMouseFlags(mouse);

        // Mouse move
        if (browserX != lastBrowserX || browserY != lastBrowserY)
        {
            var mouseEvent = new CefMouseEvent(browserX, browserY, flags);
            host.SendMouseMoveEvent(mouseEvent, false);
        }

        // Mouse buttons
        var mouseEvt = new CefMouseEvent(browserX, browserY, flags);
        ProcessMouseButton(host, mouseEvt, mouse.LeftButton, _lastMouseState.LeftButton,
            CefMouseButtonType.Left);
        ProcessMouseButton(host, mouseEvt, mouse.RightButton, _lastMouseState.RightButton,
            CefMouseButtonType.Right);
        ProcessMouseButton(host, mouseEvt, mouse.MiddleButton, _lastMouseState.MiddleButton,
            CefMouseButtonType.Middle);

        // Scroll wheel
        var scrollDelta = mouse.ScrollWheelValue - _scrollWheelValue;
        if (scrollDelta != 0)
        {
            _scrollWheelValue = mouse.ScrollWheelValue;
            var wheelEvent = new CefMouseEvent(browserX, browserY, flags);
            host.SendMouseWheelEvent(wheelEvent, 0, scrollDelta);
        }
    }

    private static CefEventFlags GetMouseFlags(MouseState mouse)
    {
        var flags = CefEventFlags.None;
        if (mouse.LeftButton == ButtonState.Pressed)   flags |= CefEventFlags.LeftMouseButton;
        if (mouse.RightButton == ButtonState.Pressed)  flags |= CefEventFlags.RightMouseButton;
        if (mouse.MiddleButton == ButtonState.Pressed) flags |= CefEventFlags.MiddleMouseButton;
        return flags;
    }

    private void ProcessMouseButton(CefBrowserHost host, CefMouseEvent mouseEvent,
        ButtonState current, ButtonState previous, CefMouseButtonType button)
    {
        if (current != previous)
        {
            host.SendMouseClickEvent(mouseEvent, button, current == ButtonState.Released, 1);
        }
    }

    private void ForwardKeyboardInput()
    {
        var keyboard = Keyboard.GetState();
        var host = _browserHost!;
        
        var isShiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        var isCtrlDown = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var isAltDown = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);

        foreach (var key in AllKeys)
        {
            var isDown = keyboard.IsKeyDown(key);
            var wasDown = _lastKeyboardState.IsKeyDown(key);

            if (isDown != wasDown)
            {
                var (windowsKeyCode, modifiers) = MapKeyToCef(key, isShiftDown, isCtrlDown, isAltDown);
                if (windowsKeyCode != 0)
                {
                    var keyEvent = new CefKeyEvent
                    {
                        WindowsKeyCode = windowsKeyCode,
                        NativeKeyCode = (int)key,
                        Modifiers = modifiers,
                        IsSystemKey = false,
                        EventType = isDown ? CefKeyEventType.RawKeyDown : CefKeyEventType.KeyUp,
                    };
                    host.SendKeyEvent(keyEvent);
                }
            }
        }

        _lastKeyboardState = keyboard;
    }

    /// <summary>
    /// Map FNA Keys to CEF Windows virtual key codes and modifier flags.
    /// </summary>
    private static (int KeyCode, CefEventFlags Modifiers) MapKeyToCef(Keys key, bool isShiftDown, bool isCtrlDown, bool isAltDown)
    {
        var mods = CefEventFlags.None;
        if (isShiftDown) mods |= CefEventFlags.ShiftDown;
        if (isCtrlDown) mods |= CefEventFlags.ControlDown;
        if (isAltDown) mods |= CefEventFlags.AltDown;
        
        return key switch
        {
            Keys.Back => (0x08, mods),              // VK_BACK
            Keys.Tab => (0x09, mods),               // VK_TAB
            Keys.Enter => (0x0D, mods),             // VK_RETURN
            Keys.Escape => (0x1B, mods),            // VK_ESCAPE
            Keys.Space => (0x20, mods),             // VK_SPACE
            Keys.PageUp => (0x21, mods),            // VK_PRIOR
            Keys.PageDown => (0x22, mods),          // VK_NEXT
            Keys.End => (0x23, mods),               // VK_END
            Keys.Home => (0x24, mods),              // VK_HOME
            Keys.Left => (0x25, mods),              // VK_LEFT
            Keys.Up => (0x26, mods),                // VK_UP
            Keys.Right => (0x27, mods),             // VK_RIGHT
            Keys.Down => (0x28, mods),              // VK_DOWN
            Keys.Delete => (0x2E, mods),            // VK_DELETE
            Keys.Insert => (0x2D, mods),            // VK_INSERT
            >= Keys.D0 and <= Keys.D9 => (0x30 + (key - Keys.D0), mods),
            >= Keys.A and <= Keys.Z => (0x41 + (key - Keys.A), mods),
            >= Keys.NumPad0 and <= Keys.NumPad9 => (0x60 + (key - Keys.NumPad0), mods),
            Keys.Multiply => (0x6A, mods),          // VK_MULTIPLY
            Keys.Add => (0x6B, mods),               // VK_ADD
            Keys.Subtract => (0x6D, mods),          // VK_SUBTRACT
            Keys.Decimal => (0x6E, mods),           // VK_DECIMAL
            Keys.Divide => (0x6F, mods),            // VK_DIVIDE
            >= Keys.F1 and <= Keys.F12 => (0x70 + (key - Keys.F1), mods),
            Keys.NumLock => (0x90, mods),           // VK_NUMLOCK
            Keys.Scroll => (0x91, mods),            // VK_SCROLL
            Keys.LeftShift or Keys.RightShift => (0x10, mods),  // VK_SHIFT
            Keys.LeftControl or Keys.RightControl => (0x11, mods), // VK_CONTROL
            Keys.LeftAlt or Keys.RightAlt => (0x12, mods),       // VK_MENU
            Keys.OemSemicolon => (0xBA, mods),      // VK_OEM_1
            Keys.OemPlus => (0xBB, mods),           // VK_OEM_PLUS
            Keys.OemComma => (0xBC, mods),          // VK_OEM_COMMA
            Keys.OemMinus => (0xBD, mods),          // VK_OEM_MINUS
            Keys.OemPeriod => (0xBE, mods),         // VK_OEM_PERIOD
            Keys.OemQuestion => (0xBF, mods),       // VK_OEM_2
            Keys.OemTilde => (0xC0, mods),          // VK_OEM_3
            Keys.OemOpenBrackets => (0xDB, mods),   // VK_OEM_4
            Keys.OemCloseBrackets => (0xDD, mods),  // VK_OEM_6
            Keys.OemPipe => (0xDC, mods),           // VK_OEM_5
            Keys.OemQuotes => (0xDE, mods),         // VK_OEM_7
            _ => (0, mods),
        };
    }

    #endregion

    #region Shutdown

    public void Shutdown()
    {
        if (!_isInitialized) return;

        _renderHandler?.DestroyTexture();

        _browserHost?.CloseBrowser();
        _browserHost?.Dispose();
        _browserHost = null;
        _browser = null;

        _spriteBatch?.Dispose();
        _spriteBatch = null;

        // CefRuntime.Shutdown must be called on the same thread as Initialize
        CefRuntime.Shutdown();
        _isInitialized = false;

        TextInputEXT.TextInput -= ForwardTextInput;
    }

    public void Dispose()
    {
        Shutdown();
    }

    #endregion
}

file abstract class CommonCefApp : CefApp
{
    private readonly CustomScheme[]? _customSchemes;

    internal CommonCefApp(CustomScheme[]? customSchemes = null) => this._customSchemes = customSchemes;

    protected override void OnRegisterCustomSchemes(CefSchemeRegistrar registrar)
    {
        if (this._customSchemes == null)
            return;
        foreach (CustomScheme customScheme in this._customSchemes)
            registrar.AddCustomScheme(customScheme.SchemeName, customScheme.Options);
    }
}

file class BrowserCefApp : CommonCefApp
{
    private readonly CefBrowserProcessHandler _browserProcessHandler;
    private readonly KeyValuePair<string, string>[]? _flags;

    internal BrowserCefApp(CustomScheme[]? customSchemes = null, KeyValuePair<string, string>[]? flags = null, BrowserProcessHandler? browserProcessHandler = null) :
        base(customSchemes)
    {
        _browserProcessHandler = new CommonBrowserProcessHandler(browserProcessHandler, customSchemes);
        _flags = flags;
    }

    protected override void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
    {
        if (string.IsNullOrEmpty(processType))
        {
            if (CefRuntime.Platform == CefRuntimePlatform.Linux) 
            {
                commandLine.AppendSwitch("no-zygote");
            }

            // if (CefRuntimeLoader.IsOSREnabled)
            {
                commandLine.AppendSwitch("disable-gpu", "1");
                commandLine.AppendSwitch("disable-gpu-compositing", "1");
                commandLine.AppendSwitch("enable-begin-frame-scheduling", "1");
                commandLine.AppendSwitch("disable-smooth-scrolling", "1");
            }

            if (_flags != null)
            {
                foreach (var flag in _flags)
                {     
                    commandLine.AppendSwitch(flag.Key, flag.Value);
                }
            }
        }
    }

    protected override CefBrowserProcessHandler GetBrowserProcessHandler()
    {
        return _browserProcessHandler;
    }
}

file static class CommandLineArgs
{
    public const string CustomScheme = "--custom-scheme";
    public const string ParentProcessId = "--parent-pid";
}

file class CommonBrowserProcessHandler(BrowserProcessHandler? handler, CustomScheme[]? customSchemes)
    : CefBrowserProcessHandler
{
    private readonly string _currentProcessId = System.Environment.ProcessId.ToString();

    protected override void OnBeforeChildProcessLaunch(CefCommandLine commandLine)
    {
        handler?.HandleBeforeChildProcessLaunch(commandLine);
        if (customSchemes?.Length > 0)
        {
            commandLine.AppendSwitch(CommandLineArgs.CustomScheme, ToCommandLineValue(customSchemes));
        }

        commandLine.AppendSwitch(CommandLineArgs.ParentProcessId, _currentProcessId);
    }

    protected override void OnContextInitialized()
    {
        handler?.HandleContextInitialized();
    }

    protected override void OnScheduleMessagePumpWork(long delayMs)
    {
        handler?.HandleScheduleMessagePumpWork(delayMs);
    }
    
    private static string SerializeToCommandLineValue(CustomScheme scheme)
    {
        return $"{scheme.SchemeName}|{scheme.DomainName}|{((int) scheme.Options).ToString()}";
    }

    internal static string ToCommandLineValue(CustomScheme[] schemes)
    {
        return string.Join(";", schemes.Select(SerializeToCommandLineValue));
    }

}

file class BrowserProcessHandler : CefBrowserProcessHandler
{
    internal void HandleBeforeChildProcessLaunch(CefCommandLine commandLine)
    {
        OnBeforeChildProcessLaunch(commandLine);
    }

    internal void HandleContextInitialized()
    {
        OnContextInitialized();
    }

    internal void HandleScheduleMessagePumpWork(long delayMs)
    {
        OnScheduleMessagePumpWork(delayMs);
    }
}