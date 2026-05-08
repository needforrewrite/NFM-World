using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

internal sealed class MwTexture(MW.Texture inner) : Core.ITexture
{
    public MW.Texture Inner => inner;
    public uint Width => inner.Width;
    public uint Height => inner.Height;
    public Core.TextureFormat Format => Convert.ToCore(inner.Format);
    public Core.TextureUsageFlags UsageFlags => (Core.TextureUsageFlags)(uint)inner.UsageFlags;
    public void Dispose() => inner.Dispose();
}

internal sealed class MwBuffer(MW.Buffer inner) : Core.IBuffer
{
    public MW.Buffer Inner => inner;
    public uint Size => inner.Size;
    public Core.BufferUsageFlags UsageFlags => (Core.BufferUsageFlags)(uint)inner.UsageFlags;
    public void Dispose() => inner.Dispose();
}

internal sealed class MwShader(MW.Shader inner) : Core.IShader
{
    public MW.Shader Inner => inner;
    public void Dispose() => inner.Dispose();
}

internal sealed class MwSampler(MW.Sampler inner) : Core.ISampler
{
    public MW.Sampler Inner => inner;
    public void Dispose() => inner.Dispose();
}

internal sealed class MwGraphicsPipeline(MW.GraphicsPipeline inner) : Core.IGraphicsPipeline
{
    public MW.GraphicsPipeline Inner => inner;
    public void Dispose() => inner.Dispose();
}

internal sealed class MwTransferBuffer(MW.TransferBuffer inner) : Core.ITransferBuffer
{
    public MW.TransferBuffer Inner => inner;
    public uint Size => inner.Size;
    public void Map(bool cycle) => inner.Map(cycle);
    public Span<T> Map<T>(bool cycle, uint offsetInBytes = 0) where T : unmanaged => inner.Map<T>(cycle, offsetInBytes);
    public Span<T> MappedSpan<T>(uint offsetInBytes = 0) where T : unmanaged => inner.MappedSpan<T>(offsetInBytes);
    public void Unmap() => inner.Unmap();
    public void Dispose() => inner.Dispose();
}

internal sealed class MwFence(MW.Fence inner) : Core.IFence
{
    public MW.Fence Inner => inner;
}

internal sealed class MwWindow(global::MoonWorks.Window inner) : Core.IWindow
{
    public global::MoonWorks.Window Inner => inner;
    public uint Width => inner.Width;
    public uint Height => inner.Height;
    public Core.TextureFormat SwapchainFormat => Convert.ToCore(inner.SwapchainFormat);
    public void RegisterSizeChangeCallback(Action<uint, uint> callback) =>
        inner.RegisterSizeChangeCallback(callback);
}
