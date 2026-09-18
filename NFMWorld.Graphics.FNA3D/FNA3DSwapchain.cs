using NFMWorld.Graphics.FNA3D.Native;

namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DSwapchain : ISwapchain
{
    private readonly IntPtr _device;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public FNA3DSwapchain(IntPtr device, int width, int height)
    {
        _device = device;
        Width = width;
        Height = height;
    }

    public void Present()
    {
        FNA3DNative.FNA3D_SwapBuffers(_device, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    public void RefreshSize()
    {
        FNA3DNative.FNA3D_GetBackbufferSize(_device, out var w, out var h);
        Width = w;
        Height = h;
    }
}
