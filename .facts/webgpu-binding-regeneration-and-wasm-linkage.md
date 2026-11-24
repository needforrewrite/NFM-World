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
|`--use-port=emdawnwebgpu.port.py` fails under the SDK's `emcc`|its `linker_setup` asserts `LINK_AS_CXX`, which the SDK's `emcc` does not set. Drive the build directly: `NFMWorld.Platform.WebGPU/native/build_wgpu.py` compiles `webgpu.cpp` and the `nfmw_*` shim to `-emit-llvm` bitcode and archives it as `wgpu_native.a` (the name the P/Invoke table and `[DllImport("wgpu_native")]` key on). `-fno-exceptions` is upstream's own flag there and is also what keeps the object out of the legacy exception model, which crashes `wasm-ld` in instruction selection|
|The four JS libraries must be `--js-library` in port order|`enum_tables`, `generated_struct_info`, `generated_sig_info`, `library_webgpu`. They cannot be compiled; pass them after the generated tables. On their way from an MSBuild property a **backslash in the path is dropped** by node's `readFileSync` (`W:projectsNFM-World…`), so the csproj rewrites `\` to `/` with `System.String.Copy(...).Replace('\','/')`|
|`-sUSE_WEBGPU` looks like the right switch|`tools/settings.py:258` marks it "No longer supported; replaced by `--use-port=emdawnwebgpu`, which implements a newer (but incompatible) version of `webgpu.h`", so emcc refuses it. This project takes neither: the port's sources are compiled here and its four JS libraries passed to the link|

The canvas surface is a Dawn extension, not core: `WGPUEmscriptenSurfaceSourceCanvasHTMLSelector`
(`0x00040000`) with `selector = { "canvas", WGPU_STRLEN }` chained into `wgpuInstanceCreateSurface`.
The generator drops Dawn's duplicate `SType`/`FeatureName` tokens (the bakery adds core enums first
and `CheckForDuplicates` keeps the first by name), so those four members live in a hand-written
`DawnSType.cs` that regeneration leaves alone.

## Shipped: the browser runs WebGPU (resolved 2026-11-24)

Both blockers below were solved and the browser head now defaults to WebGPU, with WebGL2 as the other entry in the
same renderer list. What the solution looks like:

- **The page creates the device.** `NFMWorld.Browser/wwwroot/main.js` calls `requestAdapter`/`requestDevice` before
  `dotnet.create()` and parks the result on `Module['preinitializedWebGPUDevice']`; `NFMWorld.Platform.WebGPU`'s
  `WgpuNativeContext.AcquireDevice()` calls `emscripten_webgpu_get_device()` and holds the reference. Nothing on the
  managed side waits on a future. Measured: `wgpuInstanceProcessEvents` spun 6.6M times and the `.then` never ran.
- **Per-target WGSL.** `IShaderModule.WgslBrowser` is a second member; `WgslCompiler` emits the desktop flavour with
  `diagnostic(off, derivative_uniformity)` stripped (naga rejects it) and the browser flavour with it kept (Tint
  requires it). `WebGpuResources.WgslFor` picks by `IsBrowser`.

## The two blockers a Silk.NET version choice did not remove

1. **Async — the blocking wait does not work, and `emscripten_webgpu_get_device` is what replaces it.**
   `emwgpuWaitAny` is `Asyncify.handleAsync`; the non-asyncify build aborts with
   `TODO: Implement asyncify-free WaitAny for timeout=0`. `.NET` wasm has no JSPI, so
   `wgpuInstanceWaitAny` is unusable.

   `CallbackMode.AllowProcessEvents` + `WebGpuInterop.WaitFor` does **not** substitute for it on the
   web. `wgpuInstanceProcessEvents` only dispatches futures already flagged ready
   (`webgpu.cpp:510-545`, `if (mMode == AllowProcessEvents && mIsReady)`), and `mIsReady` is set from
   the promise's `.then` (`library_webgpu.js:2079`). A `.then` is a microtask, so it runs only once
   the JS stack unwinds; a synchronous spin never lets it run. Measured in Node: a 400 ms busy-wait
   after `Promise.resolve().then(...)` saw `false` with 6.6M iterations. The browser head also drives
   `Tick` synchronously from `requestAnimationFrame` (`NFMWorld.Browser/Program.cs:186`), so blocking
   there would freeze the page anyway. Each `WaitFor` site (`WebGpuGraphicsDevice.cs:348` adapter,
   `:434` device, `:843` buffer map) would therefore burn its timeout and throw.

   **The browser arm must import a device instead of requesting one.** `webgpu.h:2345` declares
   `WGPUDevice emscripten_webgpu_get_device(void)`; it wraps whatever JS the page put in
   `Module['preinitializedWebGPUDevice']` (`library_webgpu.js:656-669`) through
   `emwgpuCreateQueue`/`emwgpuCreateDevice`. Measured: linking a caller that calls it against
   `webgpu.o` + the four JS libraries and running under Node prints a live device and queue pointer.
   So the page does `adapter = await navigator.gpu.requestAdapter(); device = await adapter.requestDevice()`
   before calling into .NET, and the backend's adapter/device request is skipped entirely.

   Consequences for the backend, all cheap:
   - `emscripten_webgpu_import_adapter` is **declared in `library_webgpu_generated_sig_info.js` but
     implemented nowhere** — a caller fails to link (`undefined symbol`). So there is no adapter, and
     `WebGpuGraphicsDevice.Adapter` stays null on the web.
   - Every adapter query the backend makes has a device-level equivalent that *is* implemented:
     `wgpuDeviceGetAdapterInfo`, `wgpuDeviceGetLimits`, `wgpuDeviceHasFeature`, `wgpuDeviceGetFeatures`
     (all JS-backed, so all need `nfmw_*` forwarders). Use those off the imported device.
   - `SurfaceGetCapabilities` takes the adapter but ignores it on this implementation
     (`webgpu.cpp:2159`: the parameters are unnamed), so a null adapter is fine there.
   - `SurfacePresent` aborts unconditionally (`library_webgpu.js:2622`), and `wgpuDevicePoll` does not
     exist at all — `Present` and `Poll` both have to become no-ops on the web. The canvas is
     composited when the rAF callback returns.
   - `BufferMapAsync` cannot be waited on either, so any `ReadTexture` readback is unavailable on the
     web (its only non-smoke callers are the stage editor's PNG export and the shadow-map debug view).
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
