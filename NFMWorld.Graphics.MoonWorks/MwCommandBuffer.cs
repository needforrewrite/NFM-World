using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

internal sealed class MwCommandBuffer(MW.CommandBuffer inner) : Core.ICommandBuffer
{
    public MW.CommandBuffer Inner => inner;

    public Core.ITexture? AcquireSwapchainTexture(Core.IWindow window)
    {
        var tex = inner.AcquireSwapchainTexture(Convert.Unwrap(window));
        return tex != null ? new MwTexture(tex) : null;
    }

    public void PushVertexUniformData<T>(in T uniforms, uint slot = 0) where T : unmanaged =>
        inner.PushVertexUniformData(uniforms, slot);

    public void PushFragmentUniformData<T>(in T uniforms, uint slot = 0) where T : unmanaged =>
        inner.PushFragmentUniformData(uniforms, slot);

    public Core.IRenderPass BeginRenderPass(params ReadOnlySpan<Core.ColorTargetInfo> colorTargetInfos)
    {
        Span<MW.ColorTargetInfo> mw = stackalloc MW.ColorTargetInfo[colorTargetInfos.Length];
        for (int i = 0; i < colorTargetInfos.Length; i++)
        {
            ref readonly var c = ref colorTargetInfos[i];
            mw[i] = new MW.ColorTargetInfo
            {
                Texture = Convert.Unwrap(c.Texture),
                MipLevel = c.MipLevel,
                LayerOrDepthPlane = c.LayerOrDepthPlane,
                ClearColor = new MW.Color(c.ClearColor.R, c.ClearColor.G, c.ClearColor.B, c.ClearColor.A),
                LoadOp = Convert.ToMW(c.LoadOp),
                StoreOp = Convert.ToMW(c.StoreOp),
                Cycle = c.Cycle,
            };
        }
        return new MwRenderPass(inner.BeginRenderPass(mw));
    }

    public Core.IRenderPass BeginRenderPass(in Core.DepthStencilTargetInfo depthStencilTargetInfo, params ReadOnlySpan<Core.ColorTargetInfo> colorTargetInfos)
    {
        var ds = new MW.DepthStencilTargetInfo
        {
            Texture = Convert.Unwrap(depthStencilTargetInfo.Texture),
            ClearDepth = depthStencilTargetInfo.ClearDepth,
            LoadOp = Convert.ToMW(depthStencilTargetInfo.LoadOp),
            StoreOp = Convert.ToMW(depthStencilTargetInfo.StoreOp),
            StencilLoadOp = Convert.ToMW(depthStencilTargetInfo.StencilLoadOp),
            StencilStoreOp = Convert.ToMW(depthStencilTargetInfo.StencilStoreOp),
            Cycle = depthStencilTargetInfo.Cycle,
            ClearStencil = depthStencilTargetInfo.ClearStencil,
        };

        Span<MW.ColorTargetInfo> mw = stackalloc MW.ColorTargetInfo[colorTargetInfos.Length];
        for (int i = 0; i < colorTargetInfos.Length; i++)
        {
            ref readonly var c = ref colorTargetInfos[i];
            mw[i] = new MW.ColorTargetInfo
            {
                Texture = Convert.Unwrap(c.Texture),
                MipLevel = c.MipLevel,
                LayerOrDepthPlane = c.LayerOrDepthPlane,
                ClearColor = new MW.Color(c.ClearColor.R, c.ClearColor.G, c.ClearColor.B, c.ClearColor.A),
                LoadOp = Convert.ToMW(c.LoadOp),
                StoreOp = Convert.ToMW(c.StoreOp),
                Cycle = c.Cycle,
            };
        }
        return new MwRenderPass(inner.BeginRenderPass(ds, mw));
    }

    public void EndRenderPass(Core.IRenderPass renderPass) =>
        inner.EndRenderPass(((MwRenderPass)renderPass).Inner);

    public Core.ICopyPass BeginCopyPass() =>
        new MwCopyPass(inner.BeginCopyPass());

    public void EndCopyPass(Core.ICopyPass copyPass) =>
        inner.EndCopyPass(((MwCopyPass)copyPass).Inner);
}
