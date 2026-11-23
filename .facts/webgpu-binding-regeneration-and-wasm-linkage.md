---
name: webgpu-binding-regeneration-and-wasm-linkage
description: "The repo can regenerate Silk.NET.WebGPU from any pinned webgpu.h with its own SilkGenerate in ~6 s (the only blocker was SilkTouch's .editorconfig option), and Dawn's emdawnwebgpu port compiles and links with the .NET 11 Emscripten 6.0.3 workload — so a browser WebGPU backend needs no upstream Silk.NET contribution"
metadata:
  node_type: memory
  type: reference
---

Answers "compile Silk.NET 2.x wgpu for wasm, or add a WebGPU binding to Silk.NET 3.x": neither.
Measured 2026-11-23.

## Regenerating the binding ourselves

`Maxine.Silk.EGL/tools/SilkGenerate` is a thin `Silk.NET.BuildTools.Program.Main` passthrough and
builds bindings from any `generator.json`. Pointed at the current `webgpu.h` it parses **54 enums /
126 structs** and writes **184 files / ~5.4 k lines** in **~6 s**, with the modern signatures
(`Future InstanceRequestAdapter(Instance*, RequestAdapterOptions*, RequestAdapterCallbackInfo)`,
`{ byte* Data; nuint Length; } StringView`).

Three requirements, each a silent failure mode:

|Requirement|Failure when missing|
|---|---|
|`libclang.dll` + `libClangSharp.dll` beside the `SilkGenerate` net8.0 host|native asset is not a dependency of that project; nothing parses|
|`output.licenseFile` must exist|`ProfileWriter.LicenseText` throws `ArgumentNullException: key`|
|`bakery.profileNames` names the header's base name (`["webgpu"]`) and `clang.classes` is `{"webgpu.h": "[Core]WebGPU"}`|the profile is not recognised at all|

**The compile blocker is one `.editorconfig` line.** Without `silk_touch_sealed_vtable_creation = true`
SilkTouch emits the class scaffolding only — 0 of 572 method bodies — and the build dies with 572
`CS8795` plus 572 `ST0002 MethodClass failed: System.Exception: FORCE-USE-VTABLE`. The option is read
from the analyzer-config options of the syntax tree
(`NativeApiGenerator.cs`, `generateSeal`); Silk.NET's repo root enables it. With it, the same tree
builds clean and the analyzer emits ~2 800 lines of implementations that resolve each slot lazily
through `INativeContext.GetProcAddress("<entry point>")`.

## Drift: 2.23's binding vs the current header

16 of the 55 entry points the backend uses differ, so 2.23's structs cannot be passed to a current
Dawn/wgpu-native build:

- `BufferMapAsync` arity 6 → 5, returns `Future`.
- `SurfaceGetCapabilities` and `SurfacePresent` return `void` → `Status`.
- `wgpuDeviceSetUncapturedErrorCallback` no longer exists (it is `uncapturedErrorCallbackInfo` inside
  `WGPUDeviceDescriptor`).
- Every descriptor's `Byte* label` is `WGPUStringView label`, which shifts all struct offsets by 8
  bytes. The repo assigns no labels, so this costs nothing in edits — only in layout.
- Chained `SType` values diverge: `ShaderModuleSpirvDescriptor` 0x5 → `ShaderSourceSPIRV` 0x1,
  `ShaderModuleWgslDescriptor` 0x6 → `ShaderSourceWGSL` 0x2, `RenderPassDescriptorMaxDrawCount` 0xF →
  `RenderPassMaxDrawCount` 0x3, `SurfaceDescriptorFromWindowsHwnd` 0x2 → `SurfaceSourceWindowsHWND`
  0x5.

The backend is 3 089 lines in 8 files, calls **55 distinct API methods**, constructs **3 chained
structs** (`WebGpuCommandBuffer.cs`, `WebGpuGraphicsDevice.cs` HWND, `WebGpuResources.cs` WGSL) and
assigns **no labels**.

## emdawnwebgpu links with the workload toolchain

`Microsoft.NET.Runtime.Emscripten.6.0.2.Sdk.win-x64` ships Emscripten **6.0.3**, which vendors the
`emdawnwebgpu` remote port (`tools/ports/emdawnwebgpu.py`, pinned **v20260423.175430**, port needs
≥ 4.0.10). A probe that calls `wgpuCreateInstance` + `wgpuInstanceProcessEvents` compiles and links to
`probe.js`/`probe.wasm` with `--use-port=<pkg>/emdawnwebgpu.port.py`.

Traps, all hit:

|Trap|Rule|
|---|---|
|`project is on another drive` during port build|Emscripten resolves the port sources with `os.path.relpath(src, build_dir)`; a repo on `W:` with the cache on `C:` throws `ValueError: path is on mount 'C:', start on mount 'W:'`. Put `EM_CACHE` on the same drive as the port sources|
|`emscripten requires python 3.10 or above (C:\Python39\python.exe)`|the port's inner `em++.bat` picks up `PATH`'s python. Set `EMSDK_PYTHON` to the workload's python and put it first on `PATH`|
|`emdawnwebgpu requires C++`|link with `-sDEFAULT_TO_CXX` (or em++); the browser head's `EmccExtraLDFlags` already carries `-lc++ -lc++abi`|
|`clang executable not found`|`DOTNET_EMSCRIPTEN_LLVM_ROOT` is the pack's `tools/bin`, not `tools/emscripten/bin`; binaryen root is `tools`; node is `...Node.win-x64/.../tools/bin/node.exe`|

The canvas surface is a Dawn extension, not core:
`WGPUEmscriptenSurfaceSourceCanvasHTMLSelector` (`WGPUSType_... = 0x00040000`) with
`selector = { "canvas", WGPU_STRLEN }` chained into `wgpuInstanceCreateSurface`.

## The two blockers a Silk.NET version choice does not remove

1. **Async.** `emwgpuWaitAny` is `Asyncify.handleAsync`; the non-asyncify build aborts with
   `TODO: Implement asyncify-free WaitAny for timeout=0`. `.NET` wasm has no JSPI. The escape hatch is
   `WGPUCallbackMode_AllowProcessEvents` + per-frame `wgpuInstanceProcessEvents`, which is fully
   synchronous — but device creation must become a poll, not the 10 s `ManualResetEventSlim` block at
   `WebGpuGraphicsDevice.cs:257-320`.
2. **WGSL.** One bundle cannot satisfy both: naga (desktop wgpu-native) rejects
   `diagnostic(off, derivative_uniformity)`, Chrome's Tint *requires* it under non-uniform control
   flow. A per-target WGSL string is needed. See [[webgpu-vs-webgl2-shader-cost-on-the-web]].

`gfx-rs/wgpu-native#298` (open) is why compiling wgpu-native for wasm is not the route: its
emscripten target has no `map_surface` and fails with `Unsupported Surface` then `No suitable
adapter`; wgpu's own emscripten backend is WebGL2-only, i.e. the path the browser already ships.
