---
name: msdf-text-verify-in-a-fresh-batch
description: "Debugging MSDF text in NFM-World: check a glyph alone before blaming the shader, and read the tail expressions rather than assuming band semantics."
metadata:
  node_type: memory
  type: feedback
  originSessionId: 997bbef3-7270-4d3e-90b3-86b573cffb6d
  modified: 2026-09-28T17:26:28.912Z
---

When MSDF text in `Apos.Shapes` draws wrong, two habits save the most time:

**1. Draw the failing glyph in a batch of its own before touching the shader.** The bake, the
sign, the range and the field bytes can all be provably correct while the glyph still draws
nothing, because the fault is in atlas placement rather than in the glyph. A glyph that renders
alone but not alongside others is atlas exhaustion. See
[[msdf-field-box-must-fit-the-atlas]]. In-shader probes in `apos-shapes.fx` are a poor
diagnostic here: build caching, dead-code elimination (a probe that early-returns deletes the
sampler it was meant to inspect) and conditional-flow derivative restrictions all mislead. An
ASCII-art dump of the read-back framebuffer settled in one run what a dozen probes did not.

**2. Read the tail's own expressions instead of assuming the band convention.** Text outlining
is a band *outside* the glyph, not a stroke straddling the edge: with `d = dGlyph - lineSize`,
`borderMix` crosses over at `dGlyph = 0` (the glyph's own edge) and `edgeFade` is coverage of
the glyph grown by the width. The outline therefore grows the letter by the *full* width. The
old docs said half. Files: `Content/apos-shapes.fx` lines ~3097–3098 and ~3276.

**Why:** both mistakes come from trusting a plausible mental model over the code in front of
you, and each cost a long debugging session.

**How to apply:** verify against `SlugSmoke` (CPU) and `NFMWorld.Graphics.OpenGL.Smoke` (GPU)
before concluding anything. Visual confirmation in-game is the user's job, not mine — see
[[ask-user-to-verify-visuals-dont-inject-input]].
