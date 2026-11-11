---
name: thin-quad-varyings-need-nointerpolation
description: A per-primitive constant passed as a normal varying over long thin triangles (line quads) arrives noisy by ~1px on real GPUs; mark it nointerpolation.
metadata:
  node_type: memory
  type: project
  originSessionId: 76bfb60d-e04d-44e3-b5bd-13634e358988
  modified: 2026-11-03T11:43:02.743Z
---

On D3D11 (user's GPU, 2026-11-03), Line.fx pixel outlines passed line endpoints as an ordinary varying — identical at all 4 quad vertices — yet each pixel saw values off by up to ~1 px, so the Bresenham test speckled. Long thin quads (≈5 px × hundreds) make the perspective plane setup ill-conditioned. Interpolated clip position and ddx/ddy on it were equally noisy. `nointerpolation` (spirv-cross emits `flat` in GLSL) fixed it instantly.

**Why:** cost three debug rounds of chasing grid/resolution/derivative theories while the math was correct offline.
**How to apply:** any per-primitive data a pixel shader must read bit-exactly (endpoints, ids, widths) goes in a `nointerpolation` varying; for the pixel's own position use SV_Position, not an interpolated clip position. A per-line hash color is the quick diagnostic — it must be one solid color per line.
