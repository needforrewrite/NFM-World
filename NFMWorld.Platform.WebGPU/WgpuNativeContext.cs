// LLM maintained.
//
// The WebGPU entry points the backend calls, bound for the browser - and the INativeContext that
// hands Silk.NET their addresses.
//
// Three wasm facts drive the shape, and the first two are the same ones the WebGL shim has:
//
//   - emscripten's proc addresses are wasm table indices, and calli accepts only the address of a
//     managed [UnmanagedCallersOnly] method, so Silk.NET cannot resolve by name itself: every entry
//     point is a trampoline below.
//   - a P/Invoke resolves through the generated table, so each trampoline forwards to
//     native/wgpu_shim.c, whose nfmw_* bodies are the exported symbols.
//   - the device is not requested here at all. wgpuInstanceRequestAdapter and
//     wgpuAdapterRequestDevice answer through futures completed from JS microtasks, which a
//     synchronous wait never runs; the page creates the device and hands it over instead, through
//     nfmw_emscripten_webgpu_get_device. So Adapter is null on this path.
//
// Generated from webgpu.h. When the backend starts calling something new, add the forwarder to
// wgpu_shim.c, the trampoline here, and the export in NFMWorld.Browser.csproj - hand-editing the
// three separately is how they drift apart.

#nullable enable
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace NFMWorld.Platform.WebGPU;

/// <summary>
/// Resolves WebGPU's entry points for the browser. <see cref="Device"/> is the DOM-created device,
/// which is the only device the browser path ever has: everything the desktop path asks an adapter
/// it asks a device here, because the two queries are the same values.
/// </summary>
public sealed unsafe class WgpuNativeContext : INativeContext
{
    private const string Lib = "wgpu_native";

    public static readonly WgpuNativeContext Instance = new();

    /// <summary>
    /// The device the page created before the runtime started. Handed out by
    /// <c>emscripten_webgpu_get_device</c>, which imports it with no future; released by the caller
    /// that adopted it, exactly like a device from a device request.
    ///
    /// Every call returns the same pointer but adds a reference, so a caller that adopts the device
    /// once must not call this again and expect a second device.
    /// </summary>
    public static nint AcquireDevice() => emscripten_webgpu_get_device();

    [DllImport(Lib, EntryPoint = "nfmw_emscripten_webgpu_get_device")]
    private static extern nint emscripten_webgpu_get_device();

    public IntPtr GetProcAddress(string proc, int? slot = null) => Lookup(proc);

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
    {
        addr = Lookup(proc);

        return addr != IntPtr.Zero;
    }

    // The browser's own WebGPU support is the extension story; nothing gates anything on this.
    public bool IsExtensionSupported(string extensionName) => false;

    public void Dispose()
    {
    }

    private static nint Lookup(string name) => name switch
    {
        "wgpuAdapterGetInfo" => (nint)(delegate* unmanaged<int, nint, int>)&W_AdapterGetInfo,
        "wgpuAdapterGetLimits" => (nint)(delegate* unmanaged<int, nint, int>)&W_AdapterGetLimits,
        "wgpuAdapterHasFeature" => (nint)(delegate* unmanaged<int, int, uint>)&W_AdapterHasFeature,
        "wgpuAdapterInfoFreeMembers" => (nint)(delegate* unmanaged<int, void>)&W_AdapterInfoFreeMembers,
        "wgpuAdapterRelease" => (nint)(delegate* unmanaged<int, void>)&W_AdapterRelease,
        "wgpuBindGroupLayoutRelease" => (nint)(delegate* unmanaged<int, void>)&W_BindGroupLayoutRelease,
        "wgpuBindGroupRelease" => (nint)(delegate* unmanaged<int, void>)&W_BindGroupRelease,
        "wgpuBufferDestroy" => (nint)(delegate* unmanaged<int, void>)&W_BufferDestroy,
        "wgpuBufferGetConstMappedRange" => (nint)(delegate* unmanaged<int, nuint, nuint, nint>)&W_BufferGetConstMappedRange,
        "wgpuBufferRelease" => (nint)(delegate* unmanaged<int, void>)&W_BufferRelease,
        "wgpuBufferUnmap" => (nint)(delegate* unmanaged<int, void>)&W_BufferUnmap,
        "wgpuCommandBufferRelease" => (nint)(delegate* unmanaged<int, void>)&W_CommandBufferRelease,
        "wgpuCommandEncoderBeginRenderPass" => (nint)(delegate* unmanaged<int, nint, int>)&W_CommandEncoderBeginRenderPass,
        "wgpuCommandEncoderCopyBufferToBuffer" => (nint)(delegate* unmanaged<int, int, ulong, int, ulong, ulong, void>)&W_CommandEncoderCopyBufferToBuffer,
        "wgpuCommandEncoderCopyBufferToTexture" => (nint)(delegate* unmanaged<int, nint, nint, nint, void>)&W_CommandEncoderCopyBufferToTexture,
        "wgpuCommandEncoderCopyTextureToBuffer" => (nint)(delegate* unmanaged<int, nint, nint, nint, void>)&W_CommandEncoderCopyTextureToBuffer,
        "wgpuCommandEncoderFinish" => (nint)(delegate* unmanaged<int, nint, int>)&W_CommandEncoderFinish,
        "wgpuCommandEncoderRelease" => (nint)(delegate* unmanaged<int, void>)&W_CommandEncoderRelease,
        "wgpuCreateInstance" => (nint)(delegate* unmanaged<nint, int>)&W_CreateInstance,
        "wgpuDeviceCreateBindGroup" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateBindGroup,
        "wgpuDeviceCreateBindGroupLayout" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateBindGroupLayout,
        "wgpuDeviceCreateBuffer" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateBuffer,
        "wgpuDeviceCreateCommandEncoder" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateCommandEncoder,
        "wgpuDeviceCreatePipelineLayout" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreatePipelineLayout,
        "wgpuDeviceCreateRenderPipeline" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateRenderPipeline,
        "wgpuDeviceCreateSampler" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateSampler,
        "wgpuDeviceCreateShaderModule" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateShaderModule,
        "wgpuDeviceCreateTexture" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceCreateTexture,
        "wgpuDeviceGetAdapterInfo" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceGetAdapterInfo,
        "wgpuDeviceGetLimits" => (nint)(delegate* unmanaged<int, nint, int>)&W_DeviceGetLimits,
        "wgpuDeviceGetQueue" => (nint)(delegate* unmanaged<int, int>)&W_DeviceGetQueue,
        "wgpuDeviceHasFeature" => (nint)(delegate* unmanaged<int, int, uint>)&W_DeviceHasFeature,
        "wgpuDeviceRelease" => (nint)(delegate* unmanaged<int, void>)&W_DeviceRelease,
        "wgpuInstanceCreateSurface" => (nint)(delegate* unmanaged<int, nint, int>)&W_InstanceCreateSurface,
        "wgpuInstanceProcessEvents" => (nint)(delegate* unmanaged<int, void>)&W_InstanceProcessEvents,
        "wgpuInstanceRelease" => (nint)(delegate* unmanaged<int, void>)&W_InstanceRelease,
        "wgpuPipelineLayoutRelease" => (nint)(delegate* unmanaged<int, void>)&W_PipelineLayoutRelease,
        "wgpuQueueRelease" => (nint)(delegate* unmanaged<int, void>)&W_QueueRelease,
        "wgpuQueueSubmit" => (nint)(delegate* unmanaged<int, nuint, nint, void>)&W_QueueSubmit,
        "wgpuQueueWriteBuffer" => (nint)(delegate* unmanaged<int, int, ulong, nint, nuint, void>)&W_QueueWriteBuffer,
        "wgpuQueueWriteTexture" => (nint)(delegate* unmanaged<int, nint, nint, nuint, nint, nint, void>)&W_QueueWriteTexture,
        "wgpuRenderPassEncoderDraw" => (nint)(delegate* unmanaged<int, uint, uint, uint, uint, void>)&W_RenderPassEncoderDraw,
        "wgpuRenderPassEncoderDrawIndexed" => (nint)(delegate* unmanaged<int, uint, uint, uint, int, uint, void>)&W_RenderPassEncoderDrawIndexed,
        "wgpuRenderPassEncoderEnd" => (nint)(delegate* unmanaged<int, void>)&W_RenderPassEncoderEnd,
        "wgpuRenderPassEncoderRelease" => (nint)(delegate* unmanaged<int, void>)&W_RenderPassEncoderRelease,
        "wgpuRenderPassEncoderSetBindGroup" => (nint)(delegate* unmanaged<int, uint, int, nuint, nint, void>)&W_RenderPassEncoderSetBindGroup,
        "wgpuRenderPassEncoderSetIndexBuffer" => (nint)(delegate* unmanaged<int, int, int, ulong, ulong, void>)&W_RenderPassEncoderSetIndexBuffer,
        "wgpuRenderPassEncoderSetPipeline" => (nint)(delegate* unmanaged<int, int, void>)&W_RenderPassEncoderSetPipeline,
        "wgpuRenderPassEncoderSetScissorRect" => (nint)(delegate* unmanaged<int, uint, uint, uint, uint, void>)&W_RenderPassEncoderSetScissorRect,
        "wgpuRenderPassEncoderSetVertexBuffer" => (nint)(delegate* unmanaged<int, uint, int, ulong, ulong, void>)&W_RenderPassEncoderSetVertexBuffer,
        "wgpuRenderPassEncoderSetViewport" => (nint)(delegate* unmanaged<int, float, float, float, float, float, float, void>)&W_RenderPassEncoderSetViewport,
        "wgpuRenderPipelineRelease" => (nint)(delegate* unmanaged<int, void>)&W_RenderPipelineRelease,
        "wgpuSamplerRelease" => (nint)(delegate* unmanaged<int, void>)&W_SamplerRelease,
        "wgpuShaderModuleRelease" => (nint)(delegate* unmanaged<int, void>)&W_ShaderModuleRelease,
        "wgpuSurfaceCapabilitiesFreeMembers" => (nint)(delegate* unmanaged<int, void>)&W_SurfaceCapabilitiesFreeMembers,
        "wgpuSurfaceConfigure" => (nint)(delegate* unmanaged<int, nint, void>)&W_SurfaceConfigure,
        "wgpuSurfaceGetCapabilities" => (nint)(delegate* unmanaged<int, int, nint, int>)&W_SurfaceGetCapabilities,
        "wgpuSurfaceGetCurrentTexture" => (nint)(delegate* unmanaged<int, nint, void>)&W_SurfaceGetCurrentTexture,
        "wgpuSurfacePresent" => (nint)(delegate* unmanaged<int, int>)&W_SurfacePresent,
        "wgpuSurfaceRelease" => (nint)(delegate* unmanaged<int, void>)&W_SurfaceRelease,
        "wgpuTextureCreateView" => (nint)(delegate* unmanaged<int, nint, int>)&W_TextureCreateView,
        "wgpuTextureDestroy" => (nint)(delegate* unmanaged<int, void>)&W_TextureDestroy,
        "wgpuTextureRelease" => (nint)(delegate* unmanaged<int, void>)&W_TextureRelease,
        "wgpuTextureViewRelease" => (nint)(delegate* unmanaged<int, void>)&W_TextureViewRelease,
        _ => 0,
    };

    // --------------------------------------------------------------------------------------–
    // DllImports: the wasm bodies, from native/wgpu_shim.c.
    // --------------------------------------------------------------------------------------–

    [DllImport(Lib, EntryPoint = "nfmw_AdapterGetInfo")]
    private static extern int AdapterGetInfo(int adapter, nint info);

    [DllImport(Lib, EntryPoint = "nfmw_AdapterGetLimits")]
    private static extern int AdapterGetLimits(int adapter, nint limits);

    [DllImport(Lib, EntryPoint = "nfmw_AdapterHasFeature")]
    private static extern uint AdapterHasFeature(int adapter, int feature);

    [DllImport(Lib, EntryPoint = "nfmw_AdapterInfoFreeMembers")]
    private static extern void AdapterInfoFreeMembers(int adapterInfo);

    [DllImport(Lib, EntryPoint = "nfmw_AdapterRelease")]
    private static extern void AdapterRelease(int adapter);

    [DllImport(Lib, EntryPoint = "nfmw_BindGroupLayoutRelease")]
    private static extern void BindGroupLayoutRelease(int bindGroupLayout);

    [DllImport(Lib, EntryPoint = "nfmw_BindGroupRelease")]
    private static extern void BindGroupRelease(int bindGroup);

    [DllImport(Lib, EntryPoint = "nfmw_BufferDestroy")]
    private static extern void BufferDestroy(int buffer);

    [DllImport(Lib, EntryPoint = "nfmw_BufferGetConstMappedRange")]
    private static extern nint BufferGetConstMappedRange(int buffer, nuint offset, nuint size);

    [DllImport(Lib, EntryPoint = "nfmw_BufferRelease")]
    private static extern void BufferRelease(int buffer);

    [DllImport(Lib, EntryPoint = "nfmw_BufferUnmap")]
    private static extern void BufferUnmap(int buffer);

    [DllImport(Lib, EntryPoint = "nfmw_CommandBufferRelease")]
    private static extern void CommandBufferRelease(int commandBuffer);

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderBeginRenderPass")]
    private static extern int CommandEncoderBeginRenderPass(int commandEncoder, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderCopyBufferToBuffer")]
    private static extern void CommandEncoderCopyBufferToBuffer(
        int commandEncoder,
        int source,
        ulong sourceOffset,
        int destination,
        ulong destinationOffset,
        ulong size
    );

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderCopyBufferToTexture")]
    private static extern void CommandEncoderCopyBufferToTexture(
        int commandEncoder,
        nint source,
        nint destination,
        nint copySize
    );

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderCopyTextureToBuffer")]
    private static extern void CommandEncoderCopyTextureToBuffer(
        int commandEncoder,
        nint source,
        nint destination,
        nint copySize
    );

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderFinish")]
    private static extern int CommandEncoderFinish(int commandEncoder, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_CommandEncoderRelease")]
    private static extern void CommandEncoderRelease(int commandEncoder);

    [DllImport(Lib, EntryPoint = "nfmw_CreateInstance")]
    private static extern int CreateInstance(nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateBindGroup")]
    private static extern int DeviceCreateBindGroup(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateBindGroupLayout")]
    private static extern int DeviceCreateBindGroupLayout(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateBuffer")]
    private static extern int DeviceCreateBuffer(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateCommandEncoder")]
    private static extern int DeviceCreateCommandEncoder(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreatePipelineLayout")]
    private static extern int DeviceCreatePipelineLayout(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateRenderPipeline")]
    private static extern int DeviceCreateRenderPipeline(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateSampler")]
    private static extern int DeviceCreateSampler(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateShaderModule")]
    private static extern int DeviceCreateShaderModule(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceCreateTexture")]
    private static extern int DeviceCreateTexture(int device, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceGetAdapterInfo")]
    private static extern int DeviceGetAdapterInfo(int device, nint adapterInfo);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceGetLimits")]
    private static extern int DeviceGetLimits(int device, nint limits);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceGetQueue")]
    private static extern int DeviceGetQueue(int device);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceHasFeature")]
    private static extern uint DeviceHasFeature(int device, int feature);

    [DllImport(Lib, EntryPoint = "nfmw_DeviceRelease")]
    private static extern void DeviceRelease(int device);

    [DllImport(Lib, EntryPoint = "nfmw_InstanceCreateSurface")]
    private static extern int InstanceCreateSurface(int instance, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_InstanceProcessEvents")]
    private static extern void InstanceProcessEvents(int instance);

    [DllImport(Lib, EntryPoint = "nfmw_InstanceRelease")]
    private static extern void InstanceRelease(int instance);

    [DllImport(Lib, EntryPoint = "nfmw_PipelineLayoutRelease")]
    private static extern void PipelineLayoutRelease(int pipelineLayout);

    [DllImport(Lib, EntryPoint = "nfmw_QueueRelease")]
    private static extern void QueueRelease(int queue);

    [DllImport(Lib, EntryPoint = "nfmw_QueueSubmit")]
    private static extern void QueueSubmit(int queue, nuint commandCount, nint commands);

    [DllImport(Lib, EntryPoint = "nfmw_QueueWriteBuffer")]
    private static extern void QueueWriteBuffer(int queue, int buffer, ulong bufferOffset, nint data, nuint size);

    [DllImport(Lib, EntryPoint = "nfmw_QueueWriteTexture")]
    private static extern void QueueWriteTexture(
        int queue,
        nint destination,
        nint data,
        nuint dataSize,
        nint dataLayout,
        nint writeSize
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderDraw")]
    private static extern void RenderPassEncoderDraw(
        int renderPassEncoder,
        uint vertexCount,
        uint instanceCount,
        uint firstVertex,
        uint firstInstance
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderDrawIndexed")]
    private static extern void RenderPassEncoderDrawIndexed(
        int renderPassEncoder,
        uint indexCount,
        uint instanceCount,
        uint firstIndex,
        int baseVertex,
        uint firstInstance
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderEnd")]
    private static extern void RenderPassEncoderEnd(int renderPassEncoder);

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderRelease")]
    private static extern void RenderPassEncoderRelease(int renderPassEncoder);

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetBindGroup")]
    private static extern void RenderPassEncoderSetBindGroup(
        int renderPassEncoder,
        uint groupIndex,
        int group,
        nuint dynamicOffsetCount,
        nint dynamicOffsets
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetIndexBuffer")]
    private static extern void RenderPassEncoderSetIndexBuffer(
        int renderPassEncoder,
        int buffer,
        int format,
        ulong offset,
        ulong size
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetPipeline")]
    private static extern void RenderPassEncoderSetPipeline(int renderPassEncoder, int pipeline);

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetScissorRect")]
    private static extern void RenderPassEncoderSetScissorRect(int renderPassEncoder, uint x, uint y, uint width, uint height);

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetVertexBuffer")]
    private static extern void RenderPassEncoderSetVertexBuffer(
        int renderPassEncoder,
        uint slot,
        int buffer,
        ulong offset,
        ulong size
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPassEncoderSetViewport")]
    private static extern void RenderPassEncoderSetViewport(
        int renderPassEncoder,
        float x,
        float y,
        float width,
        float height,
        float minDepth,
        float maxDepth
    );

    [DllImport(Lib, EntryPoint = "nfmw_RenderPipelineRelease")]
    private static extern void RenderPipelineRelease(int renderPipeline);

    [DllImport(Lib, EntryPoint = "nfmw_SamplerRelease")]
    private static extern void SamplerRelease(int sampler);

    [DllImport(Lib, EntryPoint = "nfmw_ShaderModuleRelease")]
    private static extern void ShaderModuleRelease(int shaderModule);

    [DllImport(Lib, EntryPoint = "nfmw_SurfaceCapabilitiesFreeMembers")]
    private static extern void SurfaceCapabilitiesFreeMembers(int surfaceCapabilities);

    [DllImport(Lib, EntryPoint = "nfmw_SurfaceConfigure")]
    private static extern void SurfaceConfigure(int surface, nint config);

    [DllImport(Lib, EntryPoint = "nfmw_SurfaceGetCapabilities")]
    private static extern int SurfaceGetCapabilities(int surface, int adapter, nint capabilities);

    [DllImport(Lib, EntryPoint = "nfmw_SurfaceGetCurrentTexture")]
    private static extern void SurfaceGetCurrentTexture(int surface, nint surfaceTexture);

    [DllImport(Lib, EntryPoint = "nfmw_SurfacePresent")]
    private static extern int SurfacePresent(int surface);

    [DllImport(Lib, EntryPoint = "nfmw_SurfaceRelease")]
    private static extern void SurfaceRelease(int surface);

    [DllImport(Lib, EntryPoint = "nfmw_TextureCreateView")]
    private static extern int TextureCreateView(int texture, nint descriptor);

    [DllImport(Lib, EntryPoint = "nfmw_TextureDestroy")]
    private static extern void TextureDestroy(int texture);

    [DllImport(Lib, EntryPoint = "nfmw_TextureRelease")]
    private static extern void TextureRelease(int texture);

    [DllImport(Lib, EntryPoint = "nfmw_TextureViewRelease")]
    private static extern void TextureViewRelease(int textureView);

    // --------------------------------------------------------------------------------------–
    // Trampolines: what Lookup hands back. Each is the address calli is allowed to take.
    // --------------------------------------------------------------------------------------–

    [UnmanagedCallersOnly]
    private static int W_AdapterGetInfo(int adapter, nint info) => AdapterGetInfo(adapter, info);

    [UnmanagedCallersOnly]
    private static int W_AdapterGetLimits(int adapter, nint limits) => AdapterGetLimits(adapter, limits);

    [UnmanagedCallersOnly]
    private static uint W_AdapterHasFeature(int adapter, int feature) => AdapterHasFeature(adapter, feature);

    [UnmanagedCallersOnly]
    private static void W_AdapterInfoFreeMembers(int adapterInfo) => AdapterInfoFreeMembers(adapterInfo);

    [UnmanagedCallersOnly]
    private static void W_AdapterRelease(int adapter) => AdapterRelease(adapter);

    [UnmanagedCallersOnly]
    private static void W_BindGroupLayoutRelease(int bindGroupLayout) => BindGroupLayoutRelease(bindGroupLayout);

    [UnmanagedCallersOnly]
    private static void W_BindGroupRelease(int bindGroup) => BindGroupRelease(bindGroup);

    [UnmanagedCallersOnly]
    private static void W_BufferDestroy(int buffer) => BufferDestroy(buffer);

    [UnmanagedCallersOnly]
    private static nint W_BufferGetConstMappedRange(int buffer, nuint offset, nuint size) => BufferGetConstMappedRange(
        buffer,
        offset,
        size
    );

    [UnmanagedCallersOnly]
    private static void W_BufferRelease(int buffer) => BufferRelease(buffer);

    [UnmanagedCallersOnly]
    private static void W_BufferUnmap(int buffer) => BufferUnmap(buffer);

    [UnmanagedCallersOnly]
    private static void W_CommandBufferRelease(int commandBuffer) => CommandBufferRelease(commandBuffer);

    [UnmanagedCallersOnly]
    private static int W_CommandEncoderBeginRenderPass(int commandEncoder, nint descriptor) => CommandEncoderBeginRenderPass(
        commandEncoder,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static void W_CommandEncoderCopyBufferToBuffer(
        int commandEncoder,
        int source,
        ulong sourceOffset,
        int destination,
        ulong destinationOffset,
        ulong size
    ) => CommandEncoderCopyBufferToBuffer(commandEncoder, source, sourceOffset, destination, destinationOffset, size);

    [UnmanagedCallersOnly]
    private static void W_CommandEncoderCopyBufferToTexture(int commandEncoder, nint source, nint destination, nint copySize) => CommandEncoderCopyBufferToTexture(
        commandEncoder,
        source,
        destination,
        copySize
    );

    [UnmanagedCallersOnly]
    private static void W_CommandEncoderCopyTextureToBuffer(int commandEncoder, nint source, nint destination, nint copySize) => CommandEncoderCopyTextureToBuffer(
        commandEncoder,
        source,
        destination,
        copySize
    );

    [UnmanagedCallersOnly]
    private static int W_CommandEncoderFinish(int commandEncoder, nint descriptor) => CommandEncoderFinish(
        commandEncoder,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static void W_CommandEncoderRelease(int commandEncoder) => CommandEncoderRelease(commandEncoder);

    [UnmanagedCallersOnly]
    private static int W_CreateInstance(nint descriptor) => CreateInstance(descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateBindGroup(int device, nint descriptor) => DeviceCreateBindGroup(device, descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateBindGroupLayout(int device, nint descriptor) => DeviceCreateBindGroupLayout(
        device,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateBuffer(int device, nint descriptor) => DeviceCreateBuffer(device, descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateCommandEncoder(int device, nint descriptor) => DeviceCreateCommandEncoder(
        device,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static int W_DeviceCreatePipelineLayout(int device, nint descriptor) => DeviceCreatePipelineLayout(
        device,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateRenderPipeline(int device, nint descriptor) => DeviceCreateRenderPipeline(
        device,
        descriptor
    );

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateSampler(int device, nint descriptor) => DeviceCreateSampler(device, descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateShaderModule(int device, nint descriptor) => DeviceCreateShaderModule(device, descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceCreateTexture(int device, nint descriptor) => DeviceCreateTexture(device, descriptor);

    [UnmanagedCallersOnly]
    private static int W_DeviceGetAdapterInfo(int device, nint adapterInfo) => DeviceGetAdapterInfo(device, adapterInfo);

    [UnmanagedCallersOnly]
    private static int W_DeviceGetLimits(int device, nint limits) => DeviceGetLimits(device, limits);

    [UnmanagedCallersOnly]
    private static int W_DeviceGetQueue(int device) => DeviceGetQueue(device);

    [UnmanagedCallersOnly]
    private static uint W_DeviceHasFeature(int device, int feature) => DeviceHasFeature(device, feature);

    [UnmanagedCallersOnly]
    private static void W_DeviceRelease(int device) => DeviceRelease(device);

    [UnmanagedCallersOnly]
    private static int W_InstanceCreateSurface(int instance, nint descriptor) => InstanceCreateSurface(instance, descriptor);

    [UnmanagedCallersOnly]
    private static void W_InstanceProcessEvents(int instance) => InstanceProcessEvents(instance);

    [UnmanagedCallersOnly]
    private static void W_InstanceRelease(int instance) => InstanceRelease(instance);

    [UnmanagedCallersOnly]
    private static void W_PipelineLayoutRelease(int pipelineLayout) => PipelineLayoutRelease(pipelineLayout);

    [UnmanagedCallersOnly]
    private static void W_QueueRelease(int queue) => QueueRelease(queue);

    [UnmanagedCallersOnly]
    private static void W_QueueSubmit(int queue, nuint commandCount, nint commands) => QueueSubmit(queue, commandCount, commands);

    [UnmanagedCallersOnly]
    private static void W_QueueWriteBuffer(int queue, int buffer, ulong bufferOffset, nint data, nuint size) => QueueWriteBuffer(
        queue,
        buffer,
        bufferOffset,
        data,
        size
    );

    [UnmanagedCallersOnly]
    private static void W_QueueWriteTexture(
        int queue,
        nint destination,
        nint data,
        nuint dataSize,
        nint dataLayout,
        nint writeSize
    ) => QueueWriteTexture(queue, destination, data, dataSize, dataLayout, writeSize);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderDraw(
        int renderPassEncoder,
        uint vertexCount,
        uint instanceCount,
        uint firstVertex,
        uint firstInstance
    ) => RenderPassEncoderDraw(renderPassEncoder, vertexCount, instanceCount, firstVertex, firstInstance);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderDrawIndexed(
        int renderPassEncoder,
        uint indexCount,
        uint instanceCount,
        uint firstIndex,
        int baseVertex,
        uint firstInstance
    ) => RenderPassEncoderDrawIndexed(renderPassEncoder, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderEnd(int renderPassEncoder) => RenderPassEncoderEnd(renderPassEncoder);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderRelease(int renderPassEncoder) => RenderPassEncoderRelease(renderPassEncoder);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetBindGroup(
        int renderPassEncoder,
        uint groupIndex,
        int group,
        nuint dynamicOffsetCount,
        nint dynamicOffsets
    ) => RenderPassEncoderSetBindGroup(renderPassEncoder, groupIndex, group, dynamicOffsetCount, dynamicOffsets);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetIndexBuffer(int renderPassEncoder, int buffer, int format, ulong offset, ulong size)
        => RenderPassEncoderSetIndexBuffer(renderPassEncoder, buffer, format, offset, size);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetPipeline(int renderPassEncoder, int pipeline) => RenderPassEncoderSetPipeline(
        renderPassEncoder,
        pipeline
    );

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetScissorRect(int renderPassEncoder, uint x, uint y, uint width, uint height) => RenderPassEncoderSetScissorRect(
        renderPassEncoder,
        x,
        y,
        width,
        height
    );

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetVertexBuffer(int renderPassEncoder, uint slot, int buffer, ulong offset, ulong size)
        => RenderPassEncoderSetVertexBuffer(renderPassEncoder, slot, buffer, offset, size);

    [UnmanagedCallersOnly]
    private static void W_RenderPassEncoderSetViewport(
        int renderPassEncoder,
        float x,
        float y,
        float width,
        float height,
        float minDepth,
        float maxDepth
    ) => RenderPassEncoderSetViewport(renderPassEncoder, x, y, width, height, minDepth, maxDepth);

    [UnmanagedCallersOnly]
    private static void W_RenderPipelineRelease(int renderPipeline) => RenderPipelineRelease(renderPipeline);

    [UnmanagedCallersOnly]
    private static void W_SamplerRelease(int sampler) => SamplerRelease(sampler);

    [UnmanagedCallersOnly]
    private static void W_ShaderModuleRelease(int shaderModule) => ShaderModuleRelease(shaderModule);

    [UnmanagedCallersOnly]
    private static void W_SurfaceCapabilitiesFreeMembers(int surfaceCapabilities) => SurfaceCapabilitiesFreeMembers(
        surfaceCapabilities
    );

    [UnmanagedCallersOnly]
    private static void W_SurfaceConfigure(int surface, nint config) => SurfaceConfigure(surface, config);

    [UnmanagedCallersOnly]
    private static int W_SurfaceGetCapabilities(int surface, int adapter, nint capabilities) => SurfaceGetCapabilities(
        surface,
        adapter,
        capabilities
    );

    [UnmanagedCallersOnly]
    private static void W_SurfaceGetCurrentTexture(int surface, nint surfaceTexture) => SurfaceGetCurrentTexture(
        surface,
        surfaceTexture
    );

    [UnmanagedCallersOnly]
    private static int W_SurfacePresent(int surface) => SurfacePresent(surface);

    [UnmanagedCallersOnly]
    private static void W_SurfaceRelease(int surface) => SurfaceRelease(surface);

    [UnmanagedCallersOnly]
    private static int W_TextureCreateView(int texture, nint descriptor) => TextureCreateView(texture, descriptor);

    [UnmanagedCallersOnly]
    private static void W_TextureDestroy(int texture) => TextureDestroy(texture);

    [UnmanagedCallersOnly]
    private static void W_TextureRelease(int texture) => TextureRelease(texture);

    [UnmanagedCallersOnly]
    private static void W_TextureViewRelease(int textureView) => TextureViewRelease(textureView);
}
