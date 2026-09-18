using NFMWorld.Graphics.FNA3D.Native;

namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DSwapchain : ISwapchain
{
    private readonly IntPtr _device;
    private readonly IntPtr _windowHandle;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public FNA3DSwapchain(IntPtr device, IntPtr windowHandle, int width, int height)
    {
        _device = device;
        _windowHandle = windowHandle;
        Width = width;
        Height = height;
    }

    public void Present()
    {
        // NULL here means "use the device's default window" on the OpenGL/D3D11 drivers, which
        // cache one internally - but FNA3D's SDLGPU driver has no such fallback and requires the
        // real window handle on every call (SDLGPU_SwapBuffers casts this straight to SDL_Window*
        // with no null check), so it must always be passed explicitly.
        FNA3DNative.FNA3D_SwapBuffers(_device, IntPtr.Zero, IntPtr.Zero, _windowHandle);
    }

    public void RefreshSize()
    {
        FNA3DNative.FNA3D_GetBackbufferSize(_device, out var w, out var h);
        Width = w;
        Height = h;
    }
}
