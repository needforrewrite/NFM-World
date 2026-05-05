using MoonWorks.Graphics;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world;

/// <summary>
/// Static render context set by the main loop and Scene before calling render methods.
/// Allows renderers to access the current CommandBuffer and RenderPass
/// without changing all render interface signatures.
/// </summary>
public static class RenderState
{
    /// <summary>The command buffer for the current frame.</summary>
    public static CommandBuffer Cmd;

    /// <summary>The active render pass (changes between shadow/main passes).</summary>
    public static RenderPass Pass;

    /// <summary>The swapchain backbuffer texture for the current frame.</summary>
    public static Texture Backbuffer;

    /// <summary>The main depth texture for depth testing.</summary>
    public static Texture MainDepthTexture;

    /// <summary>Set to true when Scene has rendered 3D content to the backbuffer this frame.</summary>
    public static bool SceneRenderedThisFrame;

    private struct PendingUpload
    {
        public TransferBuffer Source;
        public GpuBuffer Destination;
        public uint DestinationOffset;
        public uint Size;
    }

    private static readonly List<PendingUpload> PendingUploads = new();
    private static readonly List<TransferBuffer> PendingTransferBuffers = new();

    /// <summary>
    /// Enqueue a buffer upload to be flushed in a single CopyPass before rendering.
    /// The transfer buffer will be disposed after flushing.
    /// </summary>
    public static void EnqueueUpload(TransferBuffer source, GpuBuffer destination, uint size)
    {
        PendingUploads.Add(new PendingUpload
        {
            Source = source,
            Destination = destination,
            DestinationOffset = 0,
            Size = size
        });
        PendingTransferBuffers.Add(source);
    }

    /// <summary>
    /// Flush all pending uploads in a single CopyPass on the given command buffer.
    /// </summary>
    public static void FlushUploads(CommandBuffer cmd)
    {
        if (PendingUploads.Count == 0) return;

        var copyPass = cmd.BeginCopyPass();
        foreach (var upload in PendingUploads)
        {
            copyPass.UploadToBuffer(
                new TransferBufferLocation(upload.Source, 0),
                new BufferRegion(upload.Destination, upload.DestinationOffset, upload.Size),
                true);
        }
        cmd.EndCopyPass(copyPass);

        foreach (var tb in PendingTransferBuffers)
            tb.Dispose();

        PendingUploads.Clear();
        PendingTransferBuffers.Clear();
    }
}
