---
name: headless-angle-derivatives-are-zero
description: "NFMWorld.Graphics.OpenGL.Smoke's headless ANGLE context returns 0 from ddx/ddy, so every shape draws hard-edged and no rendered check there can see an AA-band change."
metadata:
  node_type: memory
  type: project
  originSessionId: 997bbef3-7270-4d3e-90b3-86b573cffb6d
  modified: 2026-09-28T19:07:07.515Z
---

In `NFMWorld.Graphics.OpenGL.Smoke` (headless ANGLE, `GlGraphicsDevice.CreateHeadless`),
`ddx`/`ddy` in the fragment shader return **0**. Apos.Shapes' `PixelFootprint`
(`apos-shapes.fx`) is built entirely from those, so it collapses to `(0,0)` and
`aaSize = footprint.y * aaPixels` becomes 0: every edge is a hard step, for **every** shape,
not just text. `AAStyle` (and any AA width at all) is unobservable there — a glyph renders
byte-identical at an aaSize of 1.5, 6 or -1.5.

**Why:** the suites read pixels back through `glReadPixels`, so the missing derivatives look
like correct rendering rather than a failure. A test asserting "the two AA styles draw the
same" passes trivially and guards nothing — that is exactly how a real regression survived
this suite.

**How to apply:** to prove a derivative-dependent change, force the shader's footprint to a
constant (`return float2(1.0, 1.0);`) and check that AA appears — that is the control. Assert
the *decision* on CPU instead (`SlugSmoke`, which has `InternalsVisibleTo` on Apos.Shapes),
not on rendered pixels. A plain `DrawCircle` is the quick tell: if it has zero partial-coverage
pixels, the derivatives are dead in that context. The game itself runs `Renderer.Angle` with a
real window, so this may be specific to the pbuffer path, not ANGLE generally. See
[[gl-smoke-tests-never-switch-pipelines]] for the same class of trap.
