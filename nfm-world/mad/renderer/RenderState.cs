using System.Diagnostics.CodeAnalysis;
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
    [AllowNull]
    public static CommandBuffer Cmd
    {
        get => field ?? ThrowAccessedOutsideFrame<CommandBuffer>(nameof(Cmd));
        set;
    }

    /// <summary>The active render pass (changes between shadow/main passes).</summary>
    [AllowNull]
    public static RenderPass Pass
    {
        get => field ?? ThrowAccessedOutsideRenderPass<RenderPass>(nameof(Pass));
        set;
    }

    /// <summary>The swapchain backbuffer texture for the current frame.</summary>
    [AllowNull]
    public static Texture Backbuffer
    {
        get => field ?? ThrowAccessedOutsideFrame<Texture>(nameof(Backbuffer));
        set;
    }

    /// <summary>The main depth texture for depth testing.</summary>
    [AllowNull]
    public static Texture MainDepthTexture
    {
        get => field ?? ThrowAccessedOutsideFrame<Texture>(nameof(MainDepthTexture));
        set;
    }

    public static void BeginDraw(CommandBuffer renderCmd, Texture backbuffer, Texture mainDepthTexture)
    {
        Cmd = renderCmd;
        Backbuffer = backbuffer;
        MainDepthTexture = mainDepthTexture;
    }

    public static void EndDraw()
    {
        Cmd = null;
        Pass = null;
        Backbuffer = null;
        MainDepthTexture = null;
    }
    
    private static T ThrowAccessedOutsideFrame<T>(string paramName)
    {
        throw new InvalidOperationException($"Accessed {nameof(RenderState)}.{paramName} outside of a frame, must call {nameof(BeginDraw)} first!");
    }

    private static T ThrowAccessedOutsideRenderPass<T>(string paramName)
    {
        throw new InvalidOperationException($"Accessed {nameof(RenderState)}.{paramName} outside of a render pass!");
    }
}
