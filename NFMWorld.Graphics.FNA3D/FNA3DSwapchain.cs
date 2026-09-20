namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// The device's backbuffer, presented to the window. Holds the
/// <c>FNA3D_PresentationParameters</c> the device was created with, because FNA3D has no
/// width/height-only resize entry point: <c>FNA3D_ResetBackbuffer</c> takes the whole parameters
/// struct again - the same call FNA's own <c>GraphicsDevice.Reset</c> makes when the window
/// changes size (FNA3D's drivers read the window handle, formats and present interval from it,
/// and rebuild their swapchain/faux-backbuffer from the size).
/// </summary>
internal sealed class FNA3DSwapchain(IntPtr device, FNA3D_PresentationParameters parameters, IntPtr windowHandle)
    : ISwapchain
{
    private FNA3D_PresentationParameters _parameters = parameters;

    public int Width { get; private set; } = parameters.backBufferWidth;
    public int Height { get; private set; } = parameters.backBufferHeight;

    public void Resize(int width, int height)
    {
        // A minimized window reports 0x0, and the drivers reject a zero-sized swapchain - keep
        // rendering into the last good drawable until it comes back.
        if (width <= 0 || height <= 0) return;
        if (width == Width && height == Height) return;

        _parameters.backBufferWidth = width;
        _parameters.backBufferHeight = height;
        FNA3D_ResetBackbuffer(device, ref _parameters);

        // Ask the driver what it ended up with rather than assuming it took the request.
        FNA3D_GetBackbufferSize(device, out var allocatedWidth, out var allocatedHeight);
        Width = allocatedWidth;
        Height = allocatedHeight;
    }

    public void Present()
    {
        // NULL here means "use the device's default window" on the OpenGL/D3D11 drivers, which
        // cache one internally - but FNA3D's SDLGPU driver has no such fallback and requires the
        // real window handle on every call (SDLGPU_SwapBuffers casts this straight to SDL_Window*
        // with no null check), so it must always be passed explicitly.
        FNA3D_SwapBuffers(device, IntPtr.Zero, IntPtr.Zero, windowHandle);
    }
}
