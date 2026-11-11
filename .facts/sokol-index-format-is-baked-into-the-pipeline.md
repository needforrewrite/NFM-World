---
name: sokol-index-format-is-baked-into-the-pipeline
description: "sokol bakes index_type into the pipeline while the abstraction supplies it per buffer, and sg_apply_bindings cross-validates the two — so a mismatch silently skips the draw."
metadata: 
  node_type: memory
  type: project
  originSessionId: b3e2f796-05c3-43bb-af21-f1658d01feb1
  modified: 2026-09-25T04:40:44.714Z
---

`sokol_gfx` takes the index format from `sg_pipeline_desc.index_type`, whereas this app's
abstraction supplies it per buffer at `SetIndexBuffer` time. `sg_apply_bindings`
cross-validates the bound index buffer against the *applied* pipeline's `index_type`
(`sokol_gfx.h:25174-25179`): mismatch sets `_sg.next_draw_valid = false`, and `sg_draw` then
treats that as "skip" (`:27476`). A non-indexed draw against an indexed pipeline is exactly
the state an earlier indexed draw leaves behind.

**Why:** the draw is silently dropped — no exception, and the `VALIDATE_ABND_EXPECTED_NO_IBUF`
log line is the only signal. SetPipeline does not clear or renumber the index buffer, and
NanoVG binds one index buffer for its fan and strip paths and then calls non-indexed `Draw`
for the strip, so a stale binding is the normal case rather than a caller mistake.

**How to apply:** both halves must match, and neither implies the other — pick the pipeline
variant from `indexed ? _indexFormat : NONE` (keyed with the colour format, since a variant is
per-attachment too), *and* withhold `bindings.index_buffer` on a non-indexed draw. Track the
applied variant in its own field: `_pipelineDirty` is already false when only the indexing
changed. GL has no such coupling — `glDrawArrays` ignores the bound element buffer.

Related: [[sokol-deferred-upload-must-cover-create-time-data]]
