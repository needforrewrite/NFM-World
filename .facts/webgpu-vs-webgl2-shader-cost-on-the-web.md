---
name: webgpu-vs-webgl2-shader-cost-on-the-web
description: "Measured on one real GPU with cold and warm caches separated: WebGPU costs 138–167 ms to create a device plus 107–819 ms for a cold pipeline against 2.9–127 s for the same shader in WebGL2 — so WebGPU is 10–150× faster on a cold cache, and the reason it is not usable is the shipped WGSL failing Chrome's validator"
metadata:
  node_type: memory
  type: reference
---

Answer to "would WebGPU on the web be faster, since we ship WGSL?" — **on shader cost, yes, by an
order of magnitude; but the shipped WGSL does not validate, and the browser cannot switch to it
anyway.** Both halves were measured on one RTX 4070 SUPER headless Chromium, ANGLE/D3D11 for GL.

## The browser is WebGL2 today, and that is not a flag

`NFMWorld.Browser/Program.cs` calls `WebGlContext.Create()`; `WorldGame`'s constructor takes
`case Renderer.Angle when _browserContext is not null` into `GlGraphicsDevice`. `--backend=webgpu`
cannot be used either way: `Silk.NET.WebGPU.Native.WGPU` ships no wasm runtime, and the surface is
built from the SDL window's Win32 handle, so `ParseRenderer` refuses it before a window exists.
`navigator.gpu` is present in the page; nothing in the game touches it.

## Measured costs, cold and warm separated

Cold = source text never compiled in this profile before; warm = the same bytes a second time. A
single-page pair per variant, so both numbers come from the same process:

| variant | WebGL2 link cold | WebGL2 link warm | WebGPU pipeline cold | WebGPU pipeline warm |
|---|---|---|---|---|
| 0 (67 KB, boot) | **2 940 ms** | 1 ms | **107 ms** | 2 ms |
| 1 (315 KB, dash) | **64 437 ms** | 4 ms | **426 ms** | 5 ms |
| 15 (463 KB, all) | **126 558 ms** | 5 ms | **819 ms** | 6 ms |

Plus WebGPU's device: `requestAdapter` + `requestDevice` = **71–167 ms**, which WebGL2 does not pay.
Module validation is 8–58 ms cold, 2–9 ms warm.

So: **cold, WebGPU is 27× (boot shader) to 150× (worst bundle) faster than ANGLE**; warm, both are
a few milliseconds and WebGPU's device creation makes it the slower option. WebGL2's warm number is
Chromium's own program cache, keyed by source text and surviving process restarts, and the page
cannot influence it (no `OES_get_program_binary`).
See [[browser-shader-link-blows-the-gpu-watchdog]] — a benchmark on fixed source text measures the
cache, not the driver.

Which matters depends on the launch: a **first** launch (after install, GPU driver update, profile
clear, or any shader-text change) is the cold case and WebGL2 pays 3 s of boot stall and cannot
reach the dash arms at all (64–127 s → watchdog); every later launch is the warm case. The bug this
replaces was arguing from a warm measurement that link was cheap, so the old cold numbers were
discarded as obsolete — they are not.

## The shipped WGSL is not usable by a browser at all

All 16 pixel modules fail Chrome's Tint validator, each with exactly one error:
`'textureSample' must only be called from uniform control flow`, at the dither path — e.g.
`Shapes0` 1270:28 through `Shapes15` 7724:32:

```wgsl
let v_622 = ((select(textureSample(BlueNoiseTex, BlueNoiseSampler, (v_620 * vec2<f32>(0.015625f))).x,
                     v_621, (v.dither_mode < 0.5f)) - 0.5f) * ...
```

`select` evaluates both arguments, so the sample sits under non-uniform control flow. Prepending
`diagnostic(off, derivative_uniformity);` makes **0/16** invalid and pipeline creation then succeeds.
`WgslCompiler.PostProcess` strips that exact directive because the naga inside the pinned
`wgpu_native` rejects it: the desktop consumer needs the line gone and the browser consumer needs it
present. `NFMWorld.Graphics.WebGPU.Smoke` cannot catch it — it runs naga, not Tint.

**How to apply:** if web WebGPU is pursued, the shader cost argument is now in its favour, but the
work is a wasm-capable wgpu binding, a surface off wasm canvas, a browser WGSL flavour that keeps
the directive, and a Tint validation step in the build or smoke. Do not quote a warm-cache GL
number to argue either way.
