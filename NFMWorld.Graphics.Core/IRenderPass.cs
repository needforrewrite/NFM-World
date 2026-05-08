namespace NFMWorld.Graphics.Core;

/// <summary>
/// A render pass context. All draw commands must be issued within a render pass.
/// </summary>
public interface IRenderPass
{
    void BindGraphicsPipeline(IGraphicsPipeline pipeline);

    void SetViewport(in Viewport viewport);
    void SetScissor(in Rect scissor);
    void SetStencilReference(byte stencilRef);
    void SetBlendConstants(Color blendConstants);

    void BindVertexBuffers(uint slot, params ReadOnlySpan<BufferBinding> bindings);
    void BindVertexBuffers(params ReadOnlySpan<BufferBinding> bindings);
    void BindVertexBuffers(params ReadOnlySpan<IBuffer> buffers);

    void BindIndexBuffer(BufferBinding binding, IndexElementSize indexElementSize);

    void BindFragmentSamplers(uint slot, params ReadOnlySpan<TextureSamplerBinding> bindings);
    void BindFragmentSamplers(params ReadOnlySpan<TextureSamplerBinding> bindings);

    void DrawPrimitives(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);
    void DrawIndexedPrimitives(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);
}
