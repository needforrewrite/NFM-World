---
name: webgpu-backend-research
description: "What the Silk.NET.WebGPU backend had to do that D3D11/GL did not: the tint shader path, pipeline variants per attachment format, a dynamic-offset UBO ring, 4-byte copy units, and the uncaptured-error callback that made a silent 870-byte copy failure visible"
metadata:
  node_type: memory
  type: reference
---

`NFMWorld.Graphics.WebGPU` is implemented (opt-in: `--backend=webgpu` or the video setting). Everything
below was verified against the real `wgpu_native` the package ships and the 26 generated shader bundles.

## Packages

|Package|What it is|
|---|---|
|`Silk.NET.WebGPU`|binding only|
|`Silk.NET.WebGPU.Native.WGPU`|`wgpu_native` for win-x64/x86/arm64, linux-{arm,arm64,x64}, osx-{x64,arm64}. **No wasm build** → desktop-only; the browser keeps WebGL2|
|`Stride.Dependencies.Tint`|the `tint` **CLI**, no .NET API; runtimes only for win-x64, linux-x64, osx-arm64|

## Shader path — HLSL → SPIR-V (existing) → tint → WGSL

Three build-time fixes, each found by a failing validation rather than by reading docs:

1. `--allow-non-uniform-derivatives` — 6 fragment stages sample under divergent control flow.
2. Strip tint's `diagnostic(off, derivative_uniformity);` — the naga inside this wgpu-native rejects
   `diagnostic` directives outright. That is a desktop-only concession: Chrome's Tint instead
   *requires* it here, and without it every one of the 16 pixel modules is refused with
   `'textureSample' must only be called from uniform control flow`. See
   [[webgpu-vs-webgl2-shader-cost-on-the-web]].
3. Strip the `return;` tint emits after `discard;` — naga rejects instructions after `return`.

Plus renumber SPIR-V decorations before tint: HLSL `b0`/`t0`/`s0` all land on set 0 binding 0, which
WebGPU refuses. Uniform → 0, textures → 1..T, samplers T+1.., one set. After that every module validates.

Rejected: **SPIR-V passthrough** (wgpu-native takes it, browsers do not) fails for the three vertex
shaders with an instance matrix — naga cannot make a matrix an entry-point input. **Tint at runtime** ships
a native binary per RID, which is not an option for a game install.

Uniform layout does not diverge: tint's WGSL struct offsets equal the HLSL reflection offsets for every
member of every module (diff 0), so the merged CPU block uploads verbatim.

## Traps that cost real debugging time

|Trap|Detail|
|---|---|
|Copy sizes are 4-byte units|a buffer whose logical size is not a multiple of 4 (an 870-byte index buffer = 435 × `ushort`) is rejected by *both* `queueWriteBuffer` and `copyBufferToBuffer`. wgpu drops the update and keeps drawing, so the only symptom is wrong geometry. Fix: allocate `WebGpuBuffer.GpuSize` rounded up and pad every copy length; the padding always starts past the logical size, so nothing reads it|
|Uncaptured errors are invisible|wgpu logs validation failures to stderr and carries on. `DeviceSetUncapturedErrorCallback` is the only channel the game sees; `ReportError` forwards each distinct message to `GraphicsDiagnostics.Warning` once (bounded set). This is what found the copy bug: 2,649 dropped updates in one boot with every frame still rendering|
|`surfaceTexture` is one frame|`BeginFrame` acquires it on the first pass that draws to the surface; `Present` releases the view, presents, *then* releases the texture. Releasing first *discards* the frame, so `SurfacePresent` fails with "Surface image is already acquired" (meaning the slot is empty)|
|`destroy` is not a refcount drop|wgpu invalidates a destroyed resource for every submission made after it, and the whole frame is one submit. Apos' glyph atlas and `ImmediateBatch3D` replace and dispose mid-frame, so `DeferDestroy` queues destroys until just after the next submit. Without it the process panics inside Rust|
|Pipelines bake attachments|colour/depth format + sample count are part of the pipeline; `Poly` alone draws into the swapchain and a single-sample shadow cascade, so variants are cached per (desc, formats, samples, filterability)|
|No multisampled surface|swapchain MSAA needs an offscreen colour texture at N with `resolveTarget` = surface view, plus a matching depth texture|
|Uniform writes are submit-ordered|`queue.writeBuffer` ordering is against submits only, so N writes to one UBO with draws between them collapse. Per-draw uniforms use a dynamic-offset ring; `UpdateBuffer` records `copyBufferToBuffer` from a staged chunk inside the recording|
|Bindings must all be present|`Nvg*` declare a sampler with no texture, so a dummy 1×1 texture/sampler fills every declared slot|
|Vertex locations|glslang assigns them in declaration order, not semantic index — see [[glslang-assigns-vertex-location-in-declaration-order]]|
|Stage interface|a fragment stage need not declare every vertex output; `WgslStageInterface.Reconcile` appends a dummy `@location(n)` parameter per missing one or pipeline creation fails|
|Scissor|always on in WebGPU; `ScissorTestEnabled = false` gets a full-viewport scissor|
|Shader cost is where it wins, on a cold cache only|measured against WebGL2 on one RTX 4070 SUPER: 71–167 ms of device creation plus a 107 / 426 / 819 ms pipeline for the boot / dash / all-features bundles, against 2 940 / 64 437 / 126 558 ms of ANGLE link for the same GLSL ES. Warm, both are single-digit ms and the device cost makes WebGPU slower. See [[webgpu-vs-webgl2-shader-cost-on-the-web]]|

## Format mapping

`Rgba8` → `Rgba8Unorm`, `Single` → `R32Float` (sampleable unfiltered; `float32-filterable` needed for
linear), `Depth24Stencil8` → `Depth24PlusStencil8` (attachment only), `Rgba32f` glyph atlas needs
`float32-filterable` for its linear sampler, `Dxt1/3/5` → BC1/2/3 needs `texture-compression-bc`.
`BlendOperation.Min/Max` and wireframe have no WebGPU equivalent (no callers). `HasBottomLeftFramebufferOrigin
= false`, same as D3D11, so the `Mad.fxh` shadow flip stays correct.

## Verification

`NFMWorld.Graphics.WebGPU.Smoke` brings up a real headless device and checks the things only this backend
can get wrong: every bundle's WGSL parsing, a 1× and 4× pipeline variant per bundle, a textured draw's
texel order, two draws reading their own dynamic-offset block, and `CheckUnalignedCopies` for the 4-byte
copy unit. The Sx harness (`main-menu-workshop.json`) renders pixel-identical to D3D11 at
`--sx-scale=1.25`; at scale 1 the two differ on ~0.15% of pixels, all on glyph edges (mean delta ~5,
no sub-pixel shift when one image is resampled onto the other) — coverage rounding where edges land on
integer pixel boundaries, not a layout, colour or gamma difference.
