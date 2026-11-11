---
name: gl-buffer-orphaning-is-not-the-cost
description: Orphaning dynamic buffer updates in the GL backends is falsified as the source of their CPU overhead.
metadata: 
  node_type: memory
  type: project
  originSessionId: 47c78e36-c219-4403-bcba-ea6e4c4d7cfa
  modified: 2026-09-25T16:15:20.305Z
---

Falsified 2026-09-25 by A/B: making `GlBuffer.Update` orphan before a whole-buffer write (in both
`NFMWorld.Graphics.OpenGL` and `NFMWorld.Graphics.DesktopGL`) produced **no measurable change** in
frame time or in PresentMon's `MsCPUBusy` distribution. The change was reverted - but see the
follow-up: the same idiom was later adopted anyway, for correctness rather than speed, as a
CPU-mirrored whole-buffer write (commit `540e5d00`). That is why this file exists: it is a
**performance** falsification, and it does not argue against the semantics now in the code. The
performance claim remains false and should not be restated — see
[[gl-dynamic-buffers-use-a-cpu-mirror]] for what the code actually does now.

**Why:** the theory was that `glBufferSubData` into storage the GPU may still be reading forces a
rename-and-copy or a stall. The `UploadUniforms` comment in `GlCommandBuffer` documents exactly that
cost measured at ~10 ms/frame, which made it look like the obvious culprit for the ~18 dynamic
buffers updated per frame (Sparks, Dust, Flames, Chips, FixFlare, FixHoop, RenderQueue instance
buffers, NanoVG, ImGui). It is not — that comment is about the uniform block only, and should not be
generalised to this path. Do not re-derive this fix.

**How to apply:** the per-draw overhead differs by backend but the frame loop, game logic and render
queue do not — everything in `Update()` (ticks, physics, render-queue building) runs identically on
sokol D3D11, which is fast. So the cost is in the GL-specific draw path. Note the GL backends are the
only ones with **no state dedup**: `FNA3DCommandBuffer.SetPipeline` and `SokolCommandBuffer.SetPipeline`
both early-out on `ReferenceEquals`, while `GlPipelineState.Apply` re-issues ~20 GL calls per
`SetPipeline` because `Clear` mutates blend/depth/stencil masks behind its back. Suspect that, but
measure before changing — see [[nfm-world-perf-measure-before-theorizing]].
