---
name: archive-content-preparation-and-sx-editor-facts
description: "How a multiplayer race gets canonical archive content, and the Sx patterns the stage-editor port needed (no native drag/drop or context menus)."
metadata:
  node_type: memory
  type: project
  pinned: false
---

Facts from implementing in-game archive publishing/racing on top of the existing Sx UI.

- **Content must be prepared twice before a race.** Clients download cars *and* the stage (every
  client builds every player's car through `BackendGameSparker.GetCar`), the game master downloads
  only the stage (`BackendStage` is all it builds). `BackendGameSparker` loads an `archive/<hash>`
  id through `ContentCache`/`ArchiveMount`, so an unprepared id throws rather than downloading.
- **The race start is two-phase:** lobby freezes the roster → `S2C_PrepareContent` (nonce) to
  members + `POST /prepare-content` to the GM → `C2S_ContentReady` acks → create race from the
  frozen seat list. Anything stale (nonce mismatch, changed selection, leave, disconnect) faults
  a `TaskCompletionSource` and the awaiting task fails the start.
- **Lobby packet opcodes are nearly exhausted.** S2C uses -1..-11 and C2S 1..13; the opcode
  generator merges lobby *and* game packets into one switch, so `-7` (game's `S2C_GameFinished`)
  collides with a lobby packet and fails as an unreachable switch arm. Next free: -12, 14.
- **Sx has no drag-and-drop and no context menu.** The stage editor's hierarchy does it with
  `onmousedown` (records a drag candidate), `onmouseenter` (drop target once moved >4px) and a
  root `View.MouseReleased` set through `onmount`, which bubbles because release dispatch walks
  leaf→root. The context menu is an always-mounted absolute overlay toggled with
  `display: none/flex`; `Display.None` nodes are skipped by `FocusManager`'s hit test, so a hidden
  overlay does not swallow clicks.
- **Part thumbnails** are render targets wrapped with `TheGraphics.G.WrapTexture(rt.ColorTexture)`
  and drawn by `NFMWorld.DriverInterface.UI.Image`. A `Sx.Show` whose condition reads a
  `_previewRevision` signal remounts the image branch when a preview finishes; rows inside
  `Sx.For` do not re-run their builder for equal values.
- **The pending TT queue is write-root-only.** `TimeTrialSubmissions.List()`/`Recover()` now use
  `VirtualFS.EnumerateWriteFiles`, because a test process that mounts the developer's real user
  directory (a `BackendGameSparker` static init does) otherwise counts their historical submissions
  as this client's queue. The `MaxPending` bound also counts only `pending` entries: uploaded runs
  stay on disk for the browser, and counting them filled the queue permanently after 20 races.
