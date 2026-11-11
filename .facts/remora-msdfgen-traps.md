---
name: remora-msdfgen-traps
description: "Remora.MSDFGen 2.0.0's MSDF path is half-texel broken and its range is in shape units; verified workarounds."
metadata:
  node_type: memory
  type: reference
  originSessionId: 997bbef3-7270-4d3e-90b3-86b573cffb6d
  modified: 2026-09-28T13:00:09.114Z
---

`Remora.MSDFGen` 2.0.0 (MIT, fully managed, no native deps — chosen over `SeasonMSDF`, whose package carries an `msdfgen-ext` P/Invoke facade) is the MSDF baker for Apos.Shapes text. Verified by baking 'o', 'A', 'l', '7', '|' at 48 px/em against brute force: median reconstruction is exact (mean |err| 0.000–0.028 texels), total ink matches stb's own rasterizer within 0.3%.

Three traps, all silent, all confirmed empirically:

1. **`EvaluateMSDF` is off by a half texel.** It does `p = (Vector2(x, y) - translate) / scale; p += (0.5, 0.5);` where msdfgen does `(x + 0.5) / scale - translate`. The 0.5 lands in *shape* units (half an em at 48 px/em) and `translate` is scaled twice. Invisible when `scale == 1`, which is why the library's own tests miss it. `GenerateSDF` is correct, so the two fields disagree. Workaround: pass MSDF an adjusted translate `scale*translate + 0.5*(scale - 1)` — the mismatch is a pure affine offset, so this cancels it exactly.

2. **`range` is in shape units, not texels.** Encoding is `sd/range`, and `sd` is in shape units because `p` is. At scale 1 the two coincide. For a texel range of 8 at 48 px/em, pass `8/48`. Wrong units cost the texel range `scale` times over, saturating the whole atlas.

3. **`CorrectErrors(Pixmap<Color4>, …)` is legacy `detectClash`**, not msdfgen's stencil `minDeviationRatio` algorithm. Threshold is `t` in encoded units with a `>=` test; the long-standing C# port value `0.3f` corresponds to `minDeviationRatio(10/9)/range(4) = 0.2778`, so `1.111/RangeTexels` is the faithful value. Only touches RGB — alpha survives, which is what lets MTSDF be assembled by hand.

Also: `Shape.Normalize()` does *not* orient contours (it only splits single-edge contours); do orientation yourself — outermost positive, alternating by containment parity, since `Contour.Winding` is get-only. `EdgeColoringSimple`'s angle threshold is **radians** despite msdfgen's docs saying degrees (the source does `Math.Sin(angleThreshold)`), so 3° is `3*π/180`, not `3`. There is no MTSDF method: call `GenerateMSDF` + `GenerateSDF`, assemble `Color4{R,G,B,A=trueDistance}`, then `CorrectErrors`.

See [[sokol-d3d11-hlsl-semantic-names-must-match-signature]] for the sibling class of "the library silently does the wrong thing" trap.
