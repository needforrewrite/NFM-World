namespace NFMWorld.Graphics.Core;

/// <summary>
/// A staging buffer for transferring data between CPU and GPU.
/// </summary>
public interface ITransferBuffer : IDisposable
{
    uint Size { get; }

    void Map(bool cycle);
    Span<T> Map<T>(bool cycle, uint offsetInBytes = 0) where T : unmanaged;
    Span<T> MappedSpan<T>(uint offsetInBytes = 0) where T : unmanaged;
    void Unmap();
}
