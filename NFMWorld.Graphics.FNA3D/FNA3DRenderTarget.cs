namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DRenderTarget : IRenderTarget
{
    public IntPtr Device { get; }
    public IntPtr ColorRenderbuffer { get; private set; }
    public IntPtr DepthStencilRenderbuffer { get; private set; }
    public ITexture ColorTexture { get; }
    public ITexture? DepthStencilTexture { get; }

    public FNA3DRenderTarget(IntPtr device, IntPtr colorRenderbuffer, IntPtr depthStencilRenderbuffer, ITexture colorTexture, ITexture? depthStencilTexture)
    {
        Device = device;
        ColorRenderbuffer = colorRenderbuffer;
        DepthStencilRenderbuffer = depthStencilRenderbuffer;
        ColorTexture = colorTexture;
        DepthStencilTexture = depthStencilTexture;
    }

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
