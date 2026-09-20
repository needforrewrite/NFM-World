using System;
using System.IO;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// PNG encoding for CPU-side pixel buffers, wrapping FNA3D's stb_image_write bindings - the same
/// functions FNA's own <c>Texture2D.SaveAsPng</c> extension calls
/// (<see cref="FNA3D.WritePNGStream"/> does the callback-to-Stream marshalling).
/// </summary>
/// <remarks>
/// Deliberately not part of <c>IGraphicsDevice</c>: encoding pixels is not a device operation, and
/// <c>FNA3D_Image_SavePNG</c> takes no device handle - it is a static entry point over FNA3D's own
/// image helper. Promote this to a real abstraction type (e.g. <c>IImageCodec</c>) if a second
/// backend ever needs a different encoder; the one consumer today (the stage editor's top-down
/// export) only needs "write these RGBA8 bytes as a PNG".
/// </remarks>
public static class FNA3DImageCodec
{
    /// <summary>
    /// Encodes tightly packed 32-bit RGBA8 pixels (<paramref name="srcWidth"/> x
    /// <paramref name="srcHeight"/>, top row first) into <paramref name="stream"/> as a PNG,
    /// resampled to <paramref name="dstWidth"/> x <paramref name="dstHeight"/> (pass the source
    /// dimensions to write at native size).
    /// </summary>
    public static unsafe void WritePng(
        Stream stream,
        int srcWidth,
        int srcHeight,
        int dstWidth,
        int dstHeight,
        ReadOnlySpan<byte> rgbaPixels)
    {
        // FNA3D's writer copies through an unmanaged context handle, so the caller's span has to
        // stay pinned for the duration of the encode. WritePNGStream comes in through this
        // project's `global using static NFMWorld.FNA3D.FNA3D;` - spelling it out as
        // FNA3D.WritePNGStream would bind "FNA3D" to this file's own namespace instead.
        fixed (byte* ptr = rgbaPixels)
        {
            WritePNGStream(stream, srcWidth, srcHeight, dstWidth, dstHeight, (IntPtr)ptr);
        }
    }
}
