---
name: angle-hlsl-translation-of-apos-shapes-takes-minutes
description: "Apos.Shapes' shader takes 2-3 minutes to translate on ANGLE/D3D11, dominating the OpenGL smoke test."
metadata: 
  node_type: memory
  type: reference
  originSessionId: b4c7d401-6252-4849-aef1-a58c171ff633
  modified: 2026-09-25T16:15:17.085Z
---

`NFMWorld.Graphics.OpenGL.Smoke`'s `MeasureAposCompile` reports **120-160 seconds** for
`Apos.Shapes.ShapeBatch`'s create+link on ANGLE over D3D11 (observed 2026-09-25, RTX 4070 SUPER).
The same line measures ~13 ms for the first draw. So the cost is entirely in ANGLE's HLSL
translation, not in this backend. DesktopGL's smoke test has no equivalent and runs in a second.

**Why it matters:** it makes the OpenGL smoke run look hung. A run whose first visible output is
`DEVICE: ...` and then nothing for two minutes is translating, not deadlocked. Timing out or
killing it looks identical to a hang.

**How to apply:** don't shorten the budget to "fix" a presumed hang, and don't treat the GL error it
leaves behind as a backend bug — when the budget runs out, GL_INVALID_OPERATION is left pending and
`DrainGlErrors` counts it as a failure, which made an otherwise-green run report
`SMOKE: FAILED (1 failure(s))`. `MeasureAposCompile` now drains that error with `GetError` directly
and reports it as not-counted, with the reason in its doc comment. If this timing changes, that
scaffolding is the first thing to revisit.
