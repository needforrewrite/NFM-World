namespace NFMWorld.Graphics.Core;

/// <summary>
/// A copy pass context for uploading data to GPU resources.
/// </summary>
public interface ICopyPass
{
    void UploadToBuffer<T>(ITransferBuffer source, IBuffer destination,
        uint sourceStartElement, uint destinationStartElement, uint numElements, bool cycle) where T : unmanaged;

    void UploadToBuffer(ITransferBuffer source, IBuffer destination, bool cycle);
}
