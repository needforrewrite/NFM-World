using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

internal sealed class MwCopyPass(MW.CopyPass inner) : Core.ICopyPass
{
    public MW.CopyPass Inner => inner;

    public void UploadToBuffer<T>(Core.ITransferBuffer source, Core.IBuffer destination,
        uint sourceStartElement, uint destinationStartElement, uint numElements, bool cycle) where T : unmanaged =>
        inner.UploadToBuffer<T>(
            ((MwTransferBuffer)source).Inner,
            Convert.Unwrap(destination),
            sourceStartElement, destinationStartElement, numElements, cycle);

    public void UploadToBuffer(Core.ITransferBuffer source, Core.IBuffer destination, bool cycle) =>
        inner.UploadToBuffer(((MwTransferBuffer)source).Inner, Convert.Unwrap(destination), cycle);
}
