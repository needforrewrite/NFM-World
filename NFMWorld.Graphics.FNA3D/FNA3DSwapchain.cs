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

    /// <summary>
    /// The count the driver reports for the drawable that actually exists, not the field in
    /// <see cref="_parameters"/> - drivers may clamp or normalize what was requested.
    /// </summary>
    public int MultiSampleCount { get; private set; } = FNA3D_GetBackbufferMultiSampleCount(device);

    /// <summary>
    /// The last count that was asked for, which is deliberately *not* <see cref="MultiSampleCount"/>:
    /// when the hardware clamps a request, comparing future requests against the clamped allocation
    /// would report a difference forever and rebuild the drawable every frame. Requests are compared
    /// against requests; only the allocation is reported back to callers.
    /// </summary>
    private int _requestedMultiSampleCount = parameters.multiSampleCount;

    /// <summary>
    /// False: <c>FNA3D_ResetBackbuffer</c> rebuilds the drawable with the new count in place.
    ///
    /// The count is genuinely applied mid-session here, which is the whole difference from the two
    /// GL backends - their sample count is a property of a window pixel format that only a new
    /// context can change.
    /// </summary>
    public bool MultiSampleChangeRequiresRestart => false;

    public void Resize(int width, int height, int multiSampleCount = 0)
    {
        // A minimized window reports 0x0, and the drivers reject a zero-sized swapchain - keep
        // rendering into the last good drawable until it comes back.
        if (width <= 0 || height <= 0) return;

        // FNA only ever handed FNA3D powers of two (MathHelper.ClosestMSAAPower), so a
        // non-power-of-two request is a caller bug worth failing loudly rather than silently rounding.
        if (multiSampleCount != 0 && (multiSampleCount & (multiSampleCount - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(multiSampleCount), multiSampleCount, "Multisample count must be 0, 1, or a power of two.");

        // See ISwapchain.Resize: 0 means "leave the count alone", and 1 is the UI's "MSAA 1x",
        // which means off. Resolved before the early-out so an unchanged request stays cheap.
        var requested = multiSampleCount switch
        {
            0 => _requestedMultiSampleCount,
            1 => 0,
            _ => multiSampleCount,
        };

        if (width == Width && height == Height && requested == _requestedMultiSampleCount) return;

        // Ask the device what it supports before committing: the drivers clamp to the hardware's
        // maximum (D3D11 walks down from the request, OpenGL/SDLGPU return min(max, request)).
        var allocatedSampleCount = FNA3D_GetMaxMultiSampleCount(device, _parameters.backBufferFormat, requested);

        _parameters.backBufferWidth = width;
        _parameters.backBufferHeight = height;
        _parameters.multiSampleCount = allocatedSampleCount;
        FNA3D_ResetBackbuffer(device, ref _parameters);
        _requestedMultiSampleCount = requested;

        // Ask the driver what it ended up with rather than assuming it took the request.
        FNA3D_GetBackbufferSize(device, out var allocatedWidth, out var allocatedHeight);
        Width = allocatedWidth;
        Height = allocatedHeight;
        MultiSampleCount = FNA3D_GetBackbufferMultiSampleCount(device);
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
