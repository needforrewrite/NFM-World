namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DRenderTarget(
    IntPtr device,
    IntPtr colorRenderbuffer,
    IntPtr depthStencilRenderbuffer,
    ITexture colorTexture,
    ITexture? depthStencilTexture)
    : IRenderTarget
{
    public IntPtr Device { get; } = device;
    public IntPtr ColorRenderbuffer { get; private set; } = colorRenderbuffer;
    public IntPtr DepthStencilRenderbuffer { get; private set; } = depthStencilRenderbuffer;
    public ITexture ColorTexture { get; } = colorTexture;
    public ITexture? DepthStencilTexture { get; } = depthStencilTexture;

    public void Dispose()
    {
        ColorTexture.Dispose();
        DepthStencilTexture?.Dispose();
        if (ColorRenderbuffer != IntPtr.Zero)
        {
            FNA3D_AddDisposeRenderbuffer(Device, ColorRenderbuffer);
            ColorRenderbuffer = IntPtr.Zero;
        }
        if (DepthStencilRenderbuffer != IntPtr.Zero)
        {
            FNA3D_AddDisposeRenderbuffer(Device, DepthStencilRenderbuffer);
            DepthStencilRenderbuffer = IntPtr.Zero;
        }
    }
}
