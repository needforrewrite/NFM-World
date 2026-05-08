using Core = NFMWorld.Graphics.Core;
using MW = MoonWorks.Graphics;

namespace NFMWorld.Graphics.MoonWorks;

internal sealed class MwRenderPass(MW.RenderPass inner) : Core.IRenderPass
{
    public MW.RenderPass Inner => inner;

    public void BindGraphicsPipeline(Core.IGraphicsPipeline pipeline) =>
        inner.BindGraphicsPipeline(Convert.Unwrap(pipeline));

    public void SetViewport(in Core.Viewport viewport) =>
        inner.SetViewport(new MW.Viewport
        {
            X = viewport.X, Y = viewport.Y,
            W = viewport.W, H = viewport.H,
            MinDepth = viewport.MinDepth, MaxDepth = viewport.MaxDepth,
        });

    public void SetScissor(in Core.Rect scissor) =>
        inner.SetScissor(new MW.Rect(scissor.X, scissor.Y, scissor.W, scissor.H));

    public void SetStencilReference(byte stencilRef) =>
        inner.SetStencilReference(stencilRef);

    public void SetBlendConstants(Core.Color blendConstants) =>
        inner.SetBlendConstants(new MW.Color(blendConstants.R, blendConstants.G, blendConstants.B, blendConstants.A));

    public void BindVertexBuffers(uint slot, params ReadOnlySpan<Core.BufferBinding> bindings)
    {
        Span<MW.BufferBinding> mw = stackalloc MW.BufferBinding[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
            mw[i] = new MW.BufferBinding(Convert.Unwrap(bindings[i].Buffer), bindings[i].Offset);
        inner.BindVertexBuffers(slot, mw);
    }

    public void BindVertexBuffers(params ReadOnlySpan<Core.BufferBinding> bindings) =>
        BindVertexBuffers(0, bindings);

    public void BindVertexBuffers(params ReadOnlySpan<Core.IBuffer> buffers)
    {
        var mw = new MW.Buffer[buffers.Length];
        for (int i = 0; i < buffers.Length; i++)
            mw[i] = Convert.Unwrap(buffers[i]);
        inner.BindVertexBuffers(mw);
    }

    public void BindIndexBuffer(Core.BufferBinding binding, Core.IndexElementSize indexElementSize) =>
        inner.BindIndexBuffer(
            new MW.BufferBinding(Convert.Unwrap(binding.Buffer), binding.Offset),
            Convert.ToMW(indexElementSize));

    public void BindFragmentSamplers(uint slot, params ReadOnlySpan<Core.TextureSamplerBinding> bindings)
    {
        Span<MW.TextureSamplerBinding> mw = stackalloc MW.TextureSamplerBinding[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
            mw[i] = new MW.TextureSamplerBinding(Convert.Unwrap(bindings[i].Texture), Convert.Unwrap(bindings[i].Sampler));
        inner.BindFragmentSamplers(slot, mw);
    }

    public void BindFragmentSamplers(params ReadOnlySpan<Core.TextureSamplerBinding> bindings) =>
        BindFragmentSamplers(0, bindings);

    public void DrawPrimitives(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance) =>
        inner.DrawPrimitives(vertexCount, instanceCount, firstVertex, firstInstance);

    public void DrawIndexedPrimitives(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance) =>
        inner.DrawIndexedPrimitives(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
}
