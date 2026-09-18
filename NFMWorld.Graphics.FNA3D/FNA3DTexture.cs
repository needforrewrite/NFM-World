using NFMWorld.Graphics.FNA3D.Native;

namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DTexture : ITexture
{
    public IntPtr Device { get; }
    public IntPtr Handle { get; private set; }
    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }

    public FNA3DTexture(IntPtr device, IntPtr handle, TextureDesc desc)
    {
        Device = device;
        Handle = handle;
        Width = desc.Width;
        Height = desc.Height;
        Format = desc.Format;
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        FNA3DNative.FNA3D_AddDisposeTexture(Device, Handle);
        Handle = IntPtr.Zero;
    }
}
