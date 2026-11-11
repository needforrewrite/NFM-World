---
name: msdf-field-box-must-fit-the-atlas
description: "A glyph whose baked field is taller than the MSDF atlas rows draws nothing at all, silently."
metadata:
  node_type: memory
  type: project
  originSessionId: 997bbef3-7270-4d3e-90b3-86b573cffb6d
  modified: 2026-09-28T17:26:28.873Z
---

In `Apos.Shapes`, `MsdfGlyph`'s baked field is the glyph's own box grown by `RangeTexels/2`
plus a gutter on every side. At `EmPx = 48` and `RangeTexels = 16` that is already 80–120 rows
for an ordinary Latin letter, so `MsdfTable.MaxRows` has to be well past 256 and the shelf
packer has to actually use each shelf's width.

Two separate faults made glyphs vanish entirely, and neither reported anything:

1. **The shelf packer walked shelves monotonically.** It only ever looked at the newest shelf,
   so a glyph a row or two taller than the last opened a fresh shelf and permanently wrote off
   the horizontal room of every shelf before it. Four glyphs of rising height exhausted a
   256-row atlas while using ~2% of each shelf. Fixed by scanning for the first shelf a
   rectangle fits on, and by rounding shelf height up to `ShelfRows` (32) so similar glyph
   heights share a shelf instead of one shelf per distinct height.
2. **`MaxRows = 256` was too few** for even a couple of alphabets at this bake resolution, so
   the table evicted (or refused) while a glyph was still needed. Raised to 2048.

**Why:** the failure mode is a glyph that draws *absolutely nothing* — no ink, no error, and
the field itself bakes perfectly when checked in isolation. That last part is the trap: a
per-glyph check passes, so the bug looks like a shader or sampler problem and sends you into
the wrong file entirely.

**How to apply:** if a glyph draws nothing, check it in a *fresh* batch first. It rendering
alone but not alongside others is atlas exhaustion, not a draw bug. The regression test is
`SlugSmoke`'s shelf-packer check (CPU, no GPU) — see [[msdf-bake-traps]]. Related:
[[remora-msdfgen-traps]].
