---
name: sokol-deferred-upload-must-cover-create-time-data
description: "In the sokol backend, a dynamic buffer created WITH initial data must register for upload; CreateBuffer marking the mirror dirty is not enough."
metadata: 
  node_type: memory
  type: project
  originSessionId: b3e2f796-05c3-43bb-af21-f1658d01feb1
  modified: 2026-09-25T04:40:41.261Z
---

The sokol backend buffers CPU writes in a `SokolBuffer.Mirror` and defers the single
`sg_update_buffer` allowed per resource per frame to draw time. `Track(...)` (which adds a
resource to the command buffer's `_pendingUploads`) was called only from `UpdateBuffer` /
`UpdateTexture` — so a buffer created *with* initial data was marked dirty but never
registered, and its contents never reached the GPU.

**Why it's hard to spot:** any caller that rewrites the buffer every frame uploads normally.
Only resources left alone after creation break. `RenderQueue` is exactly that shape — it
creates an instanced batch's vertex buffer with its first frame's contents and then skips
`UpdateBuffer` while the batch is unchanged. So the player's car (moves every frame) drew
correctly while static stage parts and outlines did not, which reads like corrupt instance
data rather than a missing upload.

**How to apply:** resources created with initial data register on the *device*
(`TrackCreated`/`DrainCreatedUploads`), not on the command buffer, because they are typically
created while a command buffer is already recording and it holds no reference to them.
`FlushUploads` drains the device list into its own before its emptiness check. Both
`CreateBuffer` and the updatable `CreateTexture` path need this — the texture half was the
same bug.

Related: [[sokol-index-format-is-baked-into-the-pipeline]]
