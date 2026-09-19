namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DTexture(IntPtr device, IntPtr handle, TextureDesc desc) : TrackLeaks, ITexture
{
    public IntPtr Device { get; } = device;
    public IntPtr Handle { get; private set; } = handle;
    public int Width { get; } = desc.Width;
    public int Height { get; } = desc.Height;
    public TextureFormat Format { get; } = desc.Format;

    public override void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        FNA3D_AddDisposeTexture(Device, Handle);
        Handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
