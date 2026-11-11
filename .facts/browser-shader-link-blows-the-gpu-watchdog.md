---
name: browser-shader-link-blows-the-gpu-watchdog
description: "Chromium's ~100s GPU watchdog kills big ANGLE shader links, returning false with an EMPTY info log — not a varying mismatch."
metadata:
  node_type: memory
  type: reference
  originSessionId: 5edd5c60-20e0-4586-beb1-e02ac22fd517
  modified: 2026-10-30T02:55:51.648Z
---

A WebGL2 `getProgramParameter(LINK_STATUS)` that blocks ~100 s and returns **false with an
empty `getProgramInfoLog`** is Chromium's GPU watchdog killing the GPU process mid-link, not a
shader error. Confirmed on a real GPU (Edge, `--use-angle=d3d11`) with Apos.Shapes' monolithic
pixel shader (462 KB GLSL ES):

- watchdog on: LINK_STATUS false at 100.7 s, log `""`
- `--disable-gpu-watchdog`: the identical shader links **true at 135 s**

So the empty log is the tell. A real link failure names the mismatch.

**Why Apos.Shapes hit it:** ANGLE's HLSL translation is superlinear in shader size. Measured on
the 16 feature variants (same vertex stage, differing pixel entry point):

| pixel GLSL ES | link |
|---|---|
| 68 KB (no features) | 3.2 s |
| 119–134 KB | ~10 s |
| 315 KB | 70 s |
| 366 KB | 96 s |
| 410–462 KB | >100 s → killed |

The pre-split single shader was 462 KB, so it could never link in a browser. The fix is the
`ShapePixel(p, useDash, useFillGradient, useBorderGradient, useText)` split in
`Apos.Shapes/Source/Content/apos-shapes.fx` (16 techniques, `ShaderVariant.cs`,
`ShapeBatch.CreateVariant`/`SelectVariant`). Boot uses variant 0 and costs ~3 s.

**Still a limit:** the dash arms are ~4x the SSA count (468 → 2141 temporaries) and still take
70–96 s, so a *runtime* first draw of a dashed variant can still trip the watchdog. The
no-dash variants (2/4/6/8/10/12/14) all link in 3–24 s, and the game only ever draws fills and
strokes with a flat colour then glyphs, so it stays on those. Latent unless something starts
drawing dashes.

**Trap in the split:** `VertexShape.FeatureVariant` must decode the *packed* meta the way
`PackMeta` lays it out — border style at bits 10–13 and dash at bits 18–19. The first version
read `>>24` and `>>12`, which are always zero, so every vertex selected variant 0 and gradients
and glyphs silently lost their arms. Verify any change to it against `PackMeta` numerically
rather than by eye.

**How to apply:** when a browser shader link fails, check the size and the wall time before
believing the log. Test with `--disable-gpu-watchdog` to tell "too slow" from "actually invalid".
SwiftShader does not reproduce this — it links fast, so a green SwiftShader run proves nothing
here.

Related: [[angle-deferred-shader-compilation]], [[angle-hlsl-translation-of-apos-shapes-takes-minutes]], [[headless-angle-derivatives-are-zero]]
