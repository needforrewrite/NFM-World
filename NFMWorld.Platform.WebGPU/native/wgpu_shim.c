// Every webgpu.h entry point the backend calls, forwarded under an nfmw_ name.
//
// Two kinds of function sit in this list, and only the first is obvious:
//
//   - Most of emdawnwebgpu's API is implemented in webgpu.cpp, so it has a wasm body and could in
//     principle be called directly. It is forwarded anyway, because a single rule beats two: every
//     entry point the backend reaches goes through nfmw_* and therefore through the browser head's
//     --export list, and a name that is missing from that list is a link error rather than a
//     P/Invoke that resolves to nothing at run time. 35 of the 66 have no body at all (they are the
//     JS library's own functions - wgpuQueueSubmit, wgpuSurfaceConfigure, the render-pass setters),
//     and for those a forwarder is the only way to get an exportable symbol: the P/Invoke table is
//     generated from the wasm module, not from this file's includes.
//
//   - emscripten_webgpu_get_device has no C-side equivalent that works. Every adapter and device
//     request answers through a future, and the future is completed from a JS microtask, which a
//     synchronous wait never gets to run - measured: wgpuInstanceProcessEvents spins 6.6M times and
//     the callback never fires. The page therefore creates the adapter and device with
//     navigator.gpu before the runtime starts and parks the device on Module, and this hands it
//     over without a future.
//
// Generated from webgpu.h. Adding an entry point the backend starts calling means adding it here,
// its trampoline in WgpuNativeContext.cs, and its export in NFMWorld.Browser.csproj - all three.
#include <webgpu/webgpu.h>

WGPUDevice nfmw_emscripten_webgpu_get_device(void) { return emscripten_webgpu_get_device(); }

WGPUStatus nfmw_AdapterGetInfo(WGPUAdapter adapter, WGPUAdapterInfo * info) { return wgpuAdapterGetInfo(adapter, info); }
WGPUStatus nfmw_AdapterGetLimits(WGPUAdapter adapter, WGPULimits * limits) { return wgpuAdapterGetLimits(adapter, limits); }
WGPUBool nfmw_AdapterHasFeature(WGPUAdapter adapter, WGPUFeatureName feature) { return wgpuAdapterHasFeature(adapter, feature); }
void nfmw_AdapterInfoFreeMembers(WGPUAdapterInfo adapterInfo) { wgpuAdapterInfoFreeMembers(adapterInfo); }
void nfmw_AdapterRelease(WGPUAdapter adapter) { wgpuAdapterRelease(adapter); }
void nfmw_BindGroupLayoutRelease(WGPUBindGroupLayout bindGroupLayout) { wgpuBindGroupLayoutRelease(bindGroupLayout); }
void nfmw_BindGroupRelease(WGPUBindGroup bindGroup) { wgpuBindGroupRelease(bindGroup); }
void nfmw_BufferDestroy(WGPUBuffer buffer) { wgpuBufferDestroy(buffer); }
void const * nfmw_BufferGetConstMappedRange(WGPUBuffer buffer, size_t offset, size_t size) { return wgpuBufferGetConstMappedRange(buffer, offset, size); }
void nfmw_BufferRelease(WGPUBuffer buffer) { wgpuBufferRelease(buffer); }
void nfmw_BufferUnmap(WGPUBuffer buffer) { wgpuBufferUnmap(buffer); }
void nfmw_CommandBufferRelease(WGPUCommandBuffer commandBuffer) { wgpuCommandBufferRelease(commandBuffer); }
WGPURenderPassEncoder nfmw_CommandEncoderBeginRenderPass(WGPUCommandEncoder commandEncoder, WGPURenderPassDescriptor const * descriptor) { return wgpuCommandEncoderBeginRenderPass(commandEncoder, descriptor); }
void nfmw_CommandEncoderCopyBufferToBuffer(WGPUCommandEncoder commandEncoder, WGPUBuffer source, uint64_t sourceOffset, WGPUBuffer destination, uint64_t destinationOffset, uint64_t size) { wgpuCommandEncoderCopyBufferToBuffer(commandEncoder, source, sourceOffset, destination, destinationOffset, size); }
void nfmw_CommandEncoderCopyBufferToTexture(WGPUCommandEncoder commandEncoder, WGPUTexelCopyBufferInfo const * source, WGPUTexelCopyTextureInfo const * destination, WGPUExtent3D const * copySize) { wgpuCommandEncoderCopyBufferToTexture(commandEncoder, source, destination, copySize); }
void nfmw_CommandEncoderCopyTextureToBuffer(WGPUCommandEncoder commandEncoder, WGPUTexelCopyTextureInfo const * source, WGPUTexelCopyBufferInfo const * destination, WGPUExtent3D const * copySize) { wgpuCommandEncoderCopyTextureToBuffer(commandEncoder, source, destination, copySize); }
WGPUCommandBuffer nfmw_CommandEncoderFinish(WGPUCommandEncoder commandEncoder, WGPU_NULLABLE WGPUCommandBufferDescriptor const * descriptor) { return wgpuCommandEncoderFinish(commandEncoder, descriptor); }
void nfmw_CommandEncoderRelease(WGPUCommandEncoder commandEncoder) { wgpuCommandEncoderRelease(commandEncoder); }
WGPUInstance nfmw_CreateInstance(WGPU_NULLABLE WGPUInstanceDescriptor const * descriptor) { return wgpuCreateInstance(descriptor); }
WGPUBindGroup nfmw_DeviceCreateBindGroup(WGPUDevice device, WGPUBindGroupDescriptor const * descriptor) { return wgpuDeviceCreateBindGroup(device, descriptor); }
WGPUBindGroupLayout nfmw_DeviceCreateBindGroupLayout(WGPUDevice device, WGPUBindGroupLayoutDescriptor const * descriptor) { return wgpuDeviceCreateBindGroupLayout(device, descriptor); }
WGPU_NULLABLE WGPUBuffer nfmw_DeviceCreateBuffer(WGPUDevice device, WGPUBufferDescriptor const * descriptor) { return wgpuDeviceCreateBuffer(device, descriptor); }
WGPUCommandEncoder nfmw_DeviceCreateCommandEncoder(WGPUDevice device, WGPU_NULLABLE WGPUCommandEncoderDescriptor const * descriptor) { return wgpuDeviceCreateCommandEncoder(device, descriptor); }
WGPUPipelineLayout nfmw_DeviceCreatePipelineLayout(WGPUDevice device, WGPUPipelineLayoutDescriptor const * descriptor) { return wgpuDeviceCreatePipelineLayout(device, descriptor); }
WGPURenderPipeline nfmw_DeviceCreateRenderPipeline(WGPUDevice device, WGPURenderPipelineDescriptor const * descriptor) { return wgpuDeviceCreateRenderPipeline(device, descriptor); }
WGPUSampler nfmw_DeviceCreateSampler(WGPUDevice device, WGPU_NULLABLE WGPUSamplerDescriptor const * descriptor) { return wgpuDeviceCreateSampler(device, descriptor); }
WGPUShaderModule nfmw_DeviceCreateShaderModule(WGPUDevice device, WGPUShaderModuleDescriptor const * descriptor) { return wgpuDeviceCreateShaderModule(device, descriptor); }
WGPUTexture nfmw_DeviceCreateTexture(WGPUDevice device, WGPUTextureDescriptor const * descriptor) { return wgpuDeviceCreateTexture(device, descriptor); }
WGPUStatus nfmw_DeviceGetAdapterInfo(WGPUDevice device, WGPUAdapterInfo * adapterInfo) { return wgpuDeviceGetAdapterInfo(device, adapterInfo); }
WGPUStatus nfmw_DeviceGetLimits(WGPUDevice device, WGPULimits * limits) { return wgpuDeviceGetLimits(device, limits); }
WGPUQueue nfmw_DeviceGetQueue(WGPUDevice device) { return wgpuDeviceGetQueue(device); }
WGPUBool nfmw_DeviceHasFeature(WGPUDevice device, WGPUFeatureName feature) { return wgpuDeviceHasFeature(device, feature); }
void nfmw_DeviceRelease(WGPUDevice device) { wgpuDeviceRelease(device); }
WGPUSurface nfmw_InstanceCreateSurface(WGPUInstance instance, WGPUSurfaceDescriptor const * descriptor) { return wgpuInstanceCreateSurface(instance, descriptor); }
void nfmw_InstanceProcessEvents(WGPUInstance instance) { wgpuInstanceProcessEvents(instance); }
void nfmw_InstanceRelease(WGPUInstance instance) { wgpuInstanceRelease(instance); }
void nfmw_PipelineLayoutRelease(WGPUPipelineLayout pipelineLayout) { wgpuPipelineLayoutRelease(pipelineLayout); }
void nfmw_QueueRelease(WGPUQueue queue) { wgpuQueueRelease(queue); }
void nfmw_QueueSubmit(WGPUQueue queue, size_t commandCount, WGPUCommandBuffer const * commands) { wgpuQueueSubmit(queue, commandCount, commands); }
void nfmw_QueueWriteBuffer(WGPUQueue queue, WGPUBuffer buffer, uint64_t bufferOffset, void const * data, size_t size) { wgpuQueueWriteBuffer(queue, buffer, bufferOffset, data, size); }
void nfmw_QueueWriteTexture(WGPUQueue queue, WGPUTexelCopyTextureInfo const * destination, void const * data, size_t dataSize, WGPUTexelCopyBufferLayout const * dataLayout, WGPUExtent3D const * writeSize) { wgpuQueueWriteTexture(queue, destination, data, dataSize, dataLayout, writeSize); }
void nfmw_RenderPassEncoderDraw(WGPURenderPassEncoder renderPassEncoder, uint32_t vertexCount, uint32_t instanceCount, uint32_t firstVertex, uint32_t firstInstance) { wgpuRenderPassEncoderDraw(renderPassEncoder, vertexCount, instanceCount, firstVertex, firstInstance); }
void nfmw_RenderPassEncoderDrawIndexed(WGPURenderPassEncoder renderPassEncoder, uint32_t indexCount, uint32_t instanceCount, uint32_t firstIndex, int32_t baseVertex, uint32_t firstInstance) { wgpuRenderPassEncoderDrawIndexed(renderPassEncoder, indexCount, instanceCount, firstIndex, baseVertex, firstInstance); }
void nfmw_RenderPassEncoderEnd(WGPURenderPassEncoder renderPassEncoder) { wgpuRenderPassEncoderEnd(renderPassEncoder); }
void nfmw_RenderPassEncoderRelease(WGPURenderPassEncoder renderPassEncoder) { wgpuRenderPassEncoderRelease(renderPassEncoder); }
void nfmw_RenderPassEncoderSetBindGroup(WGPURenderPassEncoder renderPassEncoder, uint32_t groupIndex, WGPU_NULLABLE WGPUBindGroup group, size_t dynamicOffsetCount, uint32_t const * dynamicOffsets) { wgpuRenderPassEncoderSetBindGroup(renderPassEncoder, groupIndex, group, dynamicOffsetCount, dynamicOffsets); }
void nfmw_RenderPassEncoderSetIndexBuffer(WGPURenderPassEncoder renderPassEncoder, WGPUBuffer buffer, WGPUIndexFormat format, uint64_t offset, uint64_t size) { wgpuRenderPassEncoderSetIndexBuffer(renderPassEncoder, buffer, format, offset, size); }
void nfmw_RenderPassEncoderSetPipeline(WGPURenderPassEncoder renderPassEncoder, WGPURenderPipeline pipeline) { wgpuRenderPassEncoderSetPipeline(renderPassEncoder, pipeline); }
void nfmw_RenderPassEncoderSetScissorRect(WGPURenderPassEncoder renderPassEncoder, uint32_t x, uint32_t y, uint32_t width, uint32_t height) { wgpuRenderPassEncoderSetScissorRect(renderPassEncoder, x, y, width, height); }
void nfmw_RenderPassEncoderSetVertexBuffer(WGPURenderPassEncoder renderPassEncoder, uint32_t slot, WGPU_NULLABLE WGPUBuffer buffer, uint64_t offset, uint64_t size) { wgpuRenderPassEncoderSetVertexBuffer(renderPassEncoder, slot, buffer, offset, size); }
void nfmw_RenderPassEncoderSetViewport(WGPURenderPassEncoder renderPassEncoder, float x, float y, float width, float height, float minDepth, float maxDepth) { wgpuRenderPassEncoderSetViewport(renderPassEncoder, x, y, width, height, minDepth, maxDepth); }
void nfmw_RenderPipelineRelease(WGPURenderPipeline renderPipeline) { wgpuRenderPipelineRelease(renderPipeline); }
void nfmw_SamplerRelease(WGPUSampler sampler) { wgpuSamplerRelease(sampler); }
void nfmw_ShaderModuleRelease(WGPUShaderModule shaderModule) { wgpuShaderModuleRelease(shaderModule); }
void nfmw_SurfaceCapabilitiesFreeMembers(WGPUSurfaceCapabilities surfaceCapabilities) { wgpuSurfaceCapabilitiesFreeMembers(surfaceCapabilities); }
void nfmw_SurfaceConfigure(WGPUSurface surface, WGPUSurfaceConfiguration const * config) { wgpuSurfaceConfigure(surface, config); }
WGPUStatus nfmw_SurfaceGetCapabilities(WGPUSurface surface, WGPUAdapter adapter, WGPUSurfaceCapabilities * capabilities) { return wgpuSurfaceGetCapabilities(surface, adapter, capabilities); }
void nfmw_SurfaceGetCurrentTexture(WGPUSurface surface, WGPUSurfaceTexture * surfaceTexture) { wgpuSurfaceGetCurrentTexture(surface, surfaceTexture); }
WGPUStatus nfmw_SurfacePresent(WGPUSurface surface) { return wgpuSurfacePresent(surface); }
void nfmw_SurfaceRelease(WGPUSurface surface) { wgpuSurfaceRelease(surface); }
WGPUTextureView nfmw_TextureCreateView(WGPUTexture texture, WGPU_NULLABLE WGPUTextureViewDescriptor const * descriptor) { return wgpuTextureCreateView(texture, descriptor); }
void nfmw_TextureDestroy(WGPUTexture texture) { wgpuTextureDestroy(texture); }
void nfmw_TextureRelease(WGPUTexture texture) { wgpuTextureRelease(texture); }
void nfmw_TextureViewRelease(WGPUTextureView textureView) { wgpuTextureViewRelease(textureView); }
