---
name: sx-ui-development
description: Design, implement, layout, style, and test NFM-World C# Sx interfaces end-to-end. Use for new screens, editor panels, menus, tabs, dialogs, controls, reactive UI bugs, focus/input routing, Yoga layout, scroll compression, and visual verification with the Sx screenshot harness.
---

# Sx UI development — design to verified pixels

Use this workflow for new UI and substantial UI changes in NFM-World. The deliverable is a usable, reactive, visually inspected interface—not just compiling C#.

## 1. Establish the real system

Read the project's `AGENTS.md` UI/Sx sections, then inspect the current APIs before coding.

| Concern | Current source |
|---|---|
| Element and flow API | `NFMWorld.UI/Sx/Sx.cs` |
| Mounting and effect ownership | `NFMWorld.UI/Sx/Renderer.cs`, `RenderScope.cs`, `Node.cs` |
| Frame scheduling | `NFMWorld.UI/Sx/UiScheduler.cs` |
| Named styles | `NFMWorld.UI/Sx/Styled.cs` |
| Layout/defaults/rendering | `NFMWorld.UI/Component.cs`, `Styles.cs`, `TextStyles.cs` |
| Input and keyboard navigation | `NFMWorld.UI/FocusManager.cs`, `KeyboardNavigation.cs`, `Events/Events.cs` |
| Phase mounting and input forwarding | `nfm-world/UI/UiRenderer.cs`, `nfm-world/Mad/Gameplay/BasePhase.cs`, `GameSparker.cs` |
| Existing controls and palette | `nfm-world/UI/Components/Controls.cs`, `Button.cs`, `Theme.cs` |
| Settings tabs/focus examples | `nfm-world/UI/Components/SettingsView.cs` |
| Editor examples | `nfm-world/UI/Routes/ModelEditorView.cs`, `nfm-world/Mad/UI/StageEditor/StageEditorPhase.Sx.cs` |
| Visual harness | `NFMWorld/WorldGame.SxHarness.cs`, `NFMWorld/UiHarness/*.json` |

Use `index` first, then targeted reads. Check dependency/project files before assuming a library is available. Match local C# style. Use editing tools, not shell scripts, to change source.

This is **C# Sx + SignalsDotnet + Yoga**, not React, JSX, Luau, Preact, or an HTML frontend. Old documentation and generated files may describe removed systems. There is no router, virtual DOM, or component re-render.

## 2. Design the interaction before the tree

Write a short inventory in the task plan, not a new documentation file:

- Who owns the screen? Which phase mounts it, and what happens on back/return/disposal?
- What is the main task? Identify primary action, secondary actions, destructive actions, and cancellation.
- Which state belongs to the document/controller, which is presentation-only, and which is asynchronous?
- Enumerate empty, populated, selected, dirty, loading, error, disabled, hover, focused, and modal states.
- Decide what must fit and what scrolls at small sizes, high UI scale, long labels, and large lists.
- For an editor, explicitly separate viewport input from panels, toolbars, dialogs, and overlays.
- Define measurable acceptance criteria: aligned field edges, popup-to-trigger alignment, minimum control heights, correct selection after switching, no effects after close, no input through overlays.

Use the existing visual language unless asked otherwise: reuse `Theme`, `Controls`, `Button`, card/panel conventions, and Settings-style tabs. Avoid replacing every familiar control with a custom one.

## 3. Assign state and ownership

### Phase/controller versus view

The phase owns document operations, history, scene/camera, persistence, network work, and lifecycle. The view owns its tree and local presentation state. Pass explicit callbacks, commonly a `record Actions(...)`, instead of making the view a second editing engine.

`BasePhase.BuildUi()` supplies the phase's tree. `Enter`/`Exit` route through `UiRenderer.ShowPhase`/`HidePhase`. Build inside `Renderer.Mount` so effects have an owner. Decide deliberately which values survive a rebuilt tree on phase re-entry.

### Every changing read must be reactive

A component method runs **once**. A plain field or captured local does not cause updates. Read signals inside style/text/value/condition accessors:

```csharp
var selected = new Signal<bool>(false);
var node = Sx.Text(text: () => selected.Value ? "Selected" : "Not selected");
```

For an existing plain-field controller, use a revision adapter consistently:

```csharp
private Func<T> Observe<T>(Func<T> read) => () =>
{
    _ = _revision.Value;
    return read();
};
```

Both parts are required: an action publishes the revision, and **every changing slot reads it**. Audit labels, `active`, checkbox values, dropdown indices, `Show` conditions, dirty marks, status text, and styles—not only the obvious text.

Prefer dedicated signals for high-frequency data. A mouse-coordinate signal should refresh the cursor label, not republish the entire hierarchy on every mouse move. Check early-return paths such as dragging and placement.

Do not copy changing data into a local at construction and expect the local to update. `Sx.For` rows are also built once.

### Flow and lifetime

- `Show` keeps one branch; `Switch` chooses a keyed branch; `For` keys by item equality. There is no `Index` helper or `key` prop.
- Construct dialog/branch content **lazily inside its factory**. Use `Dialog(Func<Node[]> content)`, then `content()` inside the visible branch. A prebuilt `Node[]` reused on reopening can contain disposed effects.
- Key an inspector by the selected object, not `selection != null`. A boolean `Show` stays true when A changes to B and keeps fields captured from A. A keyed `For` or appropriate switch remounts for the new identity.
- Returning the same mutable list can suppress change detection. Return a snapshot where membership/order changes must be observed; preserve stable identities for unchanged rows.
- The `For` index is the creation index, not a live position. Resolve current position from identity/state for tabs, reordered rows, and callbacks.
- A reactive child slot must return a **single Node**. Wrap a row array in `Sx.View(children: ...)`. Nested enumerable children are supported, but an array returned directly from a slot is not a Node and can silently fail inside an effect.
- A slot that constructs rows may accidentally subscribe to each row's hover signal during initial style binding. Hover then remounts the collection, destroying the hovered row. Use `Sx.For` keyed by menu/item identity and keep hover effects local to each row.
- Dispose subscriptions, jobs, and scopes on teardown. Reopen/close repeatedly and verify only the live controls react.

## 4. Lay out with Yoga deliberately

### Know the defaults

Yoga uses web defaults. Important defaults are row direction, stretch alignment, border-box sizing, and `flexShrink: 1`. `Styles` fields are nullable for merge semantics: unset is not the same as an explicit zero/default. Merge using `Styles.Over` / `TextStyles.Over`, not field-by-field copies.

Set column direction explicitly for vertical panels. Flow helpers insert `Display.Contents` hosts: they contribute no box. Padding, backgrounds, dimensions, and clipping belong on a real surrounding View, not the flow host.

### Decide which box absorbs pressure

For a vertical panel:

1. Header/search/toolbars/footer actions: `flexShrink: 0`.
2. Scrollable middle: `flexGrow: 1`, `flexShrink: 1`, `minHeight: 0`, `overflow: Overflow.Scroll`.
3. Rows and grouped controls inside that scroller: `flexShrink: 0`.
4. Wrap those rows in a `width: 100%`, `flexShrink: 0` column. `Overflow.Scroll` scrolls both axes, so one wide child or a long empty-state message otherwise draws a horizontal scrollbar on a vertical list. Use `Sx.WrappingText` at `width: 100%` for messages and error lines.

For horizontal layouts, use `minWidth: 0` on the flexible content and preserve fixed controls. A fixed width on the outer panel does **not** prevent its descendants from shrinking vertically. Inspect both the panel's children and nested groups. A long list must scroll, not compress buttons into their padding.

Wrapped parts/toolbars need non-shrinking header rows, row/column gaps, and a constrained remaining list area. A growing inspector needs a non-shrinking content column inside its scroll container; otherwise fields and dropdowns can overlap.

### Alignment and units

- Align related fields with common label and input columns. Put units/help such as `(0 = ground)` in a trailing column, reserving equal space on sibling rows.
- Standard text fields may impose a minimum width. A narrow inspector may need a custom `Sx.TextInput` style with `minWidth: 0`, a deliberate flex basis, and explicit padding/borders.
- Anchor popups to the trigger's **measured** bounds, never guessed offsets based on label length.
- `LayoutBorderPosition`/`LayoutBorderSize` are pixel measurements. Styles are scaled by `G.Scale`; convert pixel coordinates back to style units when placing an absolute popup, and account for the parent's origin.
- Measure after layout. Check resizing and another UI scale; a correct value at scale 1 may double-scale elsewhere.
- Editor viewport bounds come from its measured node, not a second copy of panel-width arithmetic. Use those bounds consistently for scissoring, picking, and gizmos.

If Yoga returns absurd dimensions, first compare the affected layout algorithm with upstream rather than compensating in application styles. Existing shrink-residual and percentage-cache regression tests document prior port bugs.

## 5. Style states, not just the idle screenshot

Use restrained hierarchy: clear panel headings, consistent padding/gaps, subdued secondary hints, prominent primary actions, and visible focus/selection. Avoid styling selected, hovered, and focused states identically when they mean different things.

- Use `Button.Create` and `Controls` where possible. Verify the actual signatures; not every Sx element accepts the same parameters.
- Current reusable named-style API is `Styled.StyleView(...)` / `Styled.StyleText(...)`, not the stale `Sx.Styled` XML example.
- Custom hover belongs in a local signal, read by that node's style/text effects. Do not rebuild a row to change its color.
- Tabs should have stable identity, a visible active treatment (e.g. Settings underline), readable dirty state, keyboard activation, and a close action that does not activate the tab underneath.
- Menus need measured anchoring, individual row hover/focus, outside-click dismissal, Escape, action execution, and correct switching between triggers.
- Give interactive components stable, unique `Name` values for harness targeting. Do not use duplicate display names as identity.

## 6. Make input routing and editing safe

`FocusManager.HitTestChain` is occlusion-aware. A transparent full-screen View still catches input. `PointerEvents=false` skips the **whole subtree**, including children; do not put it on a wrapper containing a working toolbar.

For a click-through editor region, use a `Display.Contents` structural wrapper, a pointer-disabled viewport surface, and separately positioned interactive toolbar/panels. Assert an empty viewport's hit chain is root-only and a toolbar hit still reaches its control.

`MouseEvent.StopPropagation()` stops UI bubbling, **not phase input dispatch**. `GameSparker` dispatches UI input and flushes updates before calling the phase. Closing an overlay can remove it before the phase hit-tests the same press; consume/latch that press to prevent click-through selection or placement.

- Gate viewport input using UI hit tests and modal state, including wheel and release paths.
- Use event button/state rather than polling the final global mouse state for queued press/release events.
- Clear held movement/drag flags on key release, loss of focus, modal entry, and teardown. Do not let Ctrl+S also move the camera backward.
- Do not rely on a root `onkeydown` receiving every key. Focused controls own dispatch; screen-level shortcuts belong in the phase with explicit field/modal guards.
- Custom controls use `KeyboardNavigation.Bind(control, handleKey, focusChanged, preferred)`; `focusChanged` is required. Use `Row`/`Scope` where appropriate. Check keyboard navigation after clicking a text field.
- Numeric fields need a **draft string** separate from the parsed model. Preserve partial `-`, `1.`, and empty text; reject non-finite values. Do not reformat the field on every revision or keystroke. Define commit/blur behavior and undo grouping.
- One user gesture should create one undo operation. Failures/cancel must preserve dirty state, selection, and history as required.

Async work needs cancellation/generation ownership so stale searches or previews cannot overwrite newer selections or touch disposed UI. Capture document snapshots on the game thread, marshal UI changes through the host's established scheduling pattern, and surface errors from fire-and-forget work.

## 7. Test behavior and render real pixels

### Focused automated tests

Use MSTest in `NFMWorld.Library.Test`, not NUnit. Reuse:

- `SxRendererTests`: updates, branch disposal, lazy dialog reopening, keyed row hover lifetime.
- `FocusManagerMousePressTests`: hit chains, focus, transparent/Contents viewport behavior.
- `YogaScrollFlexTests`: long lists and shrink behavior.
- Linked view/controller tests such as `AccountViewTests` when their dependencies fit.

Initialize `IBackend.Backend = new DummyBackend()` before tests that apply layout styles. Use `Renderer.Mount` and flush the scheduler after writes before assertions. Test postconditions and effect disposal, not merely lack of exceptions. Include a negative control or show that the regression fails with the old pattern.

The library test host does not automatically reference game views. Do not assume a framework-pattern test exercises the actual editor. Link lightweight sources deliberately or use the real visual harness.

### Drive and screenshot the actual UI

Run from repository root:

```text
dotnet run --project NFMWorld -- --backend=d3d11 --sx-harness=NFMWorld/UiHarness/stage-editor-menu.json
dotnet run --project NFMWorld -- --backend=d3d11 --sx-harness=NFMWorld/UiHarness/stage-editor-menu.json --sx-scale=1.25
dotnet run --project NFMWorld -- --backend=d3d11 --sx-harness=NFMWorld/UiHarness/stage-editor-inspector.json
```

The harness mounts the actual editor `BuildUi`, uses Apos/fonts and GPU rendering, and sends direct `UiRenderer` events—**never OS mouse/SendKeys automation**. It renders offscreen and writes PNGs plus measured node/hover JSON. Read and inspect the PNGs using `view_image`; creating captures alone is not visual verification.

Script fields: `width`, `height`, `scale`, `output` (relative to the script), optional `view` (`stage-editor`, `model-editor`, `garage`, `stage-select`), and `steps`. `pieceCount` seeds a synthetic stage-editor hierarchy/selection for layout stress; it is not a real rendered track. Use the existing inspector scenario for an 80-row list. The menu views mount the real `GarageView`/`StageSelectView` over synthetic state fixtures.

Supported steps:

| Action | Fields / meaning |
|---|---|
| `click`, `hover` | `target`: unique component Name; or pixel `x`, `y` |
| `text` | `value`: characters sent to the focused input |
| `key` | `value`: actual `Key` enum name; no modifier chord support unless extended |
| `scroll` | target/point plus `delta` |
| `capture` | `capture`: plain filename stem |
| `assert-hover` | target's `IsHovered` must be true |
| `assert-visible` | target's own `IsDisplayed` must be true |
| `assert-aligned` | target and `anchor` left edges within one pixel |
| `assert-input-aligned` | named field containers' direct TextInputs have equal left edges and widths |
| `assert-min-height` | `height`: minimum logical height, scaled by the harness |
| `assert-size` | `width`/`height`: exact logical size, scaled; do not use on `100%` heights, which stay at device pixels |

Any step can include `capture`. Example:

```json
{
  "width": 1280,
  "height": 720,
  "scale": 1,
  "output": "../../.git/sx-captures",
  "steps": [
    { "action": "click", "target": "Editor menu File", "capture": "file-open" },
    { "action": "assert-aligned", "target": "Editor menu popup", "anchor": "Editor menu File" },
    { "action": "hover", "target": "Editor menu item Save", "capture": "save-hover" },
    { "action": "assert-hover", "target": "Editor menu item Save" }
  ]
}
```

Limitations matter: the current harness is UI-only, does not run phase gameplay/input ticks or real scene rendering, and only exposes the registered fixtures. `assert-visible` alone does not prove a node is on-screen, unclipped, or unobstructed; inspect bounds, hit tests, and pixels too. Extend the harness with a narrow real view factory for new screens rather than testing a lookalike. Give interactive components a `name` (`Button.Create(..., name:)`, `Sx.View(name:)`) so scenarios can target them. Keep writes sandboxed and exclude capture artifacts from source control.

Verify at least: empty and dense content; first/last rows; long text; hover transition between siblings; focused input; open/close/reopen dialog; selected-object switching; small viewport and another scale. For a popup compare before/after measured anchors and inspect the actual highlight in the image. If a screenshot cannot decode, verify the capture path itself (PNG requires zlib framing and correct chunk CRCs).

## 8. Completion gate

- [ ] Correct state/action ownership; no duplicate controller logic.
- [ ] Every changing slot reads the right signal/revision; high-frequency updates are narrow.
- [ ] Stable list identity; dialogs/inspectors have correct fresh scopes and disposal.
- [ ] Header/rows/actions do not compress; constrained content scrolls.
- [ ] Anchors/inputs align numerically at multiple scales; no clipped or overlapping controls.
- [ ] Hover, selected, focus, disabled, loading, error and empty states are usable.
- [ ] Mouse, keyboard, modal and viewport input do not leak through each other.
- [ ] Affected project builds and focused tests pass.
- [ ] Actual screenshots were generated **and viewed**; report which scenarios/scales were checked.
- [ ] State clearly what was not verified (scene picking, gameplay, accessibility/device input, etc.).
- [ ] Update relevant project guidance for a significant new subsystem; do not commit unless requested.

Do not end with “please visually test it” when the requested defect can be measured or captured by this harness. Reserve user judgment for genuine interaction feel or art-direction choices, and report limitations honestly.
