using NFMWorld.Graphics;

namespace NFMWorld.Util;

/// <summary>
/// Decodes a common image format (PNG/JPG/BMP/etc.) from a stream into an <see cref="ITexture"/>,
/// via <see cref="NFMWorld.FNA3D.FNA3D.ReadImageStream"/> - the same stb_image-backed native FNA3D
/// export FNA's own <c>Texture2D.FromStream</c> uses under the hood. Replaces that XNA method for
/// <see cref="AbstractionNvgRenderer"/>'s consumers (<c>WorldClientBackend.NvgGraphics.LoadImage</c>).
/// DDS is not supported here - FNA3D_Image_Load only wraps stb_image, which has no DDS decoder.
/// See <see cref="DdsReader"/> for that format instead.
/// </summary>
public static class TextureLoader
{
    public static unsafe ITexture LoadFromStream(IGraphicsDevice device, Stream stream)
    {
        var pixels = NFMWorld.FNA3D.FNA3D.ReadImageStream(stream, out var width, out var height, out var len);
        if (pixels == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to decode image stream (unsupported format or corrupt data).");
        }

        try
        {
            var span = new ReadOnlySpan<byte>((void*)pixels, len);
            return device.CreateTexture(new TextureDesc(width, height, TextureFormat.Rgba8), span);
        }
        finally
        {
            NFMWorld.FNA3D.FNA3D.FNA3D_Image_Free(pixels);
        }
    }
}
