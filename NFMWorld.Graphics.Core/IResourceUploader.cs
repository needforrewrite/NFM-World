namespace NFMWorld.Graphics.Core;

/// <summary>
/// Convenience for batching resource uploads. Implementations buffer data in a
/// transfer buffer since then submit a CopyPass when <see cref="Upload"/> is called.
/// </summary>
public interface IResourceUploader : IDisposable
{
    // ── Buffer Operations ────────────────────────────────────────

    /// <summary>Creates a new GPU buffer and stages data for upload.</summary>
    IBuffer CreateBuffer<T>(ReadOnlySpan<T> data, BufferUsageFlags usageFlags) where T : unmanaged;

    /// <summary>Creates a new GPU buffer and stages data for upload.</summary>
    IBuffer CreateBuffer<T>(string? name, ReadOnlySpan<T> data, BufferUsageFlags usageFlags) where T : unmanaged;

    /// <summary>Creates a new GPU buffer and returns a writable span into the staging area.</summary>
    IBuffer CreateBufferAndMap<T>(uint elementCount, BufferUsageFlags usageFlags, out Span<T> data) where T : unmanaged;

    /// <summary>Creates a new GPU buffer and returns a writable span into the staging area.</summary>
    IBuffer CreateBufferAndMap<T>(string? name, uint elementCount, BufferUsageFlags usageFlags, out Span<T> data) where T : unmanaged;

    /// <summary>Stages data to be uploaded into an existing buffer.</summary>
    void SetBufferData<T>(IBuffer buffer, uint bufferOffsetInElements, ReadOnlySpan<T> data) where T : unmanaged;

    /// <summary>Maps a region of the staging buffer for writing into an existing buffer.</summary>
    /// <remarks>
    /// Must call <see cref="EndMapBufferData{T}"/> after writing to the returned span.
    /// Do not call any other methods in this instance until <see cref="EndMapBufferData{T}"/> is called.
    /// </remarks>
    Span<T> BeginMapBufferData<T>(IBuffer buffer, uint bufferOffsetInElements, uint elementCount) where T : unmanaged;
    void EndMapBufferData<T>(IBuffer buffer, uint bufferOffsetInElements, ReadOnlySpan<T> data) where T : unmanaged;

    // ── Texture Operations ───────────────────────────────────────

    /// <summary>Creates a 2D texture and stages pixel data for upload.</summary>
    ITexture CreateTexture2D<T>(ReadOnlySpan<T> pixelData, TextureFormat format, TextureUsageFlags usage, uint width, uint height) where T : unmanaged;

    /// <summary>Creates a 2D texture and stages pixel data for upload.</summary>
    ITexture CreateTexture2D<T>(string? name, ReadOnlySpan<T> pixelData, TextureFormat format, TextureUsageFlags usage, uint width, uint height) where T : unmanaged;

    /// <summary>Creates a 2D texture from compressed image data (PNG, etc.) and stages for upload.</summary>
    ITexture CreateTexture2DFromCompressed(ReadOnlySpan<byte> compressedImageData, TextureFormat format, TextureUsageFlags usage);

    /// <summary>Creates a 2D texture from compressed image data (PNG, etc.) and stages for upload.</summary>
    ITexture CreateTexture2DFromCompressed(string? name, ReadOnlySpan<byte> compressedImageData, TextureFormat format, TextureUsageFlags usage);

    /// <summary>Stages pixel data to be uploaded into an existing texture.</summary>
    void SetTextureData<T>(ITexture texture, ReadOnlySpan<T> data, bool cycle) where T : unmanaged;

    // ── Submit ───────────────────────────────────────────────────

    /// <summary>Submits all pending uploads to the GPU.</summary>
    void Upload();

    /// <summary>Submits all pending uploads and blocks until complete.</summary>
    void UploadAndWait();
}
