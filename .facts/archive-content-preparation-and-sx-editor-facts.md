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

- **The shared stage selector browses archive stages.** `StageSelectState.Stages` holds
  `StageOption(Id, Label, Subtitle)`, not raw ids; the LOCAL source fills it from
  `MenuPreferences.GetStages` (label = file name), the ONLINE source from
  `ArchiveContentClient.SearchAsync(..., "Stage", ...)` (id = `archive/<hash>`). Selecting an archive
  row runs `ContentCache.PrepareAsync(..., AssetKind.Stage, ...)` and only then `LoadStage`; RACE
  waits for that prepare, so a failed download never starts a race with an unprepared stage. The
  lobby's create dialog still uses `Compact`, which is local-only and accepts a typed archive id.
- **The garage is two side panels, not one sidebar.** `GarageView` keeps browsing (source tabs,
  search, collections, cars) in a 380-wide left panel and the selected car's stats plus SELECT CAR in
  a 380-wide right panel, leaving the middle clear for the 3D preview. Collection and car lists need
  `minHeight: 0` on their scroll box and `flexShrink: 0` on rows, or the surrounding column squeezes
  the list to nothing (the old single sidebar did exactly that).
- **Menu harness views**: `--sx-harness` `view` also accepts `garage` and `stage-select`, mounting
  those real views over synthetic `UiState.Garage` / `StageSelectState` fixtures. `assert-size`
  compares logical width/height times harness scale — but a `100%` height stays at device pixels, so
  do not scale-assert full-height panels. `Button.Create(name: ...)` names buttons for harness targets.

- **`Overflow.Scroll` is two-axis.** A vertical list gets a horizontal scrollbar as soon as one
  direct child (a wide row or a long empty-state/error message) exceeds its width. Both menu lists
  now keep their rows in a `width: 100%`, `flexShrink: 0` inner column, and status/no-match text
  uses `Sx.WrappingText` at 100% width; the harness asserts `ScrollableWidth == 0`. Echo long
  search queries in the no-match line, but left-align it — a centered wrapping line whose single
  unbreakable word overflows shifts the earlier lines off the left edge.
