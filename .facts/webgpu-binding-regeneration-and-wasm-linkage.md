---
name: webgpu-binding-regeneration-and-wasm-linkage
description: "The repo regenerates its own WebGPU bindings from a pinned webgpu.h (Maxine.Silk.WebGPU submodule) and ships wgpu-native v29 binaries with them; the backend's async, feature-query and pass-validation traps are all wgpu-native-specific, and Dawn's emdawnwebgpu port compiles and links with the .NET 11 Emscripten workload"
metadata:
  node_type: memory
  type: reference
---

The backend no longer uses `Silk.NET.WebGPU` 2.23.0. It builds against **`Maxine.Silk.WebGPU`**
(submodule, `uwx/Maxine.Silk.WebGPU`), a companion package in the shape of `Maxine.Silk.EGL`:
namespace `Maxine.WebGPU`, regenerated from a pinned header, with its own `wgpu-native` binaries.
Measured 2026-11-23/24.

## The package

|Piece|Detail|
|---|---|
|Pinned headers|`webgpu.h` 6766 lines / `a483031c…`, `wgpu.h` 1551 / `7bd23656…`, `dawn-webgpu.h` 2877 / `40e7ecd9…`|
|Generation|one task, three profiles (`webgpu`, `wgpu`, `dawn`); ~6 s; `dotnet run --project tools/SilkGenerate -- generator.json`|
|Packages|`Maxine.Silk.WebGPU`, `.Extensions.WGPU`, `.Extensions.Dawn`, `.Native.WGPU` (native = wgpu-native **v29.0.1.1**, seven RIDs, `lib/*` only, no wasm asset exists)|
|Why native too|2.23.0's win-x64 `wgpu_native.dll` has 250 exports and lacks **47** of the 206 functions the header declares; v29 has 228 of the declared set, none missing|

Requirements for regeneration, each a silent failure: `libclang.dll` + `libClangSharp.dll` beside
the `SilkGenerate` host (else nothing parses); `output.licenseFile` must exist (else
`ProfileWriter.LicenseText` throws `ArgumentNullException: key`); `bakery.profileNames` must name the
header's base name and `clang.classes` map `{"webgpu.h": "[Core]WebGPU"}`; and
**`silk_touch_sealed_vtable_creation = true`** in `.editorconfig`, without which SilkTouch emits
scaffolding with 0 of 572 method bodies (`CS8795` ×572 + `ST0002 FORCE-USE-VTABLE`).

## Flag types come from `typedef WGPUFlags`, not from C enums

`webgpu.h` declares the flag sets as `typedef uint64_t WGPUFlags WGPUBufferUsage;` with the bit values
as `static const`. Keying `generator.json`'s `typeMaps` on the real typedef names (`WGPUBufferUsage`,
`WGPUMapMode`, `WGPUShaderStage`, `WGPUTextureUsage`, `WGPUColorWriteMask`, `WGPUInstanceBackend`) is
what makes the fields typed; Silk's `…Flags` guesses never matched, so every field came out `ulong`.
The hand-written enums are `: ulong` on purpose — a default-width C# enum is four bytes and would
shift every struct that carries one.

## What wgpu-native v29 does *not* implement

Each of these is a `panic!` inside the native library, which aborts the process: nothing in .NET can
catch it, and the only symptom is the process dying where the call was made.

|Stub|Consequence for the backend|
|---|---|
|`wgpuInstanceWaitAny`|wait-any is unusable, so `CallbackMode.WaitAnyOnly` callbacks never run. Every request uses **`AllowProcessEvents`** and is driven by `wgpuInstanceProcessEvents`, which calls the callback on the waiting thread — so captured locals still need no lock|
|`wgpuGetInstanceFeatures`, `wgpuHasInstanceFeature`, `wgpuSupportedInstanceFeaturesFreeMembers`|no way to query support|
|`TimedWaitAny` instance feature itself|requesting it makes `wgpuCreateInstance` panic ("Unsupported timed WaitAny features specified"), so the instance asks for nothing and the polling wait is the only form, not a fallback|

The `Maxine.WebGPU.Extensions.WGPU` surface is still referenced for `DevicePoll`, which is real and is
what a headless caller uses to drain validation that a present would otherwise drive.

## Porting traps the header bump exposed

|Trap|Rule|
|---|---|
|`RenderPassColorAttachment.DepthSlice`|zero is slice 0, not "none". A 2D view must carry `WebGPU.DepthSliceUndefined` (`0xFFFFFFFF`) or submit fails with "Depth slice was provided but the color attachment's view is not 3D"|
|`TextureViewDescriptor.Usage`|new field; the surface view needs `TextureUsage.RenderAttachment` or pass validation rejects it|
|`SurfaceGetCapabilities`|returns `Status` and allocates; `SurfaceCapabilitiesFreeMembers` is mandatory. It replaced `SurfaceGetPreferredFormat`, so the format choice now runs over `Formats[]` (Bgra8 → Rgba8 → first → fallback) in one place (`WebGpuSwapchain.PickSurfaceFormat`) shared by the boot log and `Configure`|
|`SurfaceGetCurrentTextureStatus`|`SuccessOptimal`/`SuccessSuboptimal` are both fine to draw; `Timeout`/`Error` leave ownership with wgpu (do **not** release the texture); only `Outdated`/`Lost` are released and reconfigured|
|`SurfacePresent`|returns `Status`, which the backend routes to `GraphicsDiagnostics`|
|`AdapterGetProperties` → `AdapterGetInfo`|`Description`/`Device`/`BackendType`, then `AdapterInfoFreeMembers`|
|`SupportedLimits`/`RequiredLimits`|gone; `AdapterGetLimits(Adapter*, Limits*)` and `DeviceDescriptor.RequiredLimits` is `Limits*`|
|`ImageCopyTexture`/`ImageCopyBuffer`/`TextureDataLayout`|`TexelCopyTextureInfo`/`TexelCopyBufferInfo`/`TexelCopyBufferLayout`|
|`ShaderModuleWGSLDescriptor`|`ShaderSourceWGSL`, `SType.ShaderSourceWgsl`, `Code` is a `StringView`|
|`WGPUDeviceSetUncapturedErrorCallback`|gone; `DeviceDescriptor.UncapturedErrorCallbackInfo`. Free the thunk with `PfnUncapturedErrorCallback.Dispose()` after the device is released|
|`DepthStencilState.DepthWriteEnabled`|`OptionalBool`, not `uint`|
|Entry points and labels|`StringView`, so pass `Utf8.View` (pointer + length), never the bare pointer|

## emdawnwebgpu links with the workload toolchain

`Microsoft.NET.Runtime.Emscripten.6.0.2.Sdk.win-x64` ships Emscripten **6.0.3**, which vendors the
`emdawnwebgpu` remote port (`tools/ports/emdawnwebgpu.py`, pinned **v20260423.175430**, needs
≥ 4.0.10). A probe that calls `wgpuCreateInstance` + `wgpuInstanceProcessEvents` compiles and links to
`probe.js`/`probe.wasm` with `--use-port=<pkg>/emdawnwebgpu.port.py`.

Traps, all hit:

|Trap|Rule|
|---|---|
|`project is on another drive` during port build|Emscripten resolves the port sources with `os.path.relpath(src, build_dir)`; a repo on `W:` with the cache on `C:` throws `ValueError: path is on mount 'C:', start on mount 'W:'`. Put `EM_CACHE` on the same drive as the port sources|
|`emscripten requires python 3.10 or above (C:\Python39\python.exe)`|the port's inner `em++.bat` picks up `PATH`'s python. Set `EMSDK_PYTHON` to the workload's python and put it first on `PATH`|
|`emdawnwebgpu requires C++`|link with `-sDEFAULT_TO_CXX` (or em++); the browser head's `EmccExtraLDFlags` already carries `-lc++ -lc++abi`|
|`clang executable not found`|`DOTNET_EMSCRIPTEN_LLVM_ROOT` is the pack's `tools/bin`, not `tools/emscripten/bin`; binaryen root is `tools`; node is `...Node.win-x64/.../tools/bin/node.exe`|

The canvas surface is a Dawn extension, not core: `WGPUEmscriptenSurfaceSourceCanvasHTMLSelector`
(`0x00040000`) with `selector = { "canvas", WGPU_STRLEN }` chained into `wgpuInstanceCreateSurface`.
The generator drops Dawn's duplicate `SType`/`FeatureName` tokens (the bakery adds core enums first
and `CheckForDuplicates` keeps the first by name), so those four members live in a hand-written
`DawnSType.cs` that regeneration leaves alone.

## The two blockers a Silk.NET version choice does not remove

1. **Async.** `emwgpuWaitAny` is `Asyncify.handleAsync`; the non-asyncify build aborts with
   `TODO: Implement asyncify-free WaitAny for timeout=0`. `.NET` wasm has no JSPI. The escape hatch is
   `WGPUCallbackMode_AllowProcessEvents` + per-frame `wgpuInstanceProcessEvents` — which is now what
   the desktop backend does anyway for wgpu-native's own reasons, so the browser and desktop paths
   agree.
2. **WGSL.** One bundle cannot satisfy both: naga (desktop wgpu-native) rejects
   `diagnostic(off, derivative_uniformity)`, Chrome's Tint *requires* it under non-uniform control
   flow. A per-target WGSL string is needed. See [[webgpu-vs-webgl2-shader-cost-on-the-web]].

`gfx-rs/wgpu-native#298` (open) is why compiling wgpu-native for wasm is not the route: its
emscripten target has no `map_surface` and fails with `Unsupported Surface` then `No suitable
adapter`; wgpu's own emscripten backend is WebGL2-only, i.e. the path the browser already ships.

## Why the namespace has no `Silk` segment

Generated code names `Silk.NET.Core` types in full (`Silk.NET.Core.Bool32`), so a namespace
containing `Silk` shadows them and the binding fails to compile. `Maxine.WebGPU` — the same reason
`Maxine.Silk.EGL` publishes `Maxine.EGL`. Publish-side package ids keep the `Maxine.Silk.` prefix.

**Callbacks are where `.From` is the wrong route for wasm.** `Pfn*Callback.From(d)` calls
`SilkMarshal.DelegateToPtr`, which resolves a static target through
`Delegate.Method.MethodHandle.GetFunctionPointer()` and anything else through
`Marshal.GetFunctionPointerForDelegate`. Both hand out a code address that the native side calls back
through, and this tree's established wasm constraint (recorded in
`wasm-pinvoke-table-and-gl-proc-addresses`, and the reason `GlNativeContext` exists) is that only a
managed `[UnmanagedCallersOnly]` address is callable that way. Each `Pfn*Callback` has an
`op_Explicit` from `IntPtr`, so a browser build builds the wrapper from a `[UnmanagedCallersOnly]`
trampoline instead. The backend has four `.From` sites, all in `WebGpuGraphicsDevice.cs`; all four
wrappers reach `DelegateToPtr` from their only constructor, and all four expose an `IntPtr`
conversion.
