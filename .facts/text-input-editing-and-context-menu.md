---
name: text-input-editing-and-context-menu
description: "TextInput/TextArea clipboard, editing shortcuts, and the right-click menu's geometry and propagation traps."
metadata:
  node_type: memory
  type: project
---

`TextInput` (single line) and `TextArea` (multiline) now share **one process-wide clipboard**:
`TextInput.DefaultGetClipboardText` / `DefaultSetClipboardText`, wired once in
`WorldGame.Initialize` to `SdlWindow.GetClipboardText`/`SetClipboardText`. A per-field
`GetClipboardText`/`SetClipboardText` still wins, but nothing in the game sets one any more
(`ModelEditorView` used to; that wiring is gone). `TextArea` reads the same statics through its own
`ResolveGetClipboard`/`ResolveSetClipboard`.

- **SDL's emscripten video backend implements no clipboard** (`src/video/emscripten/` has no
  `SetClipboardText`/`GetClipboardText` driver hook). SDL therefore falls back to its *internal*
  clipboard (`SDL_GetInternalClipboardData`), so copy/paste works inside the browser build but
  never touches the browser's system clipboard. Not a bug to chase without a JS-side implementation.
- `TextInput` shortcuts: Ctrl+A/C/X/V, Shift+arrow/Home/End, plus a right-click menu
  (Cut/Copy/Paste/Select All). `Escape` closes the menu. `TextArea` has the same menu: it is one
  shared component, `NFMWorld.UI/Components/TextEditContextMenu.cs`, parameterised by four
  callbacks + three capability probes. Its keyboard shortcuts call the same methods
  (`SelectAll`/`CopySelection`/`CutSelection`/`PasteClipboard`) so the two paths cannot drift.

**`Component.IgnoresScrollOffset` (new framework flag).** `TextArea` is an `Overflow.Scroll`
container, and an absolutely positioned child of one does two wrong things: paint offsets it by the
container's scroll amount (so the menu slides away from where it was placed) and `UpdateScrollExtent`
counts it (so opening a 106px menu over a long document adds 106px of scrollable space - measured
0 -> 26 on a small fixture, 471 -> unchanged on the real editor). The flag makes paint use the
unscrolled origin and drops the node from the extent. It is still clipped by the container.

- **The cull has to use the same origin.** `PaintChildren` tested `IsOutsideClip` against the
  *scrolled* origin while painting from the unscrolled one, so once the editor scrolled the menu was
  culled as "above the viewport" and nothing painted - while every layout/geometry assertion still
  passed. Found only by profiling the rendered PNG (all four rows had identical ink profiles).
  `IgnoresScrollOffset_PopupStaysPaintedAfterScrolling` guards it, asserting `ScissorRect.Width > 0`
  because a culled node is left with an empty `ClipRect` (which is also what stops it taking presses).

Traps that cost a round each:

- **The menu is a child of the field.** It is an absolutely positioned `View` so it follows the
  field through any move, which means a press on it would otherwise bubble to the field's own
  `OnMousePressed` and re-open/re-place the menu under the pointer. The menu and every row call
  `StopPropagation`; a *disabled* row must still swallow the press, or the click falls through to
  the field. The secondary press on the field also stops propagation — otherwise a dialog backdrop
  that dismisses on press (`ConfirmDialog`, the stage editor overlay) closes the menu as it opens.
- **`Styles` lengths are unscaled.** `Component.OnStylesChanged` multiplies every length by
  `G.Scale`, so `OpenContextMenu` divides the mouse position by the scale before placing. The
  placement clamp converts `ScissorRect` (screen space) back into the field's own padding-box
  space, or a field near the bottom opens a menu partly off-screen.
- **`MenuHeight` must include the border.** It is `4*row + 2*padding + 2*border`; omitting the
  border made the clamp overshoot by exactly 2px and `ContextMenu_StaysInsideTheVisibleRegion`
  caught it. That test re-lays out after opening — the menu has no box until the next layout pass,
  so asserting on stale geometry passes even with the clamp removed.
- **The menu's enabled state is recomputed per frame** (`GameTick` → `ContextMenu.Refresh`), so it
  tracks typing and selection while open. Cheap because `MenuRow.SetEnabled`/`SetHover` early-out
  when nothing changed.
- **Right-click keeps an existing selection** and only moves the caret when there is none — matching
  browsers, so Cut/Copy act on what the user had selected and Paste lands at the click point.

Visual harness: `--sx-harness=NFMWorld/UiHarness/text-input.json` (`view: "text-input"`). It
installs an in-memory clipboard because the harness never runs `WorldGame.Initialize`. Harness
script additions made for this: `click` takes `"button": "secondary"`, and `key` takes
`"ctrl": true`.

See also [[sx-csharp-ui-traps]].
