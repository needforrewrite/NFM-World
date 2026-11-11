---
name: gl-smoke-tests-never-switch-pipelines
description: "The GL smoke tests draw with one pipeline and never switch, so they cannot catch state-application bugs across a SetPipeline change."
metadata: 
  node_type: memory
  type: project
  originSessionId: 47c78e36-c219-4403-bcba-ea6e4c4d7cfa
  modified: 2026-09-25T13:28:35.661Z
---

Every draw check in `NFMWorld.Graphics.{OpenGL,DesktopGL}.Smoke/Program.cs` binds one pipeline and
then draws repeatedly without ever binding a different one. Verified 2026-09-25: a deliberately
broken `SetPipeline` that updated the pipeline pointer but applied no state (`if
(!_pipelineStateLostToClear) return;` after the assignment) passed **both** smoke suites in full.

**Why:** the failure it produces — the previous pipeline's depth/blend/cull state left running under
the new shader — is invisible when only one pipeline is ever bound, because the state that should
have been applied is the state already in force. The same run showed up in the game as a dead
z-buffer and black quads around everything the changed pipeline drew.

**How to apply:** when touching anything that caches or skips GL state application, a green smoke run
is not evidence. Add a check that binds pipeline A, draws, binds pipeline B, draws, and asserts on
something only B's state can produce — `CheckPipelineSwitchAppliesState` in the DesktopGL suite is
the template (it clears depth to 0 and asserts 0 pixels are drawn, which only holds if the
depth test from the switched-to pipeline actually took effect). Note that suite has the check and
`NFMWorld.Graphics.OpenGL.Smoke` does not, as of 2026-09-25. See
[[gl-buffer-orphaning-is-not-the-cost]] for the surrounding investigation.
