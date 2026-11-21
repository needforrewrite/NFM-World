# Silk.NET.WebGPU backend: what it takes

Researched against the tree at `feat/lua-v2-v2` with a throwaway probe (`scratch/WgpuProbe`, gitignored).
Everything marked verified was run against the real `wgpu_native` shipped by the package and all 26
shader stages in `obj/*/ShaderCompiler/*.g.cs`.

## Packages

|Package|What it is|
|---|---|
|`Silk.NET.WebGPU` 2.23.0|binding only|
|`Silk.NET.WebGPU.Native.WGPU` 2.23.0|19 MB; `wgpu_native` for win-x64/x86/arm64, linux-{arm,arm64,x64}, osx-{x64,arm64}. **No wasm build** → desktop-only backend|
|`Stride.Dependencies.Tint` 2026.9.27|`tint` **CLI**, no .NET API. Runtimes only for win-x64, linux-x64, osx-arm64|

## Shader path — verified

`HLSL -> SPIR-V (existing shaderc step) -> tint --format wgsl`. Three build-time requirements, each
found by a failing validation, not by reading docs:

1. `--allow-non-uniform-derivatives`. 6 fragment stages sample under divergent control flow.
2. Strip the `diagnostic(off, derivative_uniformity);` line tint emits. The naga inside this
   wgpu-native rejects `diagnostic` directives outright.
3. Strip the `return;` tint emits after `discard;`. naga rejects instructions after `return`.

Then renumber SPIR-V binding decorations before tint: HLSL `b0`/`t0`/`s0` all land on set 0 binding 0,
which WebGPU refuses. Uniform → 0, textures → 1..T, samplers → T+1.., one set. After that all 26
modules validate.

Rejected alternatives:

- **SPIR-V passthrough** (wgpu-native accepts it, browsers do not) fails for the three vertex shaders
  that take the instance matrix: naga cannot make a matrix an entry-point input
  (`Argument 5 varying error ... cannot be used for user-defined entry point inputs or outputs`).
  Tint's WGSL splits it into four `vec4` locations, which is why WGSL also fixes that.
- **Tint at runtime** — ships a native binary per RID; not an option for a game install.

### Uniform layout: no divergence (verified)

Tint's WGSL struct member offsets equal the HLSL reflection offsets for every member of all 26 modules
(diff 0). The merged CPU-side block uploads verbatim; the 588-byte max block is well under the 64 KB
uniform cap.

### Vertex locations are declaration-order, not semantic slots (verified)

Poly_Basic_Vertex's instance matrix arrives as `@location(5..8) vec4`, and the trailing `parameters`
at 9 — glslang assigns locations in declaration order (`glslang-assigns-vertex-location-in-declaration-order.md`).
So the backend must map `VertexLayoutDesc.Semantic/Slot` → location via reflection. That mapping does
not exist in `ShaderReflection` today (only `ExpectedVertexLayout`, unused); D3D11 and GL match by
semantic name at runtime instead.

## Abstraction seams (no interface change strictly required, one reflection addition)

|Seam|What the backend must do|
|---|---|
|Pipeline attachments|WebGPU bakes color/depth format + sample count into the pipeline. `Poly` draws into both the swapchain (`Rgba8`) and shadow targets (`Single`). Create variants lazily, cached by (desc, colorFmt, depthFmt, samples)|
|Per-draw uniforms|`queue.writeBuffer` is ordered only against submits, so N writes to one UBO all land before the submit. Need a dynamic-offset ring (`minUniformBufferOffsetAlignment` 256) or one buffer per draw|
|`UpdateBuffer`|Same hazard: a buffer rewritten twice in a frame with draws between collapses. Record `copyBufferToBuffer` from a per-frame staging arena instead — copies are ordered inside the recording|
|Clears|`LoadOp.Clear` at pass start. Valid: every `Clear` follows `SetRenderTarget` and precedes that target's draws (`Scene.cs:72,82`, `StageMinimap.cs:143`, `WorldGame.cs:1247`)|
|Passes|Begin/end at `SetRenderTarget`/`Submit`; the frame is one `Submit` then `Present` (`WorldGame.cs` ~1247-1300)|
|Bind groups|Every declared binding must be present. `Nvg*` declare a sampler with no texture state → `PARTIALLY_BOUND` layout or dummy 1×1 texture/sampler|
|Scissor|Always on. `ScissorTestEnabled = false` ⇒ supply a full-viewport scissor|
|Spirv reflection|Add semantic→location per stage input, or the vertex layout cannot be built|

Format/wire constraints:

- `Rgba8` → `Rgba8Unorm`, `Single` → `R32Float` (sampleable unfiltered; `float32-filterable` only for linear),
  `Depth24Stencil8` → `Depth24PlusStencil8` (attachment only, not sampleable), `Rgba32f` glyph atlas
  needs `float32-filterable` for its linear sampler, `Dxt1/3/5` → BC1/2/3 needs `texture-compression-bc`.
- No multisampled surface. Swapchain MSAA needs an offscreen color texture at `sampleCount` N with
  `resolveTarget` = surface view, plus a depth texture at the same count.
- `BlendOperation.Min/Max` have no WebGPU equivalent (no callers today); wireframe unsupported;
  strip topologies need `stripIndexFormat` (none used — only `LineList`/`TriangleList`).
- `ReadTexture`: `copyTextureToBuffer` with a 256-byte-aligned `bytesPerRow`, `mapAsync`,
  `DevicePoll(wait)`, then repack.
- Surface: `SurfaceDescriptorFromWindowsHWnd` from `SdlWindow.NativeWindowHandle` + `GetModuleHandle(null)`.
  Windows first; Linux/macOS need Xlib/Wayland/MetalLayer handles from `SDL_GetWindowWMInfo`.
- `HasBottomLeftFramebufferOrigin = false` (same as D3D11); the `Mad.fxh` shadow flip stays correct.

## Effort shape

D3D11 is 8 files / ~3,400 lines; expect a WebGPU backend at parity (`Device`, `CommandBuffer`,
`PipelineState`, `Resources`, `Mapping`, `Swapchain`, `ShaderModule`, interop) plus a `.Smoke` harness
(the D3D11 one is 2,200 lines). Compiler side: WGSL emission + tint invocation + decoration rewrite
(~300 lines) and reflection locations (~60). Host side: `Renderer` enum arm, window creation without a
graphics API, settings row.
