// LLM maintained.
//
// The browser entry point.
//
// WorldGame.Main's loop cannot run here - it sleeps and spins, which on the main thread would
// block the page - so the head brings the game up and then returns to JavaScript, which drives
// one frame per requestAnimationFrame through Tick. Everything inside a frame is the same code
// the desktop runs; only who calls it differs.

#nullable enable
extern alias SDL3New;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using NFMWorld.Platform;
using NFMWorld.Platform.WebGL;
using NFMWorld.Platform.WebGPU;
using NFMWorldLibrary;
using Silk.NET.Core.Contexts;
using SDL3New::SDL3;
using static SDL3New::SDL3.SDL;

namespace NFMWorld.Browser;

public static partial class Program
{
    private static WorldGame? _game;
    private static Stopwatch? _clock;

    /// <summary>
    /// Mounts the boot archive's data files into the game's VFS. Called from the page after it has
    /// fetched the archive, because a browser has no install directory to read them from.
    /// </summary>
    [JSExport]
    public static void MountAssets(byte[] zip, string musicUrl)
    {
        BrowserBoot.Mount(zip);
        Console.WriteLine($"[nfmw] mounted {zip.Length / 1024} KiB of boot assets");

        // Music is 148 MB and is played one track at a time, so it is a second archive read on
        // demand over HTTP range requests rather than part of the boot payload. The mount is
        // installed now so the first track that plays can find it.
        MountMusicArchive(musicUrl);
    }

    private static void MountMusicArchive(string musicUrl)
    {
        // Opened on the first fetch or track listing. The read itself is
        // synchronous (see BrowserHttp) because zip parsing is, and the game is paused meanwhile.
        MusicFetch? fetch = null;
        BrowserBoot.MountMusic(path =>
            {
                try
                {
                    fetch ??= MusicFetch.Open(musicUrl);
                    if (fetch is null) return null;

                    var entryPath = path.Replace('\\', '/').TrimStart('/');
                    if (!entryPath.StartsWith("data/", StringComparison.Ordinal))
                        entryPath = "data/" + entryPath;
                    return fetch.ReadEntry(entryPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[nfmw] music fetch failed for {path}: {e.Message}");
                    return null;
                }
            }, () =>
            {
                try
                {
                    fetch ??= MusicFetch.Open(musicUrl);
                    return fetch?.Paths ?? [];
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[nfmw] music listing failed: {e.Message}");
                    return [];
                }
            }
        );
    }

    /// <summary>
    /// The page's size in CSS pixels, handed over before the game is built.
    ///
    /// A field rather than a parameter: the wasm runtime calls the assembly's entry point with no
    /// arguments, so <see cref="Main"/> has to stay parameterless - a stub <c>Main</c> beside a
    /// parameterised export is not callable and the runtime's indirect call to it lands on null.
    /// The page writes this immediately before calling <see cref="Main"/>.
    /// </summary>
    private static (int Width, int Height) _bootSize = (1280, 720);

    /// <summary>
    /// Whether the page created a WebGPU device before boot. Set by <see cref="SetWebGpuAvailable"/>
    /// rather than probed: <c>emscripten_webgpu_get_device</c> asserts (or, in a release build, hands
    /// the JS side a null to import) when the page parked nothing, so asking is not a test.
    /// </summary>
    private static bool _webGpuAvailable;

    /// <summary>
    /// One archive asset to show instead of the game, from the page's query string. Null boots the
    /// game as usual.
    /// </summary>
    private static NFMWorld.Gameplay.ArchivePreviewRequest? _preview;

    /// <summary>
    /// Records the preview page's request. Called immediately before <see cref="Main"/>, like
    /// <see cref="SetBootSize"/>: the wasm entry point takes no arguments.
    /// </summary>
    /// <param name="mode">"stage", "model", or anything else for the radpack's own kind.</param>
    /// <param name="hash">The asset's SHA-256; empty boots the game.</param>
    /// <param name="archiveUrl">An archive host to fetch from, or null for the baked endpoint.</param>
    [JSExport]
    public static void SetPreview(string? mode, string? hash, string? archiveUrl)
    {
        _preview = NFMWorld.Gameplay.ArchivePreviewRequest.Parse(mode, hash, archiveUrl);
    }

    /// <summary>
    /// TEMP: names the boot step that is about to run. A trap in native code kills the module
    /// before any managed catch can report it, so the last line of the log is the only record of
    /// how far the boot got.
    /// </summary>
    private static void Step(string what) => Console.WriteLine($"[nfmw] > {what}");

    /// <summary>
    /// Brings the game up, then returns so the page can drive frames.
    ///
    /// Both the assembly's entry point and a JS export: the runtime calls it with no arguments, and
    /// the page calls it too (after <see cref="SetBootSize"/>) because the work has to happen on
    /// the game thread rather than during module instantiation.
    /// </summary>
    [JSExport]
    public static void Main()
    {
        Console.WriteLine("[nfmw] starting");

        // Everything native is statically linked into the module, so no library has to be loaded.
        // Zero rather than NativeLibrary.GetMainProgramHandle(): that throws on a non-AOT wasm
        // build, while any non-zero handle lets the P/Invoke fall through to the build-time table,
        // which is what actually resolves SDL, the WebGL shim and cimgui.
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (_, _, _) => IntPtr.Zero);

        // The desktop head wires this in WorldGame.Main, which never runs here. Information rather
        // than the Debug the exe uses: a Release page's minimum level is Information, and the
        // backend's boot line ("webgpu: <device>, surface Bgra8Unorm, ...") plus its surface errors
        // are the only record a web build leaves about which path it took.
        NFMWorld.Graphics.GraphicsDiagnostics.Warning = message => Logging.Info(message);

        try
        {
            // SDL still owns the window and the events: its emscripten video backend creates the
            // canvas's surface and feeds the input plumbing the game already speaks.
            Step("SDL_Init");
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
                throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");

            // The page reports whether it had a WebGPU device to give us, and that answer decides
            // the whole graphics path: it gates the renderer list, which the saved choice indexes
            // into, so it has to land before the config is read. On the WebGL2 path nothing
            // changes; on the WebGPU path the canvas must not be handed to WebGL first, because a
            // canvas holds one context type and a successful `getContext('webgl2')` locks WebGPU
            // out of it for good.
            NFMWorld.UI.SettingsMenu.BrowserWebGpuAvailable = _webGpuAvailable;

            // The desktop head parses the persisted settings in Main, before the game is built;
            // this path skips that, and the assets are already mounted by the time this runs, so
            // the values come from the boot archive (or stay at their defaults without one).
            Step("LoadConfigValues");
            NFMWorld.UI.SettingsMenu.LoadConfigValues();

            var renderer = NFMWorld.UI.SettingsMenu.BrowserRenderer;

            INativeContext context;
            Action? present = null;
            Action<int>? setSwapInterval = null;
            Action<int, int>? resizeDrawable = null;
            nint device = 0;

            if (renderer == Renderer.WebGpu)
            {
                // Imported, not requested: adapter and device requests answer through promises a
                // synchronous boot never runs, so the page creates the device and parks it for
                // this call. See WgpuNativeContext.
                Step("WgpuNativeContext.AcquireDevice");
                device = WgpuNativeContext.AcquireDevice();
                if (device == 0)
                    throw new InvalidOperationException("The page reported a WebGPU device but none was parked for the module.");
                Console.WriteLine($"[nfmw] webgpu device {device:x}");

                context = WgpuNativeContext.Instance;
            }
            else
            {
                Step("WebGlContext.Create");
                context = WebGlContext.Create();
                present = WebGlContext.Present;
                setSwapInterval = WebGlContext.SetSwapInterval;
                resizeDrawable = WebGlContext.Resize;
            }

            Step("CreateForBrowser");
            _game = WorldGame.CreateForBrowser(
                renderer,
                context,
                width: Math.Max(1, _bootSize.Width),
                height: Math.Max(1, _bootSize.Height),
                present: present,
                setSwapInterval: setSwapInterval,
                resizeDrawable: resizeDrawable,
                webGpuDevice: device
            );

            GameSparker.Game = _game;

            // The desktop head does this in Main, before the game exists: it fills in the model
            // tables GameSparker.Load reads (error_mesh and the stage/car parts) and picks the
            // backend. Nothing else does it on this path, so the models would all be null.
            //
            // A preview loads one archive asset from its own bytes, so it needs neither the model
            // tables nor the plugin loader; only error_mesh is shared, since a stage whose piece
            // fails to resolve still falls back to it.
            if (_preview is null)
            {
                Step("BackendGameSparker.Load");
                BackendGameSparker.Load(isHeadless: false);
            }
            else
            {
                Step("BackendGameSparker.LoadForPreview");
                BackendGameSparker.LoadForPreview();
            }

            Step("Initialize");
            _game.Initialize();
            Step(_preview is null ? "LoadContent" : "LoadContentPreview");
            if (_preview is null)
                _game.LoadContent();
            else
                _game.LoadContentPreview(_preview);

            Step("booted");
            _clock = Stopwatch.StartNew();
        }
        catch (Exception ex)
        {
            // A managed exception at boot is otherwise invisible: the runtime reports it to
            // dotnet's own handler, which a page without error handling drops.
            //
            // Not `{ex}`: ToString formats the message, and a message with a number or a date in it
            // goes through ICU. This build runs with InvariantGlobalization, which leaves ICU's
            // function pointers unset, so formatting an exception that way traps inside the trap
            // and the original failure never prints. The type and message are read directly and
            // the stack is walked by hand, none of which formats anything.
            Console.WriteLine($"[nfmw] boot failed: {ex.GetType().FullName}: {ex.Message}");
            for (var frame = ex.StackTrace; frame != null;)
            {
                var nl = frame.IndexOf('\n');
                Console.WriteLine("[nfmw]   " + (nl < 0 ? frame : frame[..nl]).TrimEnd('\r'));
                if (nl < 0) break;
                frame = frame[(nl + 1)..];
            }
        }
    }

    /// <summary>One frame, from the page's requestAnimationFrame loop.</summary>
    [JSExport]
    public static void Tick()
    {
        try
        {
            _game?.Tick(_clock!);
        }
        catch (Exception ex)
        {
            // The runtime hands an escaping exception to the page as `ManagedError: <message>`,
            // which for a framework exception is a resource key and no frames at all. Record it
            // once in full, then let it through: the page's loop keeps calling this, so repeating
            // the dump every frame would bury the first one.
            if (!_frameFailureLogged)
            {
                _frameFailureLogged = true;
                Console.WriteLine($"[nfmw] frame failed: {ex.GetType().FullName}: {ex.Message}");
                for (var frame = ex.StackTrace; frame != null;)
                {
                    var nl = frame.IndexOf('\n');
                    Console.WriteLine("[nfmw]   " + (nl < 0 ? frame : frame[..nl]).TrimEnd('\r'));
                    if (nl < 0) break;
                    frame = frame[(nl + 1)..];
                }
            }

            throw;
        }
    }

    private static bool _frameFailureLogged;

    /// <summary>
    /// Tells the game the page is now this size in CSS pixels.
    ///
    /// Everything that follows from this is SDL's: its emscripten backend resizes the canvas to the
    /// window size scaled by the device pixel ratio, and sends the window-resized event that
    /// propagates the new size to the swapchain, the viewport and pointer mapping. The page has to
    /// forward the size because a canvas that CSS has laid out does not resize its backing store on
    /// its own, so without this the drawable and the pointer mapping stay at the boot size while the
    /// CSS stretches the canvas to the window.
    /// </summary>
    [JSExport]
    public static void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _game?.Window.SetSize(width, height);
    }

    /// <summary>
    /// Records the page's size for the boot that follows. Called immediately before
    /// <see cref="Main"/>, because the window has to be created at the real size: the first phase
    /// reads it when it is entered to set its camera's aspect, so a window created at 1280x720 and
    /// resized afterwards keeps the wrong field of view until something else resizes the page.
    /// </summary>
    [JSExport]
    public static void SetBootSize(int width, int height)
    {
        if (width > 0 && height > 0) _bootSize = (width, height);
    }

    /// <summary>
    /// Records whether the page created a WebGPU device and parked it for
    /// <c>emscripten_webgpu_get_device</c>. Called immediately before <see cref="Main"/>, because
    /// the answer decides the renderer list and the renderer itself.
    /// </summary>
    [JSExport]
    public static void SetWebGpuAvailable(bool available) => _webGpuAvailable = available;
}
