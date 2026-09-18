using NFMWorld.Graphics.FNA3D.Native;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Small set of FNA3D queries a windowing layer (NFMWorld.Platform.SDL3, or this project's own
/// smoke test before that exists) needs before creating its window - kept separate from
/// <see cref="FNA3DGraphicsDevice"/> since these calls don't require a device to exist yet.
/// </summary>
public static class FNA3DInterop
{
    /// <summary>
    /// Native window-creation flags FNA3D needs set (e.g. requesting a Vulkan-capable window on
    /// some backends) before the window is created - must be OR'd into the platform layer's own
    /// SDL_WindowFlags before calling SDL_CreateWindow.
    /// </summary>
    public static uint PrepareWindowAttributes() => FNA3DNative.FNA3D_PrepareWindowAttributes();
}
