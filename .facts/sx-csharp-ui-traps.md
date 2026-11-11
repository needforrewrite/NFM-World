---
name: sx-csharp-ui-traps
description: "Traps found porting the Sx UI from Luau to C# (flow hosts, nullable Styles, SignalsDotnet scheduling)."
metadata:
  node_type: memory
  type: project
  originSessionId: 08df7a44-84a8-46e8-9a56-6bbb0bead9ff
  modified: 2026-10-30T06:58:09.516Z
---

Ported the Luau `Sx` UI to C# in `NFMWorld.UI/Sx/` (SignalsDotnet + existing `NFMWorld.UI` nodes).
Traps that each cost a debugging round:

- **Phases own the UI outright; there is no router.** `BasePhase.BuildUi()` returns the phase's
  whole tree (null for the ImGui editors). `BasePhase.Enter()` calls `UiRenderer.ShowPhase(this)`
  and `Exit()` calls `HidePhase(this)`, so the top phase's tree is what is on screen and it is
  rebuilt from `BuildUi` every time the phase is re-entered. Views take a `record Actions(...)`
  from their phase instead of static callbacks (`MainMenuView.Build(actions)`,
  `GarageView.Build(actions)`, `RaceHudView.Build(actions)`). Don't reintroduce a route table:
  that abstraction existed only because the Luau side couldn't build the nodes itself.
- **A burst of frame work must land in the phase, not the view.** Garage left/right/enter/escape
  are handled in `GaragePhase.KeyPressed`, not on the tree root: `DispatchKeyPressed` only runs a
  node's handler while that node is *focused*, so clicking the search box (or anything focusable)
  would silently stop the garage responding. The phase sees every key.
- **Scroll extent must descend into `Display.Contents` flow hosts.** `Component.UpdateScrollExtent`
  measures visual children, but Yoga zeroes a contents host's box, so anything slotted through
  `Sx.For`/`Show`/`Switch` contributes nothing and the container never scrolls. The settings
  key-bindings tab was the only broken one for exactly that reason: the only tab whose rows live
  in a `Sx.For`.
- **Hand-rolled style merges must use `Over`, never copy field-by-field.** The first `GlassCard`
  and `Button` merges copied every field from the caller's style, so an unset `PaddingTop`
  reset the component's own padding to 0 - the garage cards' "margin where padding should be".
  `Styles.Over` / `TextStyles.Over` only apply fields the overlay actually set.

- **Flow hosts must be `Display.Contents`.** `Show`/`For`/`Switch`/function-child slots each need a
  real node to write into, but a plain `View` is a *row* container (Yoga web defaults), so every
  slotted child stacks sideways. `Sx.FlowHost()` sets `Display.Contents`, which Yoga flattens
  (`CleanupContentsNodesRecursively`). Set it via `Component.MakeFlowHost()`, **not** the `Styles`
  setter — that setter reads `G.Scale` and so needs a graphics backend, which tests do not have.
- **`Styles` fields are nullable on purpose.** `Align.Stretch == 0` and `FlexDirection.Row == 0`,
  so `default` cannot mean "unset"; a hover overlay written against non-nullable defaults silently
  resets a base style's alignment. Nullable + `Styles.Resolved` (web defaults, applied only in
  `OnStylesChanged`) + `Styles.Over(overlay)` is the merge model. `TextStyles` and
  `TextInputStyles` follow the same shape; `Text` resolves on read, so a partially-set style is
  safe to assign.
- **Flow components must dispose the branches they swap out.** `Show`, `Switch`, `For` and the
  function-child slot each keep a per-branch `RenderScope`; if it isn't disposed on swap and on
  `Node.Disposing` (raised by `ComponentChildCollection.Remove/Clear/SetItem`), every toggle
  leaks a live effect that keeps re-running. `SxRendererTests.Show_DisposesTheHiddenBranch` /
  `Switch_ReleasesEveryBranchItLeaves` / `For_ReleasesRowsWhenItsHostLeavesTheTree` fail if this
  regresses.
- **SignalsDotnet effects are scheduled through `TimeProvider`.** `UiScheduler` (a `TimeProvider`
  that only records callbacks) + `Renderer.Flush()` at end of frame coalesces a frame's writes.
  `Effect.DefaultScheduler` must be installed before the first mount.
- **Hit-testing is occlusion-aware** (`FocusManager.HitTestChain`) and skips `PointerEvents=false`
  cancelled nodes. A `Display.Contents` host must not absorb the hit or it swallows presses meant
  for its own children (`FocusManager` has an explicit branch for this).

`styled` came back as `StyledStyle { Base, Hover, Text, HoverText }` + `StyledView`/`StyledText`
delegates; hover is a full second style now that fields are nullable.

See also [[ui-runtime-is-sx-not-preact]].
