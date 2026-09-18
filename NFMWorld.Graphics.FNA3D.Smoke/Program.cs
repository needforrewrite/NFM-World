// Milestone 1 smoke test: create an SDL3 window, create an FNA3D-backed IGraphicsDevice against
// it directly (not through FNA's own Game/GraphicsDeviceManager), clear the screen to a color
// each frame, and present. Drawing an actual triangle needs a shader/pipeline, which requires
// the Effect-blob design decided in Milestone 3 - so this only proves device creation, clearing,
// and presentation for now. Not wired into WorldGame; NFMWorld.Platform.SDL3 (Milestone 2) will
// take over window/event-pump duties once it exists.
using NFMWorld.Graphics;
using NFMWorld.Graphics.FNA3D;
using SDL3;

const int width = 1280;
const int height = 720;

if (!SDL.Init(SDL.InitFlags.Video))
{
    Console.Error.WriteLine($"SDL Init failed: {SDL.GetError()}");
    return 1;
}

// Default driver selection (SDL_GPU/Vulkan) fails to claim this window in this environment, and
// D3D11 hits a DXGI swapchain-creation error worth investigating separately later; OpenGL is the
// one that verifiably works end-to-end here, so it's forced for this smoke test. Driver
// selection UX belongs to NFMWorld.Graphics.FNA3D's device-creation code per the plan
// (Milestone 2), not here.
SDL.SetHint("FNA3D_FORCE_DRIVER", "OpenGL");

var windowFlags = (SDL.WindowFlags)FNA3DInterop.PrepareWindowAttributes();
var window = SDL.CreateWindow("NFMWorld.Graphics.FNA3D smoke test", width, height, windowFlags);
if (window == IntPtr.Zero)
{
    Console.Error.WriteLine($"SDL CreateWindow failed: {SDL.GetError()}");
    return 1;
}

using var device = FNA3DGraphicsDevice.Create(window, width, height, vsync: true);

var running = true;
var t = 0f;
while (running)
{
    while (SDL.PollEvent(out var e))
    {
        if (e.Type == (uint)SDL.EventType.Quit)
            running = false;
    }

    t += 0.01f;
    var cb = device.AcquireCommandBuffer();
    cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.5f + 0.5f * MathF.Sin(t), 0.2f, 0.4f));
    device.Submit(cb);
    device.Swapchain.Present();
}

SDL.DestroyWindow(window);
SDL.Quit();
return 0;
