---
name: sx-scroll-needs-bounded-parent
description: "A flexGrow scroll container only scrolls when its ancestor bounds its height; an auto-height dialog grows to fit and the list never scrolls."
metadata:
  node_type: memory
  type: project
---

The lobby's create dialog stage list appeared unscrollable. The selector itself was correct
(`flexGrow: 1`, `flexShrink: 1`, `minHeight: 0`, `overflow: Scroll`, `flexShrink: 0` rows in a
`width: 100%` column). The dialog was the problem: `Sx.View` in a column with **no height bound**
sizes to its content, so the list's viewport became the whole list — `ScrollableHeight == 0`, no
scrollbar, everything below the fold unreachable.

Rules that came out of it:

- A dialog that holds a scrollable list needs `maxHeight` (or an explicit height) so the list has a
  viewport smaller than its content. `maxHeight: 90.Percent()` also keeps the panel on screen.
- Give the dialog `minHeight: 0` and `overflow: Overflow.Scroll` of its own: in a window too short
  for the whole form, the dialog scrolls rather than the form spilling past its border.
- Then the dialog scrollbar overlays its fields — add `paddingRight: 28` (the 6px bar plus margin)
  or it sits on top of the controls.
- A `minHeight` on the compact selector (200) holds a couple of rows; set it too high and the
  dialog scrolls while the list cannot shrink, which is the same bug from the other side.
- The scroll extents ignore up to one `G.Scale` of overflow (`Component.UpdateScrollExtent`). Yoga
  rounds boxes onto the physical pixel grid, so at 1.25x a child stretched to the dialog's content
  box came out 1.25pt wider and the dialog grew a **horizontal** scrollbar strip across its bottom.
  `Overflow.Scroll` has no axis split, so that artifact is the only way to get a horizontal bar on
  a vertical form.

Verify with the harness rather than by eye: `assert-vertical-scroll` on the list name, and the
measured JSON's `ScrollableHeight` for both the list and the dialog. Views:
`lobby-create` (real `LobbyView` + dialog over the shipped stages, 78 of them) and
`stage-select-compact` (the selector in a standalone bounded dialog). Both are in
`NFMWorld/UiHarness/`. `StageSelectViewTests` (MSTest, `NFMWorld.Library.Test`) encodes the two
postconditions.
