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
}
