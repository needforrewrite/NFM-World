using NFMWorld.Platform.SDL3;
using SDL3;

// ── 1. What the settings menu's resolution list will contain from the display query ──
var modes = SdlWindow.GetFullscreenDisplayModes();
Console.WriteLine($"modes: {modes.Count}, deduplicated: {modes.Distinct().Count() == modes.Count}");
foreach (var (w, h) in modes.Take(4))
    Console.WriteLine($"  {w} x {h}");

// ── 2. HiDPI facts: is the window's drawable (pixel) size the same as its logical size? ──
// A hidden window so nothing appears on screen; sizes are still real. SdlWindow.Create can't
// pass SDL_WINDOW_HIDDEN (no flag for it), so this goes through SDL directly.
if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
    throw new InvalidOperationException(SDL.SDL_GetError());

var primary = SDL.SDL_GetPrimaryDisplay();
Console.WriteLine($"primary display: {SDL.SDL_GetDisplayName(primary)}");
Console.WriteLine($"  content scale: {SDL.SDL_GetDisplayContentScale(primary)}");

var hidden = SDL.SDL_CreateWindow("hidpi probe", 1280, 720,
    SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN | SDL.SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY);
if (hidden == IntPtr.Zero)
    throw new InvalidOperationException(SDL.SDL_GetError());

SDL.SDL_GetWindowSize(hidden, out var logicalW, out var logicalH);
SDL.SDL_GetWindowSizeInPixels(hidden, out var pixelW, out var pixelH);
Console.WriteLine($"hidden window 1280x720: logical {logicalW}x{logicalH}, pixels {pixelW}x{pixelH}");
Console.WriteLine($"  window content scale: {SDL.SDL_GetWindowDisplayScale(hidden)}");
Console.WriteLine(pixelW == logicalW && pixelH == logicalH
    ? "  => 1:1, window size == drawable size (no HiDPI scaling to handle)"
    : "  => HiDPI! drawable size differs from window size - backbuffer must use pixels");
SDL.SDL_DestroyWindow(hidden);
SDL.SDL_Quit();
