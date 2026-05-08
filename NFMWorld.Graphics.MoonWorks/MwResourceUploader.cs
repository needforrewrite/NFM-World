using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

internal sealed class MwResourceUploader(MW.ResourceUploader inner) : Core.IResourceUploader
{
    public MW.ResourceUploader Inner => inner;

    // ── Buffer Operations ────────────────────────────────────────

    public Core.IBuffer CreateBuffer<T>(ReadOnlySpan<T> data, Core.BufferUsageFlags usageFlags) where T : unmanaged =>
        new MwBuffer(inner.CreateBuffer(data, Convert.ToMW(usageFlags)));

    public Core.IBuffer CreateBuffer<T>(string? name, ReadOnlySpan<T> data, Core.BufferUsageFlags usageFlags) where T : unmanaged =>
        new MwBuffer(inner.CreateBuffer(name!, data, Convert.ToMW(usageFlags)));

    public Core.IBuffer CreateBufferAndMap<T>(uint elementCount, Core.BufferUsageFlags usageFlags, out Span<T> data) where T : unmanaged =>
        new MwBuffer(inner.CreateBufferAndMap<T>(elementCount, Convert.ToMW(usageFlags), out data));

    public Core.IBuffer CreateBufferAndMap<T>(string? name, uint elementCount, Core.BufferUsageFlags usageFlags, out Span<T> data) where T : unmanaged =>
        new MwBuffer(inner.CreateBufferAndMap<T>(name!, elementCount, Convert.ToMW(usageFlags), out data));

    public void SetBufferData<T>(Core.IBuffer buffer, uint bufferOffsetInElements, ReadOnlySpan<T> data) where T : unmanaged =>
        inner.SetBufferData(Convert.Unwrap(buffer), bufferOffsetInElements, data);

    public Span<T> BeginMapBufferData<T>(Core.IBuffer buffer, uint bufferOffsetInElements, uint elementCount) where T : unmanaged =>
        inner.MapBufferData<T>(Convert.Unwrap(buffer), bufferOffsetInElements, elementCount);

    public void EndMapBufferData<T>(Core.IBuffer buffer, uint bufferOffsetInElements, ReadOnlySpan<T> data) where T : unmanaged
    {
        // MoonWorks MapBufferData returns a span that's already staged; no explicit "end" needed.
        // This is a no-op because the data was already written to the mapped span.
    }

    // ── Texture Operations ───────────────────────────────────────

    public Core.ITexture CreateTexture2D<T>(ReadOnlySpan<T> pixelData, Core.TextureFormat format, Core.TextureUsageFlags usage, uint width, uint height) where T : unmanaged =>
        new MwTexture(inner.CreateTexture2D(pixelData, Convert.ToMW(format), Convert.ToMW(usage), width, height));

    public Core.ITexture CreateTexture2D<T>(string? name, ReadOnlySpan<T> pixelData, Core.TextureFormat format, Core.TextureUsageFlags usage, uint width, uint height) where T : unmanaged =>
        new MwTexture(inner.CreateTexture2D(name!, pixelData, Convert.ToMW(format), Convert.ToMW(usage), width, height));

    public Core.ITexture CreateTexture2DFromCompressed(ReadOnlySpan<byte> compressedImageData, Core.TextureFormat format, Core.TextureUsageFlags usage) =>
        new MwTexture(inner.CreateTexture2DFromCompressed(compressedImageData, Convert.ToMW(format), Convert.ToMW(usage)));

    public Core.ITexture CreateTexture2DFromCompressed(string? name, ReadOnlySpan<byte> compressedImageData, Core.TextureFormat format, Core.TextureUsageFlags usage) =>
        new MwTexture(inner.CreateTexture2DFromCompressed(name!, compressedImageData, Convert.ToMW(format), Convert.ToMW(usage)));

    public void SetTextureData<T>(Core.ITexture texture, ReadOnlySpan<T> data, bool cycle) where T : unmanaged
    {
        var mwTex = Convert.Unwrap(texture);
        var region = new MW.TextureRegion(mwTex);
        inner.SetTextureData(region, data);
    }

    // ── Submit ───────────────────────────────────────────────────

    public void Upload() => inner.Upload();

    public void UploadAndWait() => inner.UploadAndWait();

    public void Dispose() => inner.Dispose();
}
