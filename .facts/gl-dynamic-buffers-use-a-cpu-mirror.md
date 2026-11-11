---
name: gl-dynamic-buffers-use-a-cpu-mirror
description: "How the two GL backends implement UpdateBuffer, and why the upload is eager rather than deferred."
metadata: 
  node_type: memory
  type: project
  originSessionId: b4c7d401-6252-4849-aef1-a58c171ff633
  modified: 2026-09-25T16:15:17.038Z
---

Both `GlBuffer` implementations (`NFMWorld.Graphics.OpenGL`, `NFMWorld.Graphics.DesktopGL`) keep a
CPU mirror for `BufferUsage.Dynamic` buffers and write the **whole** buffer with `BufferData` on
every `UpdateBuffer` — the discard semantic, so no fence on storage a draw may still be reading.
`BufferSubData` is gone from the update path entirely. Committed 2026-09-25 as `540e5d00`
("CPU mirrored buffer to avoid BufferSubData").

**Why:** the user asked for XNA-style discard, then for the full sokol-style CPU mirror so that
sub-range updates stay non-destructive. The mirror is what makes a whole-buffer `BufferData`
equivalent to a sub-range patch. Sokol's backend has the same `Mirror` for the same reason.

**How to apply:** the upload happens **eagerly in `Update`**, not deferred to a draw or a frame
boundary. Deferring is a trap worth not re-entering: it seems to buy coalescing (Sparks/Dust/Flames
update per draw), but it cannot be made correct without tracking every buffer that might be read, and
the two cases that break it pull in opposite directions — a buffer created *with* its contents and
drawn without an update (RenderQueue's instance buffers do exactly this) needs flushing with no
update, while a buffer updated and never drawn needs uploading with no draw. Two attempted designs
each failed one case: an update-driven pending list failed the first, a bound-buffer set failed the
second and needed an extra `Submit` hook. Eager upload needs no state at all. The smoke tests in both
GL backends assert the create-with-data-and-draw case twice for this reason.

Note the performance angle is a separate, already-falsified question — see
[[gl-buffer-orphaning-is-not-the-cost]]. Do not sell this change as a speedup.
