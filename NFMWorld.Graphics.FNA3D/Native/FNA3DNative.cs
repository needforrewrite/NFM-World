using System.Runtime.InteropServices;

namespace NFMWorld.Graphics.FNA3D.Native;

/// <summary>
/// Direct P/Invoke bindings for FNA3D's native C ABI (FNA/lib/FNA3D/include/FNA3D.h), extracted
/// and adapted from FNA's own FNA3D.cs wrapper. Unlike that wrapper, every signature here uses
/// the native FNA3D_* enums/structs (see FNA3DEnums.cs/FNA3DStructs.cs) instead of
/// Microsoft.Xna.Framework types, so this binding carries no dependency on FNA's C# layer or on
/// FNA.Math. Scoped for now to what a graphics-abstraction device implementation needs
/// (versioning, device lifecycle, presentation, drawing, render state, render targets, textures,
/// vertex/index buffers, effects, feature queries) - image codec helpers and GPU query objects
/// are not yet ported since nothing in NFMWorld.Graphics.FNA3D needs them.
/// </summary>
internal static class FNA3DNative
{
    private const string NativeLibName = "FNA3D";

    // ── Versioning ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint FNA3D_LinkedVersion();

    // ── Driver Functions ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint FNA3D_PrepareWindowAttributes();

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_GetDrawableSize(IntPtr window, out int w, out int h);

    // ── Init/Quit ──

    /// <summary>Returns an FNA3D_Device*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_CreateDevice(ref FNA3D_PresentationParameters presentationParameters, byte debugMode);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_DestroyDevice(IntPtr device);

    // ── Presentation ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SwapBuffers(IntPtr device, IntPtr sourceRectangle, IntPtr destinationRectangle, IntPtr overrideWindowHandle);

    // ── Drawing ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_Clear(IntPtr device, FNA3D_ClearOptions options, ref FNA3D_Vec4 color, float depth, int stencil);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_DrawIndexedPrimitives(
        IntPtr device,
        FNA3D_PrimitiveType primitiveType,
        int baseVertex,
        int minVertexIndex,
        int numVertices,
        int startIndex,
        int primitiveCount,
        IntPtr indices, /* FNA3D_Buffer* */
        FNA3D_IndexElementSize indexElementSize);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_DrawInstancedPrimitives(
        IntPtr device,
        FNA3D_PrimitiveType primitiveType,
        int baseVertex,
        int minVertexIndex,
        int numVertices,
        int startIndex,
        int primitiveCount,
        int instanceCount,
        IntPtr indices, /* FNA3D_Buffer* */
        FNA3D_IndexElementSize indexElementSize);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_DrawPrimitives(IntPtr device, FNA3D_PrimitiveType primitiveType, int vertexStart, int primitiveCount);

    // ── Mutable Render States ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetViewport(IntPtr device, ref FNA3D_Viewport viewport);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetScissorRect(IntPtr device, ref FNA3D_Rect scissor);

    // ── Immutable Render States ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetBlendState(IntPtr device, ref FNA3D_BlendState blendState);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetDepthStencilState(IntPtr device, ref FNA3D_DepthStencilState depthStencilState);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_ApplyRasterizerState(IntPtr device, ref FNA3D_RasterizerState rasterizerState);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_VerifySampler(IntPtr device, int index, IntPtr texture, ref FNA3D_SamplerState sampler);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void FNA3D_ApplyVertexBufferBindings(
        IntPtr device,
        FNA3D_VertexBufferBinding* bindings,
        int numBindings,
        byte bindingsUpdated,
        int baseVertex);

    // ── Render Targets ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void FNA3D_SetRenderTargets(
        IntPtr device,
        FNA3D_RenderTargetBinding* renderTargets,
        int numRenderTargets,
        IntPtr depthStencilBuffer, /* FNA3D_Renderbuffer* */
        FNA3D_DepthFormat depthFormat,
        byte preserveDepthStencilContents);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_ResolveTarget(IntPtr device, ref FNA3D_RenderTargetBinding target);

    // ── Backbuffer Functions ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_ResetBackbuffer(IntPtr device, ref FNA3D_PresentationParameters presentationParameters);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_GetBackbufferSize(IntPtr device, out int w, out int h);

    // ── Textures ──

    /// <summary>Returns an FNA3D_Texture*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_CreateTexture2D(IntPtr device, FNA3D_SurfaceFormat format, int width, int height, int levelCount, byte isRenderTarget);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_AddDisposeTexture(IntPtr device, IntPtr texture);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetTextureData2D(IntPtr device, IntPtr texture, int x, int y, int w, int h, int level, IntPtr data, int dataLength);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_GetTextureData2D(IntPtr device, IntPtr texture, int x, int y, int w, int h, int level, IntPtr data, int dataLength);

    // ── Renderbuffers ──

    /// <summary>Returns an FNA3D_Renderbuffer*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_GenColorRenderbuffer(IntPtr device, int width, int height, FNA3D_SurfaceFormat format, int multiSampleCount, IntPtr texture);

    /// <summary>Returns an FNA3D_Renderbuffer*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_GenDepthStencilRenderbuffer(IntPtr device, int width, int height, FNA3D_DepthFormat format, int multiSampleCount);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_AddDisposeRenderbuffer(IntPtr device, IntPtr renderbuffer);

    // ── Vertex Buffers ──

    /// <summary>Returns an FNA3D_Buffer*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_GenVertexBuffer(IntPtr device, byte dynamic, FNA3D_BufferUsage usage, int sizeInBytes);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_AddDisposeVertexBuffer(IntPtr device, IntPtr buffer);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetVertexBufferData(
        IntPtr device,
        IntPtr buffer,
        int offsetInBytes,
        IntPtr data,
        int elementCount,
        int elementSizeInBytes,
        int vertexStride,
        FNA3D_SetDataOptions options);

    // ── Index Buffers ──

    /// <summary>Returns an FNA3D_Buffer*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr FNA3D_GenIndexBuffer(IntPtr device, byte dynamic, FNA3D_BufferUsage usage, int sizeInBytes);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_AddDisposeIndexBuffer(IntPtr device, IntPtr buffer);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetIndexBufferData(IntPtr device, IntPtr buffer, int offsetInBytes, IntPtr data, int dataLength, FNA3D_SetDataOptions options);

    // ── Effects ──
    // FNA3D has no separate "raw shader" concept - draw calls are only issued through Effects
    // (MojoShader-parsed HLSL blobs). See NFMWorld.Shaders.Abstraction's IShaderModule doc
    // comments for how this backend maps that onto the shader abstraction (Milestone 3).

    /// <summary>effect/effectData refer to an FNA3D_Effect*/MOJOSHADER_effect*.</summary>
    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_CreateEffect(IntPtr device, byte[] effectCode, int length, out IntPtr effect, out IntPtr effectData);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_AddDisposeEffect(IntPtr device, IntPtr effect);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_SetEffectTechnique(IntPtr device, IntPtr effect, IntPtr technique);

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_ApplyEffect(IntPtr device, IntPtr effect, uint pass, IntPtr stateChanges /* MOJOSHADER_effectStateChanges* */);

    // ── Feature Queries ──

    [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FNA3D_GetMaxTextureSlots(IntPtr device, out int textures, out int vertexTextures);
}
