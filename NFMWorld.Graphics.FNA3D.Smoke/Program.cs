// Milestone 2 smoke test: create the window through NFMWorld.Platform.SDL3's SdlWindow (resize,
// input and event-pump duties), then create an FNA3D-backed IGraphicsDevice directly against its
// native handle, clear the screen to a color each frame, and present. Drawing an actual triangle
// needs a shader/pipeline, which requires the Effect-blob design decided in Milestone 3 - so this
// only proves windowing + device creation, clearing, and presentation for now. Not wired into
// WorldGame yet (Milestone 5).
using NFMWorld.Graphics;
using NFMWorld.Graphics.FNA3D;
using NFMWorld.Platform.SDL3;
using SDL3;

const int width = 1280;
const int height = 720;

// All three built-in FNA3D drivers (SDLGPU/Vulkan, D3D11, OpenGL) work fine here - the earlier
// per-frame "Could not claim window for FNA3D renderer" (SDLGPU) and DXGI swapchain failures
// (D3D11) both traced back to FNA3DSwapchain.Present() passing FNA3D_SwapBuffers a NULL
// overrideWindowHandle. That's fine for the OpenGL/D3D11 drivers (which cache a default window
// from device creation) but not SDLGPU, which has no such fallback and casts the parameter
// straight to SDL_Window* every call - fixed by always passing the real window handle through.
// Default driver order (SDLGPU, then D3D11, then OpenGL) is left alone; set FNA3D_FORCE_DRIVER
// via SDL.SDL_SetHint or the environment variable of the same name to pin one for testing.
SDL.SDL_SetLogPriorities(SDL.SDL_LogPriority.SDL_LOG_PRIORITY_VERBOSE);

// FNA3D_PrepareWindowAttributes queries the current video driver, so SDL must already be
// initialized before calling it - SdlWindow.Create() also calls SDL_Init, but that's a harmless,
// ref-counted no-op the second time.
if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
{
    Console.Error.WriteLine($"SDL_Init failed: {SDL.SDL_GetError()}");
    return 1;
}

var windowFlags = (SDL.SDL_WindowFlags)FNA3DInterop.PrepareWindowAttributes();
using var window = SdlWindow.Create("NFMWorld.Graphics.FNA3D smoke test", width, height, extraFlags: windowFlags);

using var device = FNA3DGraphicsDevice.Create(window.Handle, width, height, vsync: true);

window.Resized += (w, h) => Console.WriteLine($"Window resized to {w}x{h}");
window.KeyChanged += (key, down, repeat) =>
{
    if (down && !repeat)
        Console.WriteLine($"Key {key} down");
};
window.TextInput += c => Console.Write(c);

var t = 0f;
while (!window.ShouldQuit)
{
    window.PumpEvents();

    t += 0.01f;
    var cb = device.AcquireCommandBuffer();
    cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.5f + 0.5f * MathF.Sin(t), 0.2f, 0.4f));
    device.Submit(cb);
    device.Swapchain.Present();
}

return 0;
