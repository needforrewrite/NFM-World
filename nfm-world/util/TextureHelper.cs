using MoonWorks.Graphics;
using StbImageSharp;
using GpuBuffer = MoonWorks.Graphics.Buffer;
using GpuCommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace nfm_world;

/// <summary>
/// Helper for loading textures from streams/files into MoonWorks GPU textures.
/// Replaces FNA's Texture2D.FromStream().
/// </summary>
public static class TextureHelper
{
    public static Texture LoadTexture(GraphicsDevice device, Stream stream)
    {
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return UploadPixels(device, image.Data, (uint)image.Width, (uint)image.Height);
    }

    public static Texture LoadTexture(GraphicsDevice device, ReadOnlySpan<byte> fileData)
    {
        var image = ImageResult.FromMemory(fileData.ToArray(), ColorComponents.RedGreenBlueAlpha);
        return UploadPixels(device, image.Data, (uint)image.Width, (uint)image.Height);
    }

    private static Texture UploadPixels(GraphicsDevice device, byte[] pixels, uint width, uint height)
    {
        var texture = Texture.Create2D(device, width, height,
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler);

        var transfer = TransferBuffer.Create<byte>(device, TransferBufferUsage.Upload, (uint)pixels.Length);
        var mapped = transfer.Map<byte>(false);
        pixels.CopyTo(mapped);
        transfer.Unmap();

        var cmd = device.AcquireCommandBuffer();
        var copyPass = cmd.BeginCopyPass();
        copyPass.UploadToTexture(
            new TextureTransferInfo(transfer),
            new TextureRegion(texture),
            false);
        cmd.EndCopyPass(copyPass);
        device.Submit(cmd);

        transfer.Dispose();

        return texture;
    }
}
