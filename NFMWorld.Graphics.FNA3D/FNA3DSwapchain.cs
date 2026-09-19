namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DSwapchain(IntPtr device, IntPtr windowHandle, int width, int height)
    : ISwapchain
{
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;

    public void Present()
    {
        // NULL here means "use the device's default window" on the OpenGL/D3D11 drivers, which
        // cache one internally - but FNA3D's SDLGPU driver has no such fallback and requires the
        // real window handle on every call (SDLGPU_SwapBuffers casts this straight to SDL_Window*
        // with no null check), so it must always be passed explicitly.
        FNA3D_SwapBuffers(device, IntPtr.Zero, IntPtr.Zero, windowHandle);
    }

    public void RefreshSize()
    {
        FNA3D_GetBackbufferSize(device, out var w, out var h);
        Width = w;
        Height = h;
    }
}
