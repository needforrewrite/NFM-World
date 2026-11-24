---
name: browser-shader-link-blows-the-gpu-watchdog
description: "Chromium's ~100s GPU watchdog kills big ANGLE shader links, returning false with an EMPTY info log — not a varying mismatch. Confirmed again on a cold shader cache: 2.9 s / 64 s / 127 s link for 67/315/463 KB, and 1–5 ms once Chromium's own program cache has the source."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-11-23T00:00:00.000Z
---

A WebGL2 `getShaderParameter(..., COMPILE_STATUS)` (or `getProgramParameter(LINK_STATUS)`) that
blocks on the order of 100 s and returns **false with an empty info log** is Chromium's GPU
watchdog killing the GPU process mid-translation, not a shader error. A real failure names the
problem — a deliberately corrupted 463 KB fragment source reports
`ERROR: 0:46: '{' : syntax error` in 2 ms. So: empty log + ~100 s wall time = watchdog. Test with
`--disable-gpu-watchdog` to tell "too slow" from "actually invalid".

## The cost is the link, and the status query is what waits

`compileShader`/`linkProgram` queue the work and return in 0–1 ms; ANGLE translates when the status
is read (see [[angle-deferred-shader-compilation]]). Split apart on one page, cold cache:

| variant | pixel GLSL ES | `COMPILE_STATUS` | `LINK_STATUS` |
|---|---|---|---|
| 0 (boot) | 67 KB | 13 ms | **2 940 ms** |
| 1 (dash) | 315 KB | 171 ms | **64 437 ms** |
| 15 (all features) | 463 KB | 263 ms | **126 558 ms** |

Compile is roughly linear in size; **link is superlinear**, which is the whole problem. The same
shader with the same bytes seen before links in **1–5 ms**.

## Cold is the real launches; warm is a lie unless you control for it

Chromium keeps its own on-disk program cache in the profile, keyed by the exact source text, and it
survives process restarts. Same page, same bytes, two process launches in one profile:

- first launch: `compile 138 ms, link 65 232 ms`
- second launch: `compile 151 ms, link 4 ms`

So an identical-source benchmark measures the **cache**, not the driver: appending `// nonce` to the
source took the same variant from 4 ms back to 64 s. **Always nonce the source when measuring
translation cost, and pair a cold run with a warm one in one page** — a run that only ever compiles
each variant once may be reading the cache for every one of them.

That also means the *game* pays these numbers only on a launch whose cache is empty: first run
after install, after a GPU driver update (Chromium keys the cache on the driver), after clearing
the profile, or when the bundle's WGSL/GLSL text changes. Every other launch is 1–5 ms.

**Nothing is available to the app itself.** `getSupportedExtensions()` on the WebGL2 context returns
33 entries with no `program_binary` match — no `OES_get_program_binary` — so
`GlProgramCache` (`NFMWorld.Graphics.OpenGL`) is a no-op under Chromium. Chromium's cache is
internal to the GPU process; the page cannot feed it, only benefit from it.

## Two ANGLE builds, two orders of magnitude apart

The game's own bundled ANGLE (`libEGL.dll`, "ANGLE 2.1.1 git hash 9b3d747fe894", the desktop EGL
path `GlGraphicsDevice` uses) is in the same class as Chromium's on a cold cache: one `ShapeBatch`
(variant 0, 67 KB) is **3 266 ms** create+link and 1 ms warm
(`NFMWorld.Graphics.OpenGL.Smoke`'s `MeasureAposCompile`: `cold 3283 ms, warm 2 ms`). Desktop has
no Chromium-style cache, which is exactly why `GlProgramCache` exists; the browser gets Chromium's
for free but loses it on every cache miss.

## Why Apos.Shapes hit it

ANGLE's HLSL translation is superlinear in shader size, and the pre-split pixel shader was 462 KB,
so it could not link in a browser at all. The fix is the
`ShapePixel(p, useDash, useFillGradient, useBorderGradient, useText)` split in
`Apos.Shapes/Source/Content/apos-shapes.fx` (16 techniques, `ShaderVariant.cs`,
`ShapeBatch.CreateVariant`/`SelectVariant`). Boot uses variant 0 and costs 2.9 s cold, variant 8
(text) 15–40 ms, so the path the game actually walks is fine; the ones it must avoid are the dash
arms (1/3/5/7/9/11/13/15 `useDash`), which are 64–127 s cold and will be killed on a first launch.
The game only ever draws fills and strokes with a flat colour plus glyphs, so it stays off them.

## Trap in the split

`VertexShape.FeatureVariant` must decode the *packed* meta the way `PackMeta` lays it out — border
style at bits 10–13 and dash at bits 18–19. The first version read `>>24` and `>>12`, which are
always zero, so every vertex selected variant 0 and gradients and glyphs silently lost their arms.
Verify any change to it against `PackMeta` numerically rather than by eye.

**How to apply:** when a browser shader fails, read the size, the wall time and the log together. An
empty log after a long block is the watchdog; a named error is the shader. SwiftShader does not
reproduce the watchdog — it links fast, so a green SwiftShader run proves nothing here. And never
conclude "shader link is cheap now" from a warm-cache measurement.

Related: [[angle-deferred-shader-compilation]], [[angle-hlsl-translation-of-apos-shapes-takes-minutes]], [[headless-angle-derivatives-are-zero]], [[webgpu-vs-webgl2-shader-cost-on-the-web]]
