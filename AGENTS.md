# Agent instructions — NFM-World

Keep guidance short and actionable. Reference files and patterns below when making changes.

DO NOT write PowerShell or shell scripts for code-editing tasks. ALWAYS use the code-editing tools available to you.

Comments written by AI should be short and succint and not overused. Do not overdescribe things or explain why the code
does what it does unless it could be confusing to a future maintainer, and keep it concise. Don't add comments for
bugfixes unless they're so confusing there's a likelihood of them being reverted.
Don't reference Claude Code/Copilot projects in comments as it's likely other maintainers won't have context for them.

NFM World is a custom game engine and game written in **C#**, targeting `net11.0`. Everything — UI, gamemodes, AI — is
C#; there are no Lua or Luau code paths in the main tree any more. Older comments and docs still mention them — see
[Agent working guidelines](#agent-working-guidelines) for which references are stale and why.

Where possible, put "memories" in .facts and update .facts/MEMORY.md. Do not commit repo or user secrets to the .facts
directory. Remove outdated facts when they become irrelevant.

## Layout

| Path | Role |
|---|---|
| `NFMWorld/` (`NFMWorld.csproj`) | Desktop **exe**. `WorldGame.cs` (main loop, entry point), `WorldGame.Recovery.cs`. Imports `Plugin.Plugins.targets` so plugin output lands beside the binary. |
| `nfm-world/` (`NFMWorld.Game.csproj`) | The game body as a **library**: `Mad/` (gameplay, physics, menu), `UI/` (views, HUD, renderer), `Backends/`, `Content/`, `Renderer.cs`. `OutputType=Library` so the browser and tests can reference it. |
| `NFMWorld.Browser/` | Browser head (`Microsoft.NET.Sdk.WebAssembly`), `browser-wasm`. Mirror of the exe. |
| `NFMWorld.Library/` | Backend/game systems: gamemodes, physics (`Mad/`), stages, HUD data, VFS (`TheVFS`), plugin loader/sandbox. AOT-compiled shared library for the native hosts (except `browser-wasm`). |
| `NFMWorld.Library.Base`, `NFMWorld.Math` | Small shared primitives (logging, arrays, fixed-point helpers). |
| `NFMWorld.UI/` | The UI framework itself: Yoga-backed `Component`/`Node`, `Styles`, Sx (`Sx/`), focus/keyboard/mouse, `NodeDebugger`. |
| `NFMWorld.DriverInterface/` | `IGraphics`, `IGraphics3D`, keys, `TheGraphics`, `DummyBackend`. The platform-neutral surface the game draws against. |
| `NFMWorld.Graphics.Abstraction` | Backend-neutral GPU API (`IGraphicsDevice`, `ICommandBuffer`, pipelines, buffers). |
| `NFMWorld.Graphics.OpenGL` | ANGLE/GLES backend (the non-Windows default). `NFMWorld.Graphics.DesktopGL` — desktop core GL. `NFMWorld.Graphics.D3D11` — the Windows default. `NFMWorld.Graphics.WebGPU` — wgpu-native through Silk.NET (opt-in, `--backend=webgpu`). Each has a `.Smoke` harness. |
| `NFMWorld.Platform.SDL3`, `NFMWorld.Platform.WebGL` | Window/input layer. |
| `NFMWorld.ShaderCompiler*` | HLSL → SPIR-V → cross-compiled bundles, at build time. |
| `NFMWorld.Gamemode.Pvp`, `.TimeTrial`, `.Hostile` | Gamemode **plugins** (see below). |
| `NFMWorld.Ai.Classic`, `NFMWorld.Ai.ElStupido` | AI **plugins**. |
| `NFMWorld.Server.Game`, `.Server.Lobby`, `.Server.SharedMemory` | Dedicated server, `RaceOrchestrator`. |
| `NFMWorld.Archive`, `.Archive.Test`, `.Archive.Validator` | Account/asset server (ASP.NET Core + SQLite) and its out-of-process validator. See [Archive server](#archive-server). |
| `NFMWorld.Multiplayer.Base`, `NFMWorld.Api`, `NFMWorld.Sentry`, `NFMWorld.CrashReporter` | Transport, HTTP API, telemetry, crash reports. |
| `NFMWorld.Multiplayer.ENet` | Fully managed port of the ENet-CSharp fork (host/peer/protocol, UDP via `System.Net.Sockets`). No native `enet.dll`. |
| `Microsoft.UI.Reactor.Yoga` | Yoga layout binding. |
| `NFMWorld.Reactor.ObservableCollections` | `ObservableUnlimitedArray<T>`, `ClientStage` observes these. |
| `CasCore/` (submodule) | The plugin sandbox. |
| `SignalsDotnet/` (submodule) | The Sx signal library. A fork on `uwx/SignalsDotnet` branch `nfmw`, carrying the subscription-leak fix described under [Fine-Grained Reactive UI](#fine-grained-reactive-ui-sx). |
| `Apos.Shapes/` (submodule) | 2D shapes and SVG rendering (Classic HUD, `ShapeSvg`). |
| `HoleyDiver/` | Offline polygon triangulator used by the RAD asset pipeline. |

Key characteristics:
- **Fine-grained reactive UI in C#** (`NFMWorld.Reactor.Sx`, backed by `SignalsDotnet` and Yoga). See
  [Fine-Grained Reactive UI](#fine-grained-reactive-ui-sx).
- **Shader pipeline**: `data/shaders/*.fx` compiled at build to C# bundles (HLSL + GLSL + merged reflection). No
  `.fxb`, no `fxc.exe`, no wine.
- **Fixed-point math** (`FixedMathSharp`) for deterministic physics.
- **Virtual file system** (`Maxine.VFS`, via `TheVFS.VFS`) — every game file read goes through it.
- A Blender-based asset pipeline using the proprietary **RAD 3D** car format.

- **Build / run** (`net11.0`; the shader task needs `dotnet build`, not MSBuild.exe):
  - Solution: `dotnet build nfm-world.slnx -c Debug`
  - Game only: `dotnet build NFMWorld/NFMWorld.csproj`
  - Dev servers: `dotnet build NFMWorld/NFMWorld.csproj -c DebugDevServer` — Debug with the baked
    lobby/archive endpoints pointed at `lobby-dev.nfmw.net` / `dev.nfmw.net` instead of loopback.
  - Run: `dotnet run --project NFMWorld/NFMWorld.csproj`
  - Tests: `dotnet test --no-build` from the solution root, or from a test project folder.
  - Browser: `dotnet build NFMWorld.Browser/NFMWorld.Browser.csproj -p:NfmwBrowser=true` (it sets
    `browser-wasm` itself). The property cannot be set in the csproj: it has to reach referenced
    projects, and only a command-line property does.

- **Compile symbols**: `ANGLE` is defined in the game project and the backends; `WEB_AUDIO` is set only by the browser
  head (which passes `NfmwBrowser=true` through its references). Keep both when touching compilation logic.

- **Don't** remove or flatten the MSBuild platform conditionals, or the `RuntimeIdentifier != 'browser-wasm'` guards
  that keep `PublishAot`/`NativeLib` off in the wasm head.

- **`WorldGame.cs` is shared with the browser head, which links only some of the exe's files.** A
  desktop-only member it calls has to be a `partial` method whose implementation sits in an
  unlinked file (as `RunSxHarness` does in `WorldGame.SxHarness.cs`), because a plain call there
  fails the browser build with `CS0103`. The same applies to anything else the browser head adds to
  its `Compile` group.

---

## UI (Sx + Yoga)

The UI is C#. A phase builds its own tree (`BasePhase.BuildUi()`), and `nfm-world/UI/UiRenderer.cs` mounts whatever
the phase stack puts on top (`ShowPhase`/`HidePhase`). There is no router and no separate frontend build step.

- **Views**: `nfm-world/UI/Routes/` (`MainMenuView`, `MainMenuIntroView`, `GarageView`, `RaceHudView`) and
  `nfm-world/UI/Components/` (`Button`, `GlassCard`, `ClassicHud`, `SettingsView`, `PauseMenuView`).
- **Framework**: `NFMWorld.UI/` — `Component`, `Node`, `Styles`, `TextStyles`, `FocusManager`,
  `KeyboardNavigation`, `Sx/`, plus the base `View`/`Text`/`Image`/`TextInput` in `NFMWorld.UI/Components/`.
- **Yoga config** (`NFMWorld.UI/Component.cs`): `UseWebDefaults = true` plus `FixFlexBasisFitContent`. The defaults
  live in `Styles.Resolved` (`NFMWorld.UI/Styles.cs`): `FlexDirection.Row`, `Align.Stretch`, `Position.Static`,
  `BoxSizing.BorderBox`, `Display.Flex`, `PointerEvents = true`; `flexShrink` comes from Yoga's web default (1).

### Text fields

`TextInput` (single line) and `TextArea` (multiline) share one clipboard. The game wires it once in
`WorldGame.Initialize` (`TextInput.DefaultGetClipboardText`/`DefaultSetClipboardText` → `SdlWindow`), so a field needs
no per-instance wiring; a field-level `GetClipboardText`/`SetClipboardText` still overrides it. SDL's emscripten video
backend implements no clipboard, so on the browser SDL falls back to its own in-process one: copy/paste works inside
the game, not against the browser's system clipboard.

Both controls have Ctrl+A/C/X/V, selection keys and a right-click menu (Cut/Copy/Paste/Select All). The menu is one
shared component, `TextEditContextMenu` (`NFMWorld.UI/Components/`), parameterised by four callbacks and three
capability probes, so the two editors cannot drift; each owner's keyboard shortcuts call the same methods the menu
does.

|Gotcha|Rule|
|---|---|
|Menu is a child of the field|it is an absolutely positioned child so it follows the field, which means a press on it would otherwise reach the field's own handler and re-open it. The menu and its rows call `StopPropagation`, and a secondary press on the field does too or a dialog backdrop that dismisses on press closes the menu as it opens|
|Menu geometry is unscaled|`Styles` lengths are multiplied by `G.Scale` at layout, so `TextEditContextMenu.ShowAt` divides the press position and the visible region by the scale. `Height` must include the border, or the placement clamp lets the menu hang 2px past the edge|
|Menu placement is clamped|to the owner's `ScissorRect`, so a field at the bottom of a dialog opens a menu that is still fully visible|
|Menu is viewport-anchored|`TextArea` is a scroll container, and an absolutely positioned child of one both slides with the content and adds its height to the scroll extent. The menu sets `Component.IgnoresScrollOffset`, which makes paint use the unscrolled origin and drops it from `UpdateScrollExtent`. **The off-screen cull in `PaintChildren` must use that same origin** — testing against the scrolled one culls the popup as soon as the container scrolls, and every layout assertion still passes while nothing paints (`IgnoresScrollOffset_PopupStaysPaintedAfterScrolling` covers it)|
|Menu enabled state|is recomputed every frame while open (`GameTick`), so it tracks typing and selection. A disabled row still swallows the press|
|Right-click keeps the selection|the caret only moves when nothing is selected, so Cut/Copy act on what the user had, and Paste lands at the click point otherwise|

## Fine-Grained Reactive UI (Sx)

SolidJS-style: signals, per-slot effects, **no vnode and no diffing**. Backed by `SignalsDotnet`.

- **Where**: `NFMWorld.UI/Sx/` — `Sx.cs` (component API), `Renderer.cs` (mount), `RenderScope.cs` (ownership),
  `Styled.cs` (named styles), `UiScheduler.cs` (frame batching).
- **`Sx.View(style: () => Sx.Styles(...), children)`** — `style` and `text` parameters are `Func<>` accessors, and a
  child that is a `Func` is a **reactive slot** installed as its own effect. A component method runs **once**.
- **Flow**: `Sx.Show(when, then, otherwise)` (one branch exists), `Sx.For(each, row)` (keyed by value identity, no
  `key` prop), `Sx.Switch(signal, branches, fallback)`. There is **no `Index`** — `For` is the only list helper. All of
  them mount their content under a fresh `RenderScope` and use a **flow host** (`Display.Contents`, dropped from the
  layout pass) as their anchor.
- **Styles**: `Sx.Styles(...)` builds a `Styles`; every field is nullable and `Styles.Over` leaves unset fields alone.
  Named/reusable styles are `Styled.StyleView(baseStyle: ..., hoverStyle: ...)` (`NFMWorld.UI/Sx/Styled.cs`), which
  wires hover as a local signal so hovering restyles that node only — see `MainMenuView.ItemRow`. (The XML doc in
  `Styled.cs` still shows an older `Sx.Styled(Sx.View, ...)` call shape; the real API is `Styled.StyleView`.)
- **Scheduler**: `UiScheduler.Instance` (installed on first `UiRenderer` construction) replaces
  `Effect.DefaultScheduler`, so every write made during a frame coalesces into **one effect pass** at end of frame.
  Nothing in the UI fires timers itself.
- **SignalsDotnet is vendored** — `SignalsDotnet/` (submodule, `uwx/SignalsDotnet` branch `nfmw`) rather than the
  NuGet package. Upstream 3.4.1's `SignalsRequestedObservable.SubscribeCore` discarded the disposable returned by
  `subject.Subscribe(...)`, so `SignalsRequestedDisposable` only ever cleared the ambient scope and every compute
  that reused a scope left an observer on its subject **forever**. After ~30 minutes of HUD updates one subject held
  over a million observers: `Signal.Value` fanned out to all of them and the game fell to 2 fps with the cost
  aggregating over time (not a per-frame cost that was always there). Rebase the one-commit fix on any upstream bump
  and re-check that `_currentScope`'s subscription is still disposed with the compute.

### Sx gotchas

| Gotcha | Rule |
|---|---|
| Reactive slots | A `Func<T>` prop or `Func<object?>` child is an effect. Pass `() => value`, never a snapshot — a plain value is read once at mount and never updates. |
| Components run once | There is no re-render. Anything that must change feedbacks through a signal read inside a slot. |
| Flow hosts | `Show`/`For`/`Switch`/slots insert into a `Display.Contents` host, which Yoga flattens out of layout. Don't give a flow host padding/background and expect it to apply. |
| `For` keys by value | Rows are keyed by item equality and built **once**; moving them only reorders nodes. So a row remounts whenever `each` yields a new item value (a freshly built record or string does), and the `index` passed to the row is the one it was created with — read position from a signal inside the row, not from that argument. |
| Scopes own effects | Content mounted by a flow helper lives in its own `RenderScope`; disposing that scope tears down everything created inside it. Reusing a node across mounts without adopting a new scope leaves its effects dead. |
| A bind's first run is untracked | `RenderScope.Bind` computes the initial value inside `Signal.UntrackedScope()` and only then installs the effect, and `Show`/`For`/`Switch` mount their first branch the same way. Running it eagerly under the caller's computation subscribed that enclosing effect to every signal the child read, so one child's hover re-ran the parent slot and rebuilt its whole subtree — which is what made the create dialog reload its gamemode plugin on hover. |
| Style structs are nullable | `Styles` fields are `T?` on purpose (merge semantics). Setting a field to its CLR default is not the same as leaving it null. |
| Rows in a scroll container | Yoga's web default `flexShrink: 1` squeezes list rows to their padding, so a long list shows crushed rows (or one solid block) instead of scrolling. List rows need `flexShrink: 0` — the main-menu items and `LobbyView.Row` set it. |
| Scroll needs a bounded viewport | A `flexGrow: 1` scroll container inside a dialog with no height bound never scrolls: the auto-height panel grows to fit its content, so the list's viewport is the whole list. Give the dialog `maxHeight` + `minHeight: 0` and let it scroll itself (`Overflow.Scroll`) for windows too short for the form, and keep the list's own `minHeight` small enough that the two do not fight. The lobby create dialog's stage list failed exactly this way |
| Scroll is both axes | `Overflow.Scroll` has no X/Y split in `Styles`, so any direct child wider than the viewport adds a horizontal scrollbar. Put rows and messages in a `width: 100%`, `flexShrink: 0` column inside the scroller; long messages use `Sx.WrappingText` at 100% width. |
| Off-screen paint | `Component.PaintChildren` skips a child whose border box lies entirely outside the inherited clip, so a scrolled list only pays for its visible rows. Rows still lay out; only painting is skipped. Flow hosts (`Display.Contents`) are never culled by their own box — they have none. The test uses the same origin the paint will, so a `IgnoresScrollOffset` popup is not culled by the container's scroll offset. |
| Popups inside a scroll container | an absolutely positioned child of an `Overflow.Scroll` node is offset by the scroll at paint time and counts towards the scroll extent, so a context menu or dropdown anchored in a scrolling list slides away and adds its own height as scrollable space. Set `Component.IgnoresScrollOffset` on the popup: it then paints at the unscrolled origin and is dropped from `UpdateScrollExtent`. It is still clipped by the container. |
| Hidden nodes have no z | `Component.EscapeZ` ignores a node whose `IsDisplayed` is false, so a closed popup (`display: none`, z 1000) neither presents its own z nor hoists a descendant's. Counting them tied every open popup to the same z as the later of two closed ones, and the later-sibling tie-break handed the hover/press to the wrong column — the create dialog's gamemode dropdown lost every hit to the stage list's search field, whose closed `TextEditContextMenu` sits at z 1000. `ZIndexHitTestTests.HitTest_HiddenZIndexedChild_DoesNotHoistItsSibling` covers it |
| Scrollbars overlay content | `RenderScrollbars` draws inside the padding box's right/bottom 6px, over the content. A scroll container holding buttons or fields needs `paddingRight: 8` (and `paddingBottom` for the horizontal bar) or the bar sits on top of them — the stage editor's hierarchy, parts grid, inspector, property panels and dialogs all set it. |
| Mouse pixels vs styled lengths | `MouseEvent.Position` is in window pixels; `Styles` lengths are multiplied by `G.Scale` at layout. A node placed from a mouse position (menu overlay, rect-selection box, hierarchy context menu) must divide by `G.Scale`, or it lands at `position × scale` and can paint over neighbouring panels. |

### Yoga port

`Microsoft.UI.Reactor.Yoga` is a hand port of [microsoft-ui-reactor's C# Yoga](https://github.com/microsoft/microsoft-ui-reactor/tree/main/src/Reactor/Yoga),
and it lags upstream. Layout bugs that read as "impossible" numbers (a row hundreds of millions of
pixels tall, text laid out at `y = -3` inside its own row) are usually a missing upstream fix — diff
the affected function against upstream before debugging the tree. Two that were ported:

| Missing fix | Symptom |
|---|---|
| facebook/yoga#2038 (`DistributeFreeSpaceSecondPass`) | Dividing by a **small positive** shrink-total residual instead of skipping it gave `~1e8`-pixel child sizes; a scroll container's rows all landed off-screen and the list painted as one flat block. Ours tested `== 0`; upstream tests `>= 0`. `YogaScrollFlexTests` covers it. |
| facebook/yoga#2042 (`CalculateLayoutInternal`) | A cached measurement was reused across different owner sizes, so a subtree with percentage sizes got another parent's result. `CachedMeasurement` now records `OwnerWidth`/`OwnerHeight` and `YogaStyle.ResolvesAgainstOwnerWidth/Height` gates the hit. |

---

## Gamemodes (plugins only)

- **Destructible stage pieces** — `BackendStage.Destruction.cs` keeps damage by original piece index;
  only `set` pieces whose model explicitly declares positive `maxmag` opt in (`CarStats.HasMaxmag`,
  preserved by radpack serialization). `CarPhysics` reports the strongest contact per piece and car
  with a short cooldown. Online owners send reliable `C2S_StageImpact`; the GM checks ownership,
  proximity and bounds, accumulates damage on its tick thread and broadcasts `S2C_StagePieceState`.
  Attach/reconnect snapshots include partial damage and destroyed pieces without replaying debris.
  Broken pieces leave `Pieces` and their quadtree shapes are filtered out. Local restart restores them.
  `ClientStageRenderer.Destruction.cs` owns bounded, short-lived polygon debris (`StageDebris`) driven
  by the breaking car's velocity. Destructible models bypass coplanar surface removal so breaking one
  cannot expose a hole in its neighbours. Older radpacks need repacking to record the explicit opt-in.
  Surviving hits carry cumulative deformation in `StagePieceState`; `StageObjectDamage` rebuilds
  deterministic dents from pristine polygons, preserving their colours. Each damaged visual owns its mesh.
  Live surviving hits play spatial `lowcrash1-3`; breaks play `obj1-4`. Snapshots restore geometry
  silently, and local restart clears dents and owned audio/meshes.

Gamemodes are compiled `.NET` plugins — the Lua path is gone. Singleplayer and multiplayer share one code path.

### Gamemode facilities in the host

- **Every gamemode has a server side.** Completion is tracked server-side, so `GamemodeFactory` always
  constructs one; a client-only plugin cannot run. `NFMWorld.Gamemodes.StandardRace` is the shared
  completion kit: `TickCountdown` (works at both the client's and the GM's tick rates), `IsPlausibleCheckpoint`
  (+1 checkpoint exactly, corroborated against the relayed position), `BuildStandings` (winner, then running
  cars by position, then DNFs newest-death first) and `RaceProgress.TrackWasted`. PvP's server gamemode and the
  Rocket one both sit on these; keep new gamemodes on them rather than re-deriving the rules.
- **Generic car inputs** — `Control.GamemodeActions` holds ten held booleans (`GamemodeAction(0..9)` /
  `SetGamemodeAction`), written each tick by `RaceInputController` from the "Gamemode Action 1..10" bindings
  and relayed through `CarFrame.TheBitFlags` bits 17–26 (indexed `TheBitFlags[i]`), so remote cars press what
  their owner pressed. `Control.Encode`/`Decode` still carry only the five vanilla inputs; gamemode actions do
  not ride them.
- **Generic physics facilities** (`CarPhysics`) — `Grounded` (contact as resolved at the end of the last step),
  `AddImpulse(x,y,z)` (world-space, Y positive downward), `AddLocalImpulse(forward,right,up)` (car frame; model +Z
  nose, +Y down, +X right, rotated by the same `UMath.Rot` sequence `Drive` uses for the wheels), `AddForwardThrust`
  (along the nose, pitch included), `ForwardSpeed` (velocity projected onto the nose, signed), `TurnYaw` (air
  heading, positive right), `PreStep`/`PostStep` events. A gamemode arms impulses from its tick or from `PreStep`
  and they are consumed by that same step. Entering the stunt/air-control path is the gamemode's job: set
  `Mtouch = false; StuntState = 1` (there is no `BeginStunt`; `Drive` zeroes the state while `Mtouch` is set). No
  jump/boost semantics live in the engine; `NFMWorld.Gamemode.Rocket` is the reference (double jump, directional
  airborne jump, Q/E heading control, boost as a top speed, per-car state in an `UnlimitedArray` indexed by
  `ClientSidePlayer.Index`).
- **HUD widgets** — a gamemode overrides `GetHudWidgets()` (client) with data records
  (`GamemodeHudWidget(Anchor, Label, Func<float>)`; anchors in `GamemodeHudAnchor`). The widget is data and the
  host builds the node (`GamemodeHudWidgets`), which is what keeps a sandboxed plugin's custom UI inside Sx.
  Every skin passes through `HudSkinRegistry.BuildWithGamemodeWidgets`, so declared widgets reach classic,
  world and JSON skins. Widgets are re-read against `PresentationRevision`, so a declaration after mount still shows.
- **Standard-completion UI is opt-in** — `BaseClientGamemode.UsesStandardRaceCompletion` (true for PvP, Time Trial,
  Rocket) replaces the old hardcoded gamemode-id checks in the race phase; an unscored mode returns false and gets
  no finish banner or death screen.

### Car coordinate conventions

Read this before touching anything that pushes, steers or orients a car. Every rule here was
established by measurement, not by reading the trig — the formulas read plausibly either way,
and getting one wrong ships a control that works backwards. **Assert against
`CarPhysics.NoseDirection` in a test rather than reasoning about signs**; the existing
coverage is `CarPhysicsArcadeHookTests` and `RocketArcadeBehaviourTests`.

|Rule|Consequence|
|---|---|
|**Y is positive downward**|"Up" is a negative Y impulse; a rising car's position value *decreases*. `World.Ground` is a floor constant.|
|**At yaw 0 the nose points +Z**|Throttle drives towards +Z.|
|**Screen-right is +X at yaw 0**|`FollowCamera` sits behind the car looking +Z, so its right is world +X. Check every left/right question against this.|
|**Yaw (`Rotation.Xz`) increases counter-clockwise**|Adding to yaw swings the nose from +Z towards **-X**, which reads as turning **left**. `control.Right` decreases `WheelXz`/`Xz`, confirming it.|
|**Model space is +Z nose, +Y down, +X car-right**|Only `up` gets negated when mapping to world. Negating `right` too inverts every sideways kick.|

- **Derive axes with `ToWorldAxis`, never hand-written trig.** It applies the exact `UMath.Rot`
  sequence `Drive` uses for the wheel contact points (xy by `Pxy`, yz by `Pzy`, then xz by yaw).
  A trig formula written from scratch disagrees with the rendered car at pitch/roll extremes and
  on flips — that is what produced three inverted-control bugs in a row.
- **`TurnYaw(degrees)` is screen-relative**: positive turns the player's right, and it negates
  internally because yaw increases the other way. Never add to `Xz` directly.
- **`NoseDirection` is not `Mxz`.** `Mxz` comes from wheel velocity, so after a spin or flip it
  describes where the car *slides*, not where it *faces*. Boost, steering assists and the
  `ForwardSpeed` cap all use `NoseDirection`.
- **A jump starts a stunt only if the gamemode says so**: `Drive` zeroes `StuntState` on every
  `Mtouch` tick, so set **both** `Mtouch = false` and `StuntState = 1`. There is no engine helper.
  `StuntState` is 0 grounded, 1 take-off (promoted to 2 next tick), 2 airborne, -1 wall-riding.

### Architecture

- **One race phase** — `nfm-world/Mad/Gameplay/RacePhase.cs`. It composes `RaceInputController` and
  `RaceCameraDirector` and talks to an `IRaceHost` (`NFMWorld.Library/Gamemodes/RaceHost/`):
  - `LocalRaceHost` — singleplayer: runs the factory's `IServerGamemode` in-process, so SP exercises the same
    client/server split as online play.
  - `NetworkRaceHost` (`nfm-world/Mad/Gameplay/RaceHost/`) — multiplayer: bridges an `IMultiplayerClientTransport`
    (join token, C2S/S2C packets) to the same interface.
- **Contracts** — `IGamemode` and `IServerGamemode` in `NFMWorld.Library/Gamemodes/`. Base classes:
  `BaseClientGamemode` / `BaseServerGamemode`.
- **Players are the source of truth** — gamemodes own `ObservableUnlimitedArray<ClientSidePlayer>` (`Players`, each
  with a `BackendCar? Car`). `ClientStage.SetPlayers(...)` observes the collection and each player's `CarChanged` and
  keeps one `CarVisual` per car. Race phases do not keep a `CarsInRace` array; `BaseStageRenderingPhase` phases
  (garage, menu) still use `ClientStage.SetCars`.

### Car selection and Time Trial

- Main-menu GARAGE commits with SELECT CAR/Enter; Back cancels. `MenuHistory.Car` is the shared car for
  free play, time trials and lobby identity, persisted through VFS in `data/cfg/menu-history.json`.
  Entering a race remembers only its stage; test drives and dev commands must not overwrite the garage choice.
  The ONLINE list shows each public car's publisher (`CarStatsViewModel.Author`, from the archive
  search result) instead of the collection name; it is remembered while that car is previewed.
- The garage is three pages (`UiState.Garage.Page`): home menu (`CHANGE CAR` / `CUSTOMIZE CAR` / `MANAGE INVENTORY`
  disabled / `BACK`), the car browser and the paint editor. The UI is two floating panels over the live stage, not a
  full-height column: the menu is anchored at `PanelInset` and sizes to the home rows (height-capped only on the list
  pages, which is what lets those lists scroll), and the stats card is anchored to the **opposite** edge so the car is
  framed in the gap between them. The browser is a two-column card grid (`CardWidth` is (menu content − gap) / 2; keep
  them in step with `MenuWidth` or the list gains a horizontal scrollbar) whose tabs are
  `UiState.Garage.Categories` — every installed collection with a drivable car, then `Online`. Local and archive share
  one picker: the archive tab is just the last tab, and its cards appear as the search returns. Each card shows a
  baked image of the model (`CarThumbnails`, one or two renders a frame into an offscreen target from a front-right
  three-quarter view); the paint invalidates that car's image. `CarThumbnails.RenderPending` must be called from the
  garage phase's `Render` (both `GaragePhase` and the inline garage in `MainMenuPhase`).
- **Broken models are not cars.** `BackendCar`'s constructor builds a four-wheel contact set from `Wheels[0..3]`, so an
  unfinished model-editor export with fewer wheels throws. `BackendCar.IsDrivable` gates the picker list, the category
  tabs and `SetupCurrentCar`, and `MainMenuPhase.SetupMenuCar` falls back when the remembered car is not drivable.
- The car is the focus: `MenuCameraDirector` fits the garage distance to `BackendCar.MaxRadius`
  (`clamp(3.2 × radius, 420, 2400)`) and offsets it by `GarageFraming` — a fraction of the viewport half-width, so the
  car keeps the same screen position whatever its size. `GarageCameraControls` asks `GarageView.OverUi(x, y)`, which
  tests the panels' **measured rectangles**; don't reintroduce a hardcoded column width there. Keys are routed in
  `GarageController` (a tree root handler only runs while the root is focused): arrows walk the grid (up/down by
  `GridColumns`), Enter commits, Esc backs out or clears the search. Held arrows arrive as repeats, which
  `BasePhase.KeyRepeated` forwards; a focused text field owns the keys, anything else does not.
- Paint: cars declare up to four colours (`1stColor(..)`…`4thColor(..)`), the parser tags polys with `ColNum`, and
  `CarPaint` (`NFMWorld.Library/Util/`) rewrites the tagged polys on the shared `Rad3d`. `BackendGameSparker.GetCar`
  applies the saved overrides, which is what carries the chosen paint into races; the original colours are captured
  per instance before the first rewrite, so changing or resetting an override restores them. The preview's
  `CarVisual` copied its polys at construction, so a live paint change copies the colours into that mesh and rebuilds
  it. Overrides persist in `data/cfg/car-colors.json`.
- The lobby offers PvP, Time Trial and Rocket League through `GamemodeCatalog.Ids`; config controls follow the
  selected mode.
  Time Trial has no bots. Real remote racers remain roster players, not `IsFake` replay ghosts; they are drawn
  translucent and driven without inter-car collisions. Saved best-run ghosts remain singleplayer-only.
- `IGamemodeContext.IsMultiplayer` distinguishes online sessions, including solo online races, from local play.
  Never infer this from player count. Online Time Trial restart is disabled; the server waits for every racer
  to finish or forfeit/disconnect before completing the session.

### Plugin layout & lifecycle

- **Layout** — `data/gamemodes/{id}/gamemode.dll`, `data/ais/{id}/ai.dll`. The file name is identical in every
  plugin of a kind; the folder disambiguates.
- **Authoring** — derive from `PluginGamemode` / `PluginServerGamemode` (gamemodes) or `PluginAi` (AI), and mark the
  assembly with `[assembly: Gamemode(Name = ..., Description = ...)]` or `[assembly: Ai(...)]`, plus
  `GamemodeConfigProperty` / `GamemodeConfigPropertyOption`.
- **Config in the UI** — the lobby's create page renders whatever the gamemode declares: one dropdown per property
  with options, labelled from a `GamemodeConfigProperty` and its `GamemodeConfigPropertyOption`s. Nothing in the client
  hardcodes a gamemode's options. The schema is read **once, by `LobbyPhase.OnSetGamemode`**, through
  `GamemodeCatalog.ConfigFields(id)` (a `GamemodeConfigProperty` projection holding plain records), which loads the
  plugin, copies what it declares and releases it. It is then published as `UiState.Lobby.GamemodeFields`, and the view
  renders only that. A view slot must never read the plugin itself: a slot re-runs for reasons the schema does not
  depend on (a child's hover, a sibling's value), and each run would load and unload the whole assembly.
- **Bots in a session** — the creator's client simulates every bot it fielded, and only that client
  (`(player.Info.IsBot && player.Info.OwnerId == ClientPlayer.Info.Id)`). Bots reserve real lobby slots with fresh ids
  (`SessionBots.Place`, filled at creation within `MaxPlayers`, including the creator), get no join token,
  and are excluded from the GM's `PlayerCount` — a bot never sends `C2S_RaceLoaded`. Their state is relayed by their
  owner under the *bot's* id (`C2S_PlayerState.PlayerId`), and the GM accepts it only when
  `PlayerInfos[id].IsBot && OwnerId == sender`. A receiving client must not apply relayed state to a car it simulates.
- **Build** — `Plugin.props` is imported by each plugin project; `Plugin.Plugins.targets` is imported by each *host*
  (`NFMWorld`, `nfm-world`, `NFMWorld.Server.Game`, `NFMWorld.Library.Test`) and builds the plugins, passing its own
  output dir down so each plugin copies only its own output. `PluginOutputRoot` is the only property handed down: it
  is global, so anything else (an id, a folder) would also reach every project the plugin references and give each
  plugin its own build of the shared libraries. Restore is skipped when the plugin already has
  `obj/project.assets.json`, so a host rebuild costs a no-op Build per plugin.
- **Resolution** — `GamemodeFactory.Create(id, config)` in `NFMWorld.Library/Mad/Multiplayer/GameModes.cs` loads the
  plugin fresh (no cache) and returns a `NativeGamemodeFactory`. There is **no fallback**: a missing plugin throws.
  `BaseGamemodeFactory.Config` lets a caller build a second factory for the same run.
- **Loader** — `NFMWorld.Library/Gamemodes/DotNet/GamemodePlugin.cs` reads the assembly into memory, loads it into its
  own **collectible** `AssemblyLoadContext` (`PluginLoadContext`), then reflects the gamemode types once.
- **Unloading** — `BaseGamemodeFactory.End()` (overridden by `NativeGamemodeFactory` to call `GamemodePlugin.Unload`)
  releases the context. `RacePhase.Dispose` does it for the client, `LocalRaceHost.Dispose` for the singleplayer
  server side, and `RaceOrchestrator.TickServerGamemodes` when a server-side race finishes.
- **Unload verification** — `PluginUnloadVerifier` (DEBUG) asserts the release actually took, since a silent failure
  is indistinguishable from success. `End()` arms it with a `WeakReference`; `WorldGame.Update` and
  `RaceOrchestrator.TickServerGamemodes` call `Verify()` **after** that frame's teardown, and it judges only plugins
  armed on an earlier frame.
- **One factory per run** — a factory owns what it loaded and releases it in `End()`. Never share one between two
  `RacePhase`s: the replaced phase's `Dispose` is deferred to end-of-frame, so it runs after the replacement is live
  and would unload the plugin out from under it. The `map` and `car` dev commands are the cases that matter; they
  build a fresh factory instead.
- **A running game keeps the plugin it loaded: edit → rebuild → relaunch.** The host copies the built plugin into
  `bin/.../data/gamemodes/{id}/gamemode.dll`, but an already-running process holds its in-memory copy, and
  `NFMWorld.Library.Test/bin` is built separately. So a test can pass against new source while the game still runs
  old source — which once cost a full round of false debugging on a `StuntState` predicate that was correct all
  along. When behaviour contradicts the source, compare the deployed DLL's timestamp with the `.cs`, or
  `strings <dll> | grep <new symbol>`, before touching the logic.
### Gotchas

| Gotcha | Rule |
|---|---|
| Assembly identity | Ship the file as `gamemode.dll` / `ai.dll` by **renaming at copy time** (`Plugin.props`), never via `TargetName`/`AssemblyName` — those rename the assembly *identity* too, and the runtime refuses to load two assemblies under one identity |
| Plugin dependencies | Reference shared assemblies (`NFMWorld.Library`, `TightPack`, …) with `Private="false" ExcludeAssets="runtime"` so the plugin resolves against the host's copies instead of shadowing them |
| `DisableTransitiveProjectReferences` | every plugin csproj sets it, so a type used from a transitive dependency needs its own explicit reference |
| Target resolution timing | read `$(TargetDir)` inside the target, not at evaluation time — the SDK sets it after the project body |
| Unload is silent | `AssemblyLoadContext.Unload()` on a context something still references does nothing and reports nothing. Any surviving reference — a gamemode instance, a `CarVisual`, a reflected `ConstructorInfo` — keeps the assembly loaded, so teardown drops them *in order* |
| TightPack pins the context | a plugin's generated code registers its formatters into TightPack's **static** table from each type's static constructor, and they hold the plugin's types for the process lifetime. `TightPackFormatterRegistry.DropFormatters` (→ `TightPackFormatterProvider.UnregisterAssembly`) removes them in `Unload`; without it the plugin never unloads, with no error to show. Lookups are also memoised per closed type in a generic static field, which is safe: an instantiation over a collectible load context's types is allocated in that context's loader allocator and dies with it. A `Type`-keyed cache would pin, which is what `GamemodePluginUnloadTests.End_ReleasesTheAssembly_AfterItsTypesWereSerialized` guards |
| Teardown ordering | release the objects that reference the plugin *before* the factory's `End()`. `RacePhase.Dispose` does them in order: `_host.Dispose()`, `CurrentStage.SetPlayers(null)` (drops the `CarVisual`s), `GamemodeInstance?.End()`, `GamemodeInstance = null`, and only then `Gamemode.End()` — the factory release has to be last. `LocalRaceHost.Dispose` ends its server gamemode and then nulls the field for the same reason |
| Verifying an unload | never inside `Unload`/`End()`: the frames above are live roots, so a forced collection always finds the assembly alive. `PluginUnloadVerifier` does it from the host loop. Tests assert it through a `WeakReference` to the plugin's **assembly** after `GC.Collect` (`GamemodePluginUnloadTests`), never by trusting `Unload`'s return — there isn't one |

### Plugin sandbox (CasCore)

Every plugin assembly loads through `CasAssemblyLoader` (`PluginAssembly.Load`), so a plugin only reaches what the
policy admits. `PluginPolicy` builds it: CasCore's default sandbox plus whatever the host marked `[PluginVisible]` /
`[VendorPluginVisible]`. `Unsafe` is denied outright, and TightPack's reader/writer primitives are named one at a
time — they carry their lengths, `Unsafe` does not.

| Gotcha | Rule |
|---|---|
| CasCore is a fork | `CasCore/` is a submodule on `uwx/CasCore` branch `nfmw`, carrying three fixes upstream 0.3.0 needs on this runtime: `base.Foo()` calls went through the static guard and were always refused, the JSON bindings name members .NET 11 no longer has, and array intrinsic members were impossible to authorise. Rebase all three on any upstream bump |
| Policy entries by token | `CasPolicy` identifies a member by **module handle + metadata token**, not by `MemberInfo` instance. Denying through a type-forwarder (`Unsafe` forwards to `System.Private.CoreLib`) removes unrelated members that share the arithmetic — filter to `DeclaringType == typeof(...)` |
| Array members have no token | Every array type's `.ctor`/`Get`/`Set`/`Address` reports `MetadataToken 0x06000000` in `System.Private.CoreLib`, so `MemberId` cannot tell them apart (or apart from each other). `CasPolicy.CanAccess(MethodBase)` branches on `DeclaringType.IsArray` before the token lookup and answers from `CasPolicyBuilder.AllowArrayMembers()`, which `WithSandboxedSystem()` sets. A policy built without it denies `new T[i, j]` and `arr[i, j]` while `new T[i]`/`arr[i]` still work — those compile to `newarr`/`ldelem`, not a member call |
| `T[]` in a binding signature | `typeof(T[])` cannot compile in `WithSandboxedSystem`, which is why the array constructor and `op_Implicit` lines sat commented out for so long. The parameter type is the open generic's own parameter: `typeof(ReadOnlySpan<>).GetGenericArguments()[0].MakeArrayType()`. `ReadOnlySpan<T>.op_Implicit(T[])` is enabled; `Span<T>`'s mutable equivalent is not |
| Accessibility is inclusive | `Private` ⊇ `Protected` ⊇ `Public`. `IncludingAllMembersOfType` binds public members plus public and protected constructors; any other protected member a plugin calls needs its own `[PluginVisible]` (`[method: PluginVisible]` on a ctor) |
| Generated TightPack code | it is ordinary plugin IL, so it only reaches the policy. TightPack's own runtime does the unsafe work internally, so `Unsafe` can stay denied: the generator emits direct calls to `TightPackWriter`/`TightPackReader`, whose `GetSpan` bounds-checks every access (`PluginPolicy.AllowTightPack`) |
| Visibility levels | `Visible.Self` on a type exposes the type and members carrying their own `[PluginVisible]` — never private members. `IncludingAllMembersOfType` exposes the type's **public** members without a marker each; use it for a base class plugins derive from |
| `[UnsafeAccessor]` in probes | a probe reading a private host field this way compiles to a direct `ldfld`, which the policy must refuse; it fails as `InvalidProgramException` from CasCore's IL verifier rather than `SecurityException`, so assert "throws", not the type |
| Proving the sandbox | `NFMWorld.Gamemode.Hostile` has one probe per forbidden reach; `PluginSandboxTests` loads it like the game does and asserts every probe is refused. `AiPluginTests.CreateClient_PassesTheSandbox` constructs each real plugin's client gamemode, which no server-only path does. The test host declares its own plugin list, so probes never reach release output |
| Not everywhere | `PluginPolicy.Supported` is false without dynamic code (the wasm head), where loads fall back to the plain `PluginLoadContext` |
| `InlineArray4` element access | indexing one compiles to a BCL `InlineArrayFirstElementRef` helper that the IL verifier rejects (`BadImageFormatException`), so a plugin cannot read `CarPhysics.Scx/Scy/Scz` directly — add a summed accessor on the host instead (`CarPhysics.SummedWheelVelocity`) |

---

## Bot AI (`NFMWorld.Ai.*`)

A bot is `PluginAi`, attached by `BaseClientGamemode.AttachBot(id, player)` from `data/ais/<id>/ai.dll` and driven
from `PhysicsController.GameTick`. Two ship:

- **`classic`** (`NFMWorld.Ai.Classic`) — a port of the original game's driver (ezBeanie/need-for-madness
  `src/race/ai.js`), minus everything keyed on a campaign stage number or car type. PvP attaches it, so free play
  races against it. Because there is no campaign stage to tune against, its rubber-banding is **not** ported: no
  gas-off hold-back, no multi-second aim lag, no corner-cutting skip. It aims every tick, brakes for the corner
  ahead, and is clamped to the gate it still owes.
- **`el_stupido`** (`NFMWorld.Ai.ElStupido`) — a minimal node follower, used by the main-menu attract drive.

| Gotcha | Rule |
|---|---|
| Path point tracking | the physics-side path walk in `CarPhysics.cs` is commented out (`/* … }*/ // CHK16`), so `CarPhysics.Point` is never assigned. `ClassicAi` tracks its own index over `TrackPath` (built from `BackendStage.Nodes` minus the fix hoops) with a **forward-only** window. A global nearest query jumps across the track wherever the path crosses itself; letting the window step back picks a road node while the car is beside a gate that follows it in the list. `TrackPath` also trims trailing points after the final gate that are far from the first point: detached practice/fix lanes tagged with plain `)p`, such as Grapefruit's x=10000 lane, would otherwise become the next target after the last loop node. The trim stops at the last checkpoint, so a point-to-point stage like Ancient/LongRun (one gate at the far end) keeps its whole path. A `)po` block is kept instead: `_fixBlockEnd` marks it, `AdvanceNormal` jumps over it for racing, and repair routing uses `Advance` to follow it. Older nfm2 tracks (Maximum Overfly) have a single `)po` with no closing marker; when it starts after the final checkpoint, the block runs to the end of the node list |
| Standings | `PvpGamemode` (client) calls `CheckPointHelper.CalculatePositions` once per tick, after the checkpoint crossings, so `Placement` drives the HUD's position column. It runs only when there is more than one player. `ClassicAi.RankCars` recomputes the same permutation for its own use and is deliberately separate, so a bot never owns the race's order |
| Tickrate | both AI plugins run at the client's 63 tps. `ClassicAi` gates only its bookkeeping counters on `Physics.OriginalTicksPerNewTick` (the original's `tick.fraction >= tick.scale`); `ElStupido` does not gate at all. Anything that must line up with the original's 21.4 Hz has to go through that gate, not a bare tick count |
| Shortcuts | by default `ChooseGoalPoint` targets `PathIndex + 1`, so the driver visits every `)p` node. `TryShortcut` may skip 1–2 non-checkpoint nodes when the skipped nodes stay within 1200 units of the straight car→candidate line (roughly a road width), the line saves at least 500 units, the heading change is at most 90°, and the car is under 50% damage. It never skips a gate or a `FixRoadStart`. More aggressive route choice would need road/wall queries; this corridor rule is the safe version |
| Gates | the next gate is `CurrentCheckpoint` into `TrackPath.CheckpointPath`. The driven point is clamped so it cannot pass an uncleared gate. A miss is the tracked point landing **just past** the gate in route order (`ForwardDistance(gate, index)` in `(0, small]`), not "more than half a lap to the gate" — that is true of normal layouts like stage 1 after its first gate, and sends the driver across the map. `DriverState.Missed` latches until the gate clears. Recovery is a normal-speed turn back to the gate; do not add a special slow-down, or the car ends up crawling and then reversing. Ramps within two path points of a gate are not taken, since jumping the box causes the same miss. The turn-in lookahead (`AimAtPathPoint`) never skips a checkpoint point: on Snake Dance the gates sit laterally off the `)p` line (x=0/3040 vs road nodes at 1520), so aiming at the next road node drives past the gate |
| Stuck recovery | `TrackStuck` samples position every 15 ticks. Four samples without movement start `DriveUnstuck`, which holds `Down` and steers for 40+ ticks. Without it a car pinned on a wall keeps holding `Up` forever. A sample where the heading changed by more than 8° resets the count: a missed gate or U-turn moves little while swinging round, and reversing out of that turn is wrong |
| Repairs | `HeadForFix` returns early while `Missed`, and while `NearNextCheckpoint` (the next gate is three path points away). A gate always outranks a fix hoop. It also only targets a hoop whose approach point is still ahead (`FixAhead`, within 16 path points), except that a `FixRoad` is reachable at any array distance when `Fixing == Nearest`: node-list position is not detour distance, and a critically damaged driver should go to an explicit `)po` lane; chasing an approach behind the car is what made a damaged driver U-turn and circle the hoop. The original's fix radius is 2000 in 1/100-unit squared distance, i.e. `200_000` in `DistanceSquared10` (1/10-unit) terms — the raw 2000 is 100× too tight. With no `)po` and no `)pr`, the approach is the path point nearest the hoop: a ground hoop on that line is repaired by driving through it, while an aerial one with no launch is skipped. A hoop above/below the path (`FixAerial`, >1500 Y apart) is only attempted when a ramp or `)po` route leads to it (`FixRamp`); the approach becomes the ramp, `RampChoice` is forced to `Take`, and `TargetsFix` keeps the turn-in from skipping it. A `)po` fix-road start (`FixRoad`, searched 12 points back for the furthest-back marker so a closing `)po` is not taken for the start) becomes the approach; while it is ahead and before the next gate the driver turns onto it even for `OnTheWay` repairs, then follows the branch. A hoop more than 8000 units from the tracked path is ignored — its fix lane was trimmed (or is a detached practice area), so it must not be paired with an unrelated main-line ramp. On a chosen `)po` lane the tracker is pinned to the entry: `ChoosePathPoint` aims at the **current** lane node until the car is within ~3000 units (`FixLaneCaptureSquared`), then advances one node; aiming at the next node made the car orbit the entry/next node forever. A lane is enterable from either end: `FixApproachEnd` is the closing marker, taken with `FixDirection = -1` (reverse). `FixLaneActive` records a deliberate entry so the initial nearest-point search landing on a lane node is not mistaken for one. Leaving a lane clears `Fixing` and starts a 120-original-frame cooldown so it does not immediately turn back in. A ramp-only fix (`)pr` with no `)po`) is not critical-damage-only: the `OnTheWay` branch takes the ramp while it is ahead |
| Survival | a car over 50% damage does not start attacks; it races for the line instead |
| Y axis | `Position.Y` is height *below* `World.Ground` and `CarPhysics.Scy` is positive downward; the original's `car.y`/`wheelSpeedY` are the other way round. The stunt take-off and landing triggers both sum `Scy`, so check the sign against a real jump |
| Controls are ours | `Control` is not cleared between ticks — every bot must write Up/Down/Left/Right/Handb every `RunAi()`, or the car keeps its last input |

---

## Race HUD

- `nfm-world/UI/Hud/HudSkinRegistry.cs` registers Classic, World and discovered JSON skins;
  `UI/Routes/RaceHudView.cs` mounts the selected entry. Pause/settings stay outside the skin subtree.
- Settings > Game > HUD stores `hud_skin <id>` (default `classic`, from `HudSkinId.Default`); old `Classic`/`World`
  values migrate. Missing skins fall back without losing the saved ID.
- World (`nfm-world/UI/Hud/WorldSkin/`) is Sx in the main-menu blue (`Theme.Menu*`): stats top-left, destination
  arrow top-centre, standings right, radar bottom-left, speed bottom-centre. It draws its own radar (`WorldRadar`), so
  `RaceOverlays` skips the shared one for it (`RaceHudView` passes `race.Skin.Value == "world"`). Racers/arrow are
  plain lists; slots re-read them off `UiState.RaceState.PresentationRevision`, bumped once per frame after
  `RacePhase.UpdateHudPresentation`.
- **Gadget skins** (`nfm-world/UI/Hud/GadgetSkin/`) are a port of phy-angled-physics's DS-addons HUD pack: `dsmod2`,
  `blackfusion`, `digital7`, `s7`, each a whole-set replacement drawn in phy's 800×450 frame (speedometer, position,
  damage/power, guidance arrow, opponent list, radar, chronometer, track visor). `GadgetHud` applies the safe zone and
  `HudScale` and maps every helper from phy coordinates to screen, so a port reads like the original. All seven
  gadgets per skin are immediate-mode `RenderContent` draws through the nanovg-style `IGraphics` path API (`Arc`,
  `BezierTo`, `Ellipse`, polygons, rounded rects) — vector-crisp at any resolution, no sprite art. `GadgetRadar` shares
  the rotating map and blips. They draw their own radar and opponent list, so `HudSkinRegistry.DrawsRadar` keeps the
  shared `RaceOverlays` radar out of the way. `digital7`/`s7`/`dsmod2` use the `Digital7`/`Yukari` fonts phy shipped
  (`data/fonts/`, baked to `.msdf` by the build). The gadget-only data (`SpeedFraction`, `TopSpeed`, `LapSplits`,
  `ArrowDistanceMeters`, `MusicTrack`) rides `IHudData`, filled by `RacePhase.UpdateHudPresentation` from its own race
  clock; no gamemode tracks it.

  Porting rules — each of these silently produces a mirrored or shifted HUD:

  |Rule|Why|
  |---|---|
  |**Angles**: Java2D `fillArc`/`drawArc` measure y-up (0° at 3 o'clock, positive counter-clockwise); `G.Arc` is y-down. Emit the wedge as a polyline with Java's own `cx + r·cos θ, cy − r·sin θ` rather than converting angles — `G.Arc`'s large-arc/sweep flags disagree with Java2D past a half turn|A straight conversion mirrors every dial|
  |**Text**: phy's `drawString` anchors at the baseline; `IGraphics.DrawString` anchors at the line-box top. Add `metrics.LineHeight − metrics.Ascent` (the `IFontMetrics.Ascent` member exists for this)|Labels sit a descent high otherwise|
  |**Fonts**: phy lays out against Arial. `NotoSans` is the closest shipped metric; `DroidSans` is visibly wider and shifts centred/right-aligned text|Jitter in every label|
  |**`List<T>` does not implement `IReadOnlyList<T>`**|`RaceState.LapSplits` needed an explicit `IHudData.LapSplits => LapSplits`, or the interface's empty default won and the chronometer never drew|
  |**Overlapping translucent shapes**: Java2D `Area` unions (`BF_pow`, dial pie + closing polygon + skirt) must be one outline, or every overlap blends twice|Build a `ShapeRegion` (LibTessDotNet, positive winding, boundary contours) once and draw it with `FillRegion`/`StrokeRegion`. Flatten arcs at 8° and offset ellipse vertices half a step off the axes; finer steps and axis-aligned vertices leave hairline gaps in the fill|
  |**Pixel-exact art**: Apos grows every shape by its AA band and rounds caps/joins, so a 1px phy line renders ~1.5px|`GadgetHud` brackets drawing with `G.SetExactCoverage(true)`: centred AA at `aaSize` 1, square caps, mitre joins (Java2D's default stroke). Nothing else should enable it|
  |**Gauge points**: phy's `px − (int)(sin·r)` truncates toward zero, and its `sin`/`cos` tables are doubles rounded to float|Use `GadgetHud.Gauge`, not `Polar`; a float `MathF.Cos(120°)` truncates a pixel the other way. Needle angles are `(int)` too|
  |**Speed units**: S7's needle is `120 − (int)(|speed|·2/3)` in raw speed, not a fraction of top speed|`IHudData.TopSpeed` carries the stat; the labels run `0` at −120° to `360` at +120°, coloured by whether the needle has passed them|
  |**Anchoring**: phy drew on a fixed 800×450 canvas; a plain origin at the safe zone's top-left makes the right-hand and bottom gadgets float off their edges as `hud_scale` changes|`Anchor(HudAnchor.Top/TopRight/BottomLeft/BottomRight)` before each gadget group: the anchor point of the 800×450 frame is pinned to that edge of the safe zone, so the gauge grows away from the edge it belongs to. Left/top stay top-left|
  |**Thin rings**: a *filled* annulus one or two phy units thick loses chunks to the fill solver and its outer edge tears|Stroke it at the band's mid radius with its exact width. S7's r80..82 and r82..83 rings both are|
  |**Angle conventions differ**: phy's gadget trig puts 0° at the *top* (`px − sin θ·r, py − cos θ·r`); `IGraphics.Arc`/`ArcStroke` put 0° at 3 o'clock and sweep y-down|An arc ported from phy needs its start reduced by 90° (`-120, 240` becomes `-30, 240`). Getting this wrong draws the arc over the opposite half of the dial|
  |**Java2D arc width is a diameter**: `fillRoundRect`/`RoundRectangle2D` take the corner arc's *diameter*, so passing phy's number straight through cuts every corner twice as deep|Halve it when building a `ShapeRegion.RoundRect`. `BF_pow` passes 15 and needs 7.5; at 15 the panel's cut corner drops below the half disc beside it, leaving that disc's rim standing proud as a bump|
  |**Concentric dial parts**: phy's DSmod2 ticks and needle sit at (750,400) while the arc that outlines the dial is drawn about (746,395) at radius 75|Draw both from the arc's centre, with every tick's outer end exactly on its radius. phy's own ticks overrun the rim on one side and stop short of it on the other|
  |**Do not copy phy's truncation into curved contours**: it applied `(int)` to tick endpoints and ring-band points, which makes a 1px band wander in thickness|`Gauge` is for ticks/needles only. Ring bands use continuous `Polar` points, and a band under a pixel wide is stroked, not filled|

  The ground truth is phy's own painters rendered headlessly: `scratch/gadget-ref/` (gitignored) compiles the
  decompiled `GadgetPainter.java`/`Chronometer.java` against small stubs and writes `png/ref-<variant>-{dark,bright}.png`
  at 800×450. Build it with any JDK 17 (`javac -d out src/com/radicalplay/nfmm/*.java`, then
  `java -cp out com.radicalplay.nfmm.RefMain <1..4> <out.png> <phy-font-dir> [dark|light][solo][scale]`). Capture the
  NFMW side at the same size with `NFMWorld/UiHarness/race-hud-800-*.json` (or `race-hud-<skin>.json` for 1280×720,
  which is the same frame at the game's HUD scale). `race-hud-wide.json` (1920×720) and `race-hud-tall.json` (800×1000)
  are the anchoring fixtures: right-hand and bottom gadgets must stay on their edges in both — after the fixes above
  every gadget matches phy within ~2 px at the reference size.
  `IGraphics.SaveState`/`RestoreState` carry the **stroke width and the coverage mode**, not just the
  clip: a HUD that sets a 3px outline inside a save/restore must not leave every later node drawing 3px
  outlines. `GadgetHud` and `ClassicHud` both rely on that.

  "Now listening" is an **announcement**, not a state label. `RacePhase` reads `GameSparker.CurrentMusic`
  and fills `hud.MusicTrack` only for a few seconds after a track starts or is replaced, and leaves it
  null when nothing plays. Deriving it from the stage's configured soundtrack kept the visor on screen
  for the whole race, and on screen even with music off.

- `hud_scale` is a 0.5–2 user multiplier (`HudScaleSettings`, default 1), live in the HUD preview; Classic adds a
  1.15 default multiplier (`HudScaleSettings.ClassicDefault`). `HudScaleHost` isolates the skin's scale from menu UI.
- `hud_safezone_x`/`hud_safezone_y` keep 0.8–1 of each viewport axis (default 1). Skins inset anchors symmetrically via
  `HudSafeZoneSettings`, without reducing scale. Settings/preview controls stay outside the safe area; the preview
  shows a boundary guide.
- Keep `HudScaleHost` absolutely positioned: countdown/finish overlays must not consume flex space beside the HUD.
  TT position compares the player with its recorded ghost; ghost checkpoint progress updates during playback, while
  arrow targeting still excludes fake players.
- Skins consume `NFMWorld.Library/Hud/IHudData.cs`; `RacePhase.UpdateHudPresentation` copies standings/targets without
  retaining plugin objects. Keep telemetry/target logic in the host.
- **Standings order** is the server's, not the grid's. `PvpServerGamemode.CompleteRace` sorts into three keys: finishers
  and still-running cars by position, then DNFs newest death first. A car's wasted flag is relayed by its owner in
  `CarFrame.TheBitFlags.Wasted` and exposed to gamemodes as `IServerGamemodeContext.IsPlayerWasted`; `TrackWasted`
  stamps the tick each death is first seen. Reviving through a fix hoop clears the stamp, so a revived car is live
  again and can still win. A forfeit stamps the same field, so a leaver sorts with the dead.
- **Race constraint** — the PvP `constraint` config option (`racing` / `wasting` / `both`, default both) is parsed
  into `RaceConstraint` (library, `[PluginVisible]`). It gates the finish: `PvpGamemode.Finish` only fires on the
  first car across the line when `Racing`, and `PvpServerGamemode.CheckConstraint` ends the race when at most one car
  is alive when `Wasting`. "Alive" is the current relayed state (`WastedAt < 0`), not a latched first death, so a car
  revived through a fix hoop still counts. A solo session never satisfies the wasting rule.
- **Watching a race you are not in** — `C2S_SpectateSession` (lobby → `S2C_RaceStarted` with
  `Spectator = true`). The GM hands out one spectator token per race
  (`Lobby2RaceServer_CreateRaceResponse.SpectatorToken`); a connection using it joins `session.Clients`
  but never `LoadedCount`, so it receives every `S2C_PlayerState` (relayed to all clients) without
  affecting the race start or the gamemode's players. The timestamped `_sessionMatches` map in
  `GameOrchestrator` is what lets the lobby find a running race to hand over; it is cleared when the
  GM reports the result. On the client the roster has **no** `ClientPlayer` (which is why
  `IGamemode.ClientPlayer` is nullable and `BaseClientGamemode.FirstClientPlayer` replaced `Single`);
  `RacePhase` then rides `SpectateCar()` — the lock-key target, or the leader — and skips `Forfeit` on quit.
  It relays no `PlayerState` at all (`_spectating` guards the bot loop, or an ownerless bot would look like one
  it fielded), and it is neither winner nor loser: `PvpGamemode.Finish` leaves `ClientWon`/`ClientLost` false when
  `ClientPlayer` is null, so a watched race does not end on `YOU LOST!`.
- A **wasted** client is spectated: `RacePhase.GameTick` passes `TargetCarFor(car)` to the camera instead of the
  client car. The lock keys pick the target (same `LockTargetHelper.LockedCar` the arrow uses, which refuses a
  wasted car), falling back to the nearest live opponent. `IHudData.Spectating` carries this to the skins, which draw
  only the name (the arrow has no heading to point from). A revive clears `Wasted`, so the camera returns by itself.
- The race ends when **any** car's `CurrentLap` reaches `Nlaps` (`PvpGamemode.Finish(winner)`), not only the
  client. `HudState.StateText` carries `"<name> finished first, race over!"` / `"You finished first, nice job!"`,
  and `HudState.Finished` / `ClientWon` / `ClientLost` are copied to `UiState.Race`; `RaceHudView` draws
  `YOU WON!` / `YOU LOST!` in the Adventure font at the top **only for the classic skin**. It uses
  `GlassCardUi.CenterText`'s optional top/height band and font, which centres it the same way as the small message
  (the default row/flex centring did not centre horizontally).
- Race status text is `RaceAnnouncer` (`NFMWorld.Library/Hud/`), ticked from
  `BaseClientGamemode.UpdateHudAndSounds`: it diffs car/opponent state and posts into two timed slots
  (`StatusMessage`, `StuntMessage` on `HudStateData`). Durations are **seconds** and converted with `SecondsToTicks`;
  phy's counters were 21.4 tps, so copying a tick number fires a third as late at 63 tps. `nfm-world/UI/Hud/RaceMessages.cs`
  draws both lines outside the skin; blinking is gated on `SettingsMenu.HudFlash` (`hud_flash`, default on) along with
  the Classic damage-bar blink. Kill credit reads `CarPhysics.LastDamageFrom`, written by `Regx`/`Regy`/`Regz` when
  they actually deal damage. Missed-checkpoint detection lives in the announcer (`CheckpointNear` / `CheckpointFar`,
  ground contact required), not in the physics step where phy kept it.
- Custom skins live in `data/hud/skins/<id>/skin.json`. Schema/discovery/tests: `NFMWorld.Library/Hud/`,
  `NFMWorld.Library.Test/HudSkinTests.cs`; sample: `nfm-world/data/hud/skins/minimal/`. Discovery happens once
  (`HudSkinCatalog.Discover`), never per frame.
- Classic (`nfm-world/UI/Components/ClassicHud.cs`) uses the generated `data/images/power.hud.svg` and
  `damage.hud.svg`; editable sources remain `power.svg`/`damage.svg`. It loads them through `IGraphics.LoadSvgImage`
  with stage snap and the original `loadsnap` channel cap (225); text/outline colors also receive snap.
  `SvgColorAdjustment` adjusts generated hex paints, `fill`/`stroke`/`stop-color`/`color` attributes, and gradient
  stops.

---

## Graphics & audio

- **Backends** — `IGraphicsDevice`/`ICommandBuffer` in `NFMWorld.Graphics.Abstraction`; implementations
  `NFMWorld.Graphics.OpenGL` (ANGLE GLES), `NFMWorld.Graphics.DesktopGL` (desktop core profile),
  `NFMWorld.Graphics.D3D11`, `NFMWorld.Graphics.WebGPU`, and `NFMWorld.Platform.WebGL` for the browser. Selection is a
  `switch` on `Renderer` in `WorldGame`'s constructor (`Renderer.Angle` / `D3d11` / `DesktopGl` / `WebGpu`), and the
  default branch picks **D3D11 on Windows** and **ANGLE everywhere else**. Each desktop backend has a `.Smoke` project
  that runs it headless. The ANGLE arm is `#if ANGLE`, which the game and backend projects define.
- **WebGPU** — wgpu-native via `Silk.NET.WebGPU`, opt-in only (`--backend=webgpu`, or the "WebGPU" video setting).
  Desktop-only: the native package has no wasm build, and the surface is built from the SDL window's Win32 handle, so
  `ParseRenderer` refuses it off Windows before a window exists. Shaders are the bundles' `Wgsl` form, translated at
  build time from the same SPIR-V the other backends cross-compile (`tint`, see the shader pipeline below).

  Traps worth keeping in mind when touching it:

  |Trap|Rule|
  |---|---|
  |`surfaceTexture` is one frame|`BeginFrame` acquires it on the first pass that draws into the surface and `Present` releases the view, presents, and only then releases the texture. Releasing the texture first **discards** the frame — wgpu-native's texture destructor discards any acquisition whose surface has not presented yet — so `wgpuSurfacePresent` then fails with "Surface image is already acquired" (it means the slot is empty, not that it is occupied). The host presents (`WorldGame` submits, then presents); presenting inside `Submit` acquires the surface twice, and presenting a frame that never opened a surface pass is an error, so `Present` is a no-op unless a frame was acquired|
  |`destroy` is not a refcount drop|wgpu invalidates a destroyed texture/buffer for every submission made *after* the destroy, and the whole frame is one command buffer submitted at the end. Apos' glyph atlas and `ImmediateBatch3D` legitimately replace and dispose their old texture/buffer mid-frame, so `WebGpuGraphicsDevice.DeferDestroy` queues the destroy until just after the next submit. Without it the process panics inside Rust with `Texture Id(...) is destroyed`|
  |A pass resolves its ids when it ends|`wgpuRenderPassEncoderEnd` runs `run_render_pass`, which looks up every bind group, attachment view and pipeline the pass recorded. Releasing one of those while the pass is still open panics on a vacant storage slot ("Id(...) does not exist"), and the abort is inside the native library, so nothing in .NET sees it. A variant rebind mid-frame (the uniform ring growing, the glyph atlas changing the texture) releases the group the open pass already recorded, which is why those releases go through `DeferDestroy` too, not just destroys. The smoke's `REBIND`/`RING` checks are that regression, and reverting the deferral reproduces the panic|
  |Pipelines bake attachments|colour/depth format and sample count are part of the pipeline, so `WebGpuPipelineState` builds a variant per (colour format, depth format, samples, filterability) - `Poly` alone draws into both the swapchain and a single-sample shadow cascade|
  |Uniform and staging writes|`queue.writeBuffer` is ordered only against submits, so two writes to one block with a draw between them would collapse; per-draw uniforms go through a dynamic-offset ring and `UpdateBuffer` records a `copyBufferToBuffer` from a staged chunk, both inside the recording|
  |Copies move whole words|every queue write and buffer copy is in 4-byte units, so a buffer whose logical size is not a multiple of 4 — an index buffer of an odd number of 16-bit indices, 870 bytes — allocates `WebGpuBuffer.GpuSize` rounded up and pads the copy length. An unaligned size is not clamped: wgpu rejects the copy, the update is silently lost, and the only trace is the uncaptured-error callback (`CheckUnalignedCopies` covers it)|
  |Validation failures are invisible otherwise|wgpu logs them to stderr and carries on, so `DeviceSetUncapturedErrorCallback` is the only way the game sees one; `ReportError` reports each distinct message to `GraphicsDiagnostics.Warning` (once per device, capped). That is how the copy bug above was found: 2,649 validation errors in one boot — a queue write and a recorded copy per update — while every frame kept rendering|
- **Text/fonts** — `FontStashSharp` with pre-baked MSDF atlases. `NFMWorld.MsdfCache.targets` bakes
  `data/fonts/*.msdf` at build time (the game loads that file instead of baking ~220 glyphs at startup); adding a font
  means adding it under `data/fonts/`. The baker is built by a **nested** `MSBuild` call that passes
  `BuildProjectReferences=false`: its properties (`RuntimeIdentifier=`, `PublishReadyToRun=false`) are global, so
  letting them reach the tool's references builds a second instance of Apos.Shapes and its chain into the same
  `obj/Release/net11.0` the surrounding build is writing, and the loser fails with `CS2012 ... dll for writing`
  (the wasm publish died on exactly that). Builds only the tool, so the bake hooks `BeforeTargets="CoreCompile"` -
  the references exist after `ResolveReferences`. Any other nested build must keep its projects disjoint from the
  outer graph, or strip the leaking properties.
- **Audio** — FAudio (`NFMWorld.Audio.FAudio`) plus NAudio/LibOpenMPT for tracker formats, behind
  `NFMWorld.DriverInterface`. There is no BASS path.

## Shader Pipeline (HLSL → SPIR-V → C#)

Shaders live in `data/shaders/*.fx` with `*.fxh` headers (`Mad.fxh` is included by four shaders). At build,
`NFMWorld.ShaderCompiler.Task` — imported via `NFMWorld.ShaderCompiler.Task/NFMWorld.ShaderCompiler.targets` — runs
before `CoreCompile` and:

1. compiles HLSL with **shaderc** (`Silk.NET.Shaderc`), targeting Vulkan 1.1 SPIR-V,
2. reflects and cross-compiles with **spirv-cross** (`Silk.NET.SPIRV.Cross`) to the per-backend sources (HLSL for
   D3D11, GLSL for GL), and, when the `tint` CLI is present (`Stride.Dependencies.Tint`), runs the same SPIR-V through
   **tint** to WGSL for WebGPU,
3. emits **one C# bundle class per technique** under `obj/…/ShaderCompiler/`, and `IncludeShaders` globs those into
   the compile.

WGSL is optional per build: `WgslCompiler.Available` is false where the tint native is missing (a RID the package
does not ship) **or on macOS at all** — the package's osx-arm64 `tint` is built without Dawn's SPIR-V reader, so every
translation would fail with `invalid value 'spirv' for option '--input-format'` — and the bundles then carry an empty
`Wgsl` string. `WgslCompiler.UnavailableReason` carries the reason the build log prints. WGSL only feeds the WebGPU
backend, which refuses to run off Windows. Three things about the path are not obvious and each
was found by a validation failure, not by reading docs — see `WgslCompiler.cs`:

| Gotcha | Rule |
|---|---|
| Non-uniform derivatives | the tint invocation passes `--allow-non-uniform-derivatives`; six fragment stages sample under divergent control flow |
| tint's `diagnostic(off, derivative_uniformity);` | the naga inside the shipped `wgpu_native` rejects `diagnostic` directives outright, so the emitted line is stripped |
| `return;` after `discard;` | tint emits it and naga rejects an instruction after `return`, so it is stripped too |
| Bindings | SPIR-V decorations are renumbered before tint (uniform 0, textures 1..T, samplers T+1..), because HLSL's `b0`/`t0`/`s0` all land on set 0 binding 0 and WebGPU refuses that. `WebGpuInterop.TextureBinding`/`SamplerBinding` restate the same numbering for the backend |
| Vertex locations | glslang assigns them in declaration order, not by semantic slot, so the WebGPU backend maps `Semantic`/`Slot` to `@location` through reflection (`glslang-assigns-vertex-location-in-declaration-order`) |
| Stage interface | a fragment stage need not declare every vertex output; `WgslStageInterface.Reconcile` appends a dummy `@location(n)` parameter for each missing one, or pipeline creation fails on the interface mismatch |

There are no `.fxb` files, no `fxc.exe`, and no wine path; the old target that copied `.fxb` into publish is commented
out. To add a shader, drop the `.fx`/`.fxh` into `data/shaders/` — discovery is a glob, there is no `<CompileShader>`
item group to update. Two things that bite:

| Gotcha | Rule |
|---|---|
| Task host | the task is `net11.0` and loads out-of-process (`TaskHostFactory`); that is what lets its own project be rebuilt. It will not load under .NET Framework `msbuild.exe` |
| One-to-many | a `.fx` file can produce several techniques (`Nvg.fx` yields four, `Poly.fx` two), so nothing may name a bundle by `%(Filename)` |
| Incremental inputs | `CompileShaders` is skipped on a stamp, so anything that can change a bundle's *contents* must be an `Inputs` entry: the shaders, the headers, the task assembly, **`NFMWorld.ShaderCompiler.dll`**, `tint*` beside it, and the targets file itself. Naming only the task assembly meant a compiler change (the whole WGSL stage was one) never invalidated existing bundles — the DebugDevServer tree kept WGSL-less bundles and failed at WebGPU pipeline creation. Point them at genuine files, or wildcards that can match none: a named input that does not exist makes MSBuild run the target on every build |

`NFMWorld.ShaderSourceGen` still exists in the solution for the old FNA-style effect wrappers, but the game does not
reference it; new shader work goes through `NFMWorld.ShaderCompiler`.

---

## Serialization (TightPack)

`TightPack/` is the wire and on-disk format: a source-generated, reflectionless serializer shaped like MemoryPack.
MemoryPack still builds inside the solution (the `MemoryPack/`, `FixedMathSharp/` and `Maxine.Extensions/` submodules
reference it) but **no NFMWorld code uses it** — treat any MemoryPack reference in game code as stale.

- **Projects** — `TightPack` (runtime), `TightPack.Generator` (analyzer), `TightPack.Tests`, and
  `TightPack/benchmarks/TightPack.Benchmarks` (a hand-rolled harness, not in the solution: BenchmarkDotNet 0.15.8
  predates the `net11.0` moniker). Referenced by
  `NFMWorld.Library` and the gamemode plugins (`Private="false" ExcludeAssets="runtime"`), and by
  `NFMWorld.Server.SharedMemory` and `nfm-world`.
- **Attributes** — `[TightPackable]` replaces `[MemoryPackable]`; `TightPackOrder`, `TightPackUnion`,
  `TightPackIgnore`, `TightPackConstructor`, `TightPackOnDeserialized` are direct renames. A `[TightPackable]` type
  must be `partial`, and so must every type that encloses it.
- **API** — `ITightPackable<TSelf>` replaces `IMemoryPackable<TSelf>`; `TightPackSerializer.Serialize(in T)` /
  `Deserialize<T>(ReadOnlySpan<byte>)` replace the MemoryPack calls. The generic overloads constrain on
  `ITightPackable<T>`, so a generic helper that serializes `T` needs that constraint too.
- **No padding on the wire** — the generator only blits a struct after proving its layout has no padding at all.
  `StructLayout(Pack = n)` is honoured as the alignment cap for the struct that declares it, but it is not passed down:
  a struct nested inside a packed one keeps its own layout, so a padded tuple inside a `Pack = 1` struct still blocks the
  proof. Everything unproven is written member by member. `CarFrame` is one of those — it holds `(byte, ushort)` and
  `sbyte`+`fix64` tuples, so it stays member-by-member however the outer struct is packed.
- **Formatters** live in `TightPack/Formatters/`; `TightPackFormatterProvider` resolves the rest at runtime. Generated
  types hold their formatters in their own static fields and register them from a static constructor, so nothing is
  built by reflection and plugin assemblies stay unloadable (`UnregisterAssembly`).

| Gotcha | Rule |
|---|---|
| Value types from other assemblies | `fix64`, `f64Vector3`, `Int3`, `f64Euler` and `Nibble<T>` live in projects that do not reference TightPack and cannot carry `[TightPackable]`. Their hand-written formatters are in `NFMWorld.Library/Serialization/TightPackFormatters.cs`, registered from a `[ModuleInitializer]`. |
| `[TightPackAllowSerialize]` | Defers a member to the provider at runtime. On the *declaring type* it covers every member — the escape hatch for `required`/`init` members the generator cannot otherwise classify. |
| `required` members | Cannot be left unset by a plain `new`, so such a type is rebuilt through an object initializer; a type needing that is not filled in place. |
| Nested types | A nested `[TightPackable]` struct needs its own attribute — the outer one does not reach it. |
| Untyped serialization | `TightPackSerializer.SerializeUntyped(Type, object)` / `DeserializeUntyped(Type, ReadOnlySpan<byte>, ref object?)`, and `writer.WriteValue(Type, object?)` / `reader.ReadValue(Type, ref object?)`, go through `TightPackFormatterProvider.GetUntypedFormatter`, which builds an `UntypedFormatterAdapter<T>` by reflection once per type. Only a dynamic union and reflection-reached payloads use this; every static member is generic. |
| Union base | Serializing a derived event type directly writes no union tag. Round-tripping through a union means serializing as `type.BaseType`. |
| Formatter parity with MemoryPack | `TightPack/Formatters/` covers the same type surface: the BCL collections (`Stack`, `Queue`, `LinkedList`, `SortedSet`, `PriorityQueue`, `Collection`, `Observable`, the `Concurrent*` set, `SortedDictionary`/`SortedList`, `ReadOnlyCollection`, `ReadOnlyObservableCollection`, `BlockingCollection`, `ArraySegment`), `ISet`/`IReadOnlySet`, multi-dimensional arrays (rank 2–4), `Type`, `BitPackFormatter`, `InternStringFormatter`, `Utf8StringFormatter`/`Utf16StringFormatter`, `Brotli*`, and `DynamicUnionFormatter<T>`. Only `Type` and the collections are auto-resolved; the string encoders, `BitPackFormatter` and `DynamicUnionFormatter` collide with an existing formatter for the same type, so they are opt-in via `[TightPackCustomFormatter]` or an explicit registration. |
| `Brotli*` decompression | Every Brotli formatter declares its uncompressed length and refuses one above its limit (128 MB by default) before allocating. Keep that: the payload comes off the network. |
| `Stack`/`ConcurrentStack` order | Both enumerate top-first. `StackFormatter` writes bottom-first and the reader pushes, so the order survives; `ConcurrentStackFormatter` reverses the enumeration because `ConcurrentStack<T>(IEnumerable<T>)` pushes in order. |
| Bit-packed collections | `BitPackFormatter` writes eight elements per byte, so its length header cannot satisfy `TryReadCollectionHeader`'s "one byte per element" bound — use `TryReadPackedCollectionHeader` and bound the group count yourself. |
| Sandbox formatter list | `PluginPolicy.GeneratedFormatterTypes` must name every formatter `TypeParser.BuildFormatterExpression` can emit, because a plugin that declares such a member constructs the formatter in its own static initializer. `PluginSandboxTests.SerializingAnEventWithProviderResolvedMembers_Works` covers it. |
| Perf hot path | Generated code writes a **tuple member element by element** (`MemberWriteKind.Tuple`) instead of routing it through `ValueTupleFormatter`, and the tuple/collection formatters cache their element formatters in static fields. Both exist because a provider lookup costs ~2.7ns against ~1.9ns for a cached field and ~1.1ns for a direct write, and a tuple pays it per element. Any new formatter that loops must hoist its element formatter out of the loop the same way. `TightPackBufferWriter` uses a `[ThreadStatic]` free list for the same reason: the lock it used to hold cost ~24ns, more than encoding a small payload. |
| Version gates | The wire format changed wholesale in the migration, so `SavedTimeTrial.OLDEST_PLAYABLE_VERSION` is 7 and `Rad3dPoly.CurrentTriangulationVersion` is 3. `RadpackType` byte values and union tags were *not* renumbered. |
| Blittable primitives | `LayoutProver.Primitives` lists only types whose memory image *is* their encoding. `System.DateTime` is deliberately absent: `WriteDateTime` uses `ToBinary()`, which differs from the raw image for `DateTimeKind.Local`. Never add a type there whose writer formats rather than reinterprets. |

---

## Assets & data

- Both the game project and `NFMWorld.Library` ship data with `None Include="data\**\*" CopyToOutputDirectory="Always"`.
  Follow that rule; don't invent a second asset pipeline.
- The browser head packs `nfm-world/data`, `NFMWorld.Library/data` **and** the built `data/` output into archives at
  build time — plugin DLLs only exist in the build output, never in the sources, which is why the output directory is
  in that list.
- RAD car/stage meshes are parsed by `NFMWorld.Library/Mad/Rad/RadParser.cs`; a `phyangulation` line selects the
  `Phyrexian` triangulation algorithm (see HoleyDiver below).
- **Never cache derived state in a serialized type's initializer.** A `[TightPackable]` type is rebuilt through its
  parameterless constructor and its ordered properties are assigned afterwards, so a value computed from a
  constructor parameter keeps the *empty* value. `Rad3d.MaxRadius` was
  `{ get; } = CalculateMaxRadius(polys)`, which meant every deserialized car and stage piece had `MaxRadius == 0`:
  its bounding sphere became a point and `RenderQueue` frustum-culled it as soon as its origin left the view
  (visible as pieces popping out mid-screen in replays, which always load embedded `.timetrial` data). Derive such
  values lazily from the property instead. `RadpackContentTests.RoundTrip_Model_KeepsComputedBounds` covers it.
- Native libraries live under `NFMWorld.NativeLibs/libs/` and are resolved by the `DllImport` resolver in
  `NFMWorld/WorldGame.cs` — add a new native file there, not next to the consuming code.

## Archive server

In-tree replacement for the Rust `nfmw-archive`. ASP.NET Core minimal APIs plus Razor Pages, EF Core on SQLite,
uploaded files on disk. `NFMWorld.Archive/ops/README.md` covers deployment configuration and the backup drill.

- **Asset pages.** `Pages/AssetDetail.cshtml` is the human-readable page for one upload at `/asset/{id}`
  (name, kind, description, creator, upload date, size, downloads, SHA-256, download button, and the creator's other
  public uploads). `/browse` and the account page link there; `/assets/{hash}` stays the download endpoint the game
  fetches, so the page renders only rows that endpoint would serve (`IsPublic && !IsTakedown`, otherwise 404). No
  schema change: it reads the existing `Assets` row.
- **Website styling.** Razor pages use self-hosted Blades CSS 3.1.1 (`wwwroot/lib/blades/`, MIT license included).
  Keep local layout tweaks in `wwwroot/css/archive.css`; use Blades semantic HTML, `.container`, `.grid`, and
  `.overflow-auto` rather than rebuilding its controls. No npm or frontend build step is needed.
- **Trust boundaries.** Accounts are the archive's; ratings are only written by a request signed with a trusted
  server key, and only the lobby holds one. A game master is run by anyone, so it reports a finished race to the
  lobby (`LOBBY_RESULTS_URL`) and the lobby forwards it to `/results/match`.
- **Sessions** are opaque tokens stored as `selector.SHA256(secret)`; the plaintext is only in the response and the
  client. A security stamp on the user revokes every session at once.
- **Content-addressed storage.** Every upload is stored under its own SHA-256 (`FileStorage`), so a re-upload of the
  same bytes resolves to the same path — never delete a rejected upload without checking no row still references the
  hash.
- **Edge caching.** `GET /assets/{hash}` is `immutable` for a year at both browser and edge
  (`Cache-Control` plus `CDN-Cache-Control`), so a Cloudflare Cache Rule makes it a one-time origin fetch; the rules
  and zone settings are in `ops/README.md`. Cloudflare does not cache the path by default (no file extension). Because
  of the year-long TTL, every surface that changes a hash's visibility, bytes or download name must call
  `HttpContext.PurgeAssetsAsync(...)` (`Cache/CloudflareCache.cs`): the asset endpoints, the edit/delete pages,
  `AssetModeration` and the bulk `unlist`/`relist`. The hook is opt-in (`Archive__CloudflareZoneId` /
  `Archive__CloudflareApiToken`) and never fails the action on a Cloudflare error.
- **Two upload surfaces, one rule set.** `POST /assets/upload` (the game client) and `Pages/Upload.cshtml`
  (the website) both publish data assets, and they must refuse the same inputs: signed in, `Archive:ReadOnly`,
  `AssetKinds.IsPublishable`, the `.radpack`/`.rad` extension allowlist, `AssetKinds.IsUploadExtensionAllowed`
  (a bare `.rad` model has no kind of its own, so it can only claim Car/StagePiece/Wheel), the size cap, the
  out-of-process validator, and the kind-inside-the-file check. Those gates live in one place now — `AssetUpload`
  (`CheckClaim`, `StoreAndValidateAsync`, `NormalizeName`/`NormalizeDescription`) — so both callers and the edit page
  cannot drift; the website silently lost its extension check once and stored a `.txt` with no validation at all.
  Replays have no website path: `IsPublishable` excludes them, and only `/tt/upload` ranks a time, after simulating it.
- **CRUD is owner-scoped.** `GET /assets/mine` lists the account's own rows (hidden and taken-down included, because
  they are still theirs to fix); `PATCH /assets/{id}` changes name/description/`IsPublic`; `PUT /assets/{id}/content`
  replaces the bytes; `DELETE /assets/{id}` removes the row and its file. `IsTakedown` is administrators-only — an
  author resubmitting their own edit form must not un-hide a moderated row — and only the owner may replace content,
  while an administrator may also rename or delete. `Pages/AssetEdit.cshtml` (`/asset/{id}/edit`) is the website's
  half and applies exactly the same rules; the account page and the asset page link to it, and the account page has
  its own inline delete.
- **A replacement is not an upload.** `AssetUpload.StoreAndValidateReplacementAsync` validates the stored bytes as
  what they are (radpack first, then legacy `.rad` for a model kind) instead of trusting the submitted file name, so a
  legacy row can be replaced by the radpack the editors always produce. The row keeps its id and owner, which is why a
  published link and an inventory entry survive an update; one row per distinct file stays an invariant, so replacing
  with bytes already published under another name is refused.
- **Deleting or replacing unranks its replays.** `TimeTrialInvalidation.InvalidateForAssetAsync` clears `IsValid` on
  every `TimeTrial` that pinned the old hash; invalid rows are excluded from `/tt/leaderboard`, `/tt/replays` and
  `/tt/replay/{id}`, and a later re-simulation of the same (user, stage, car) restores validity. Never delete a replay
  row for this: the player's history is theirs, the ranking is what the missing bytes invalidated.
- **MY CONTENT** is the in-game CRUD surface: WORKSHOP → MY CONTENT, a full-screen overlay
  (`MyContentView` + `MyContentController`) over `UiState.MyContent`. It lists
  `AccountManager.ListOwnedAssetsAsync` and renames, hides/re-lists and deletes through the same endpoints. The
  controller owns the list, the rename modal and the delete prompt (always `MessageWindow.ShowYesNo`), and
  `MainMenuPhase` only pushes it and routes keys — the same shape as `ReplayBrowserController`. It is deliberately
  *not* in the garage: the garage's INVENTORY row is a separate, still-disabled feature. Re-publishing a document
  under a name the account already used replaces that upload (`ArchivePublisher.UploadAsync` →
  `FindOwnedAssetAsync` + `ReplaceAssetAsync`) rather than creating a duplicate.
- **Validation is out of process** (`NFMWorld.Archive.Validator`). `TimeTrialSimulator` is synchronous and cannot be
  cancelled, so the archive runs it as a child process with a wall clock and kills it. A time trial is ranked only by
  that simulation's tick count, never by a number the client sends. `Archive:ValidatorPath` must point at the built
  executable — uploads are refused with "Validator is not configured on this server" without it. Development
  auto-discovers the sibling project's build; deployed archives must set it. The Dockerfile publishes the
  validator to `/validator` and sets both `Archive:ValidatorPath` and `Archive:ValidatorWorkingDirectory`.
- **Web account protection.** `TurnstileVerifier` checks Siteverify with a ten-second timeout, exact hostname
  allowlist and per-form action. Outside Development, set `TURNSTILE_SECRET` and comma-separated
  `TURNSTILE_HOSTNAMES`; missing settings fail closed. Development uses Cloudflare's public test key pair and skips
  hostname/action matching (dummy hostname, no action); Siteverify success is still required. The archive launch
  profile selects Development on localhost:5288. Never deploy with that environment. The site key is public. Web sign-in, all password registration and Discord OAuth initiation require
  verification; Discord start is an antiforgery-protected POST. Native `/auth/login` remains password/throttle-only.
  The login page renders one widget for the selected `flow` (`signin`, `register`, or `discord`); switching flows
  reloads the page with a fresh widget. The game opens `/account/login?flow=register` for registration.
- **Client.** `AccountManager` (`nfm-world/Mad/Account/`) talks to `/auth/*` directly and persists the token through
  the VFS write root; the menu's ACCOUNT item opens `AccountView`. `ArchiveMount` mounts the asset store as a read-only
  VFS layer after local content. It reads `archive/<sha256>` directly from `/assets/<hash>` without a remote listing
  or name-to-hash manifest; existence/enumeration only know assets already fetched. Online search/listing needs
  explicit endpoints, not a VFS-wide asset index.

| Gotcha | Rule |
|---|---|
| Razor page items | `.cshtml` under `Pages/` is picked up as plain `Content` in this project and gets served verbatim as a static file; the csproj declares `<RazorGenerate Include="Pages\**\*.cshtml" />` explicitly |
| `_ViewImports` | must sit at `Pages/_ViewImports.cshtml`, not `Pages/Shared/`, or the layout and tag helpers do not apply to subpages |
| Untrusted decompression | `Deflate.Decompress(source, maxOutputBytes)` and `RadpackSerializer.Deserialize(data, maxDecompressedBytes)` are the bounded overloads; the unbounded ones are for data this build produced |
| Login enumeration | a missing user still pays for a password hash (`PasswordHasher.DummyHash`) and returns the same error as a wrong password |
| Uploaded files | are served with a download disposition and `nosniff` and belong on a separate origin; the in-app endpoint is a development convenience |
| `DateTimeOffset` ordering | SQLite refuses to `ORDER BY` a `DateTimeOffset` (stored as TEXT), throwing `NotSupportedException` at query time. Order by `Id` instead — it follows creation order — or sort in LINQ to Objects when the result set is already bounded. Both `/browse` and `/assets/search` hit this |

## Multiplayer lobby (client)

`nfm-world/Mad/Gameplay/LobbyPhase.cs` (WebSocket to the lobby) and `nfm-world/UI/Routes/LobbyView.cs` (Sx, main-menu
palette). One lobby; whether a race is rated is chosen per race, not by a menu page.

- **Endpoint is a build property**, not a setting — `LobbyEndpoint`/`LobbyPort`/`LobbyInsecure` in
  `NFMWorld.Game.csproj`, emitted as `AssemblyMetadata` and read by `LobbyEndpoint.cs`. `Debug` points at a lobby on
  this machine (`127.0.0.1:7001`, plain `ws`); `DebugDevServer` is the same kind of build with the dev stack's
  `lobby-dev.nfmw.net:443`; `Release` is `lobby.nfmw.net:443`. Deployed lobbies are `wss`, because the client presents
  its session token on this socket; `LobbyInsecure` is only true when the configured host is loopback, and every
  deployed lobby therefore sits behind a TLS terminator that answers `ws` on 443 with `400`.
  `connect <host> [port] [ws|wss]` reaches any other lobby and picks the scheme the same way: plain `ws` for a
  loopback host, `wss` otherwise unless the third argument says which. `NFMWorld.Library`'s `ArchiveEndpoint` follows
  the same split (`http://127.0.0.1:5000`, `http://dev.nfmw.net`, `https://nfmw.net`), and `DebugDevServer` is
  declared in `Directory.Build.props` so it also gets `DEBUG`, symbols and `Optimize=false` — the SDK only
  special-cases the literal name `Debug`.
- **The port is the lobby's HTTP/WebSocket listener** (`LOBBY_HTTP_ENDPOINT`, default 7001), not the game master's
  ENet port. A public lobby must sit behind a TLS terminator that proxies `wss` to it.
- **One transport per phase, and the phase owns it.** `LobbyConnection.TryOpen` is the only entry point (menu item and
  the `connect` command), so the browser guard and `ENetMultiplayer.Init` live in one place. `Dispose` must call
  `transport.Stop()` — otherwise the server keeps a ghost player and the receive loop runs for the process.
- **The view reads `UiState.Lobby` only.** The phase writes it from `GameTick`. `LobbyView.Actions` carries every
  callback; a game row opens `LobbyPage.Match` (`OpenSession`), because a button stops the press reaching its row.
  The room stays vanilla-shaped: a fixed **ROOM PLAYERS** rail, a games table (`Stage`/`Host`/`Players`/`Status`) and
  room chat. CREATE and a game's match lobby replace the games/chat area, not the rail.
- **A game is viewable before joining.** `LobbyPage.Match` renders the selected `LobbySession` (`UiState.Lobby`):
  its slots, status, gamemode, options and chat, with JOIN before the start and WATCH once it is racing. The server
  publishes the gamemode, its parameters and bot slots in `S2C_LobbyState.GameSession`, so a non-member can read a
  game without loading its plugin; the client labels the parameters from `GamemodeCatalog.ConfigFields` (cached per
  gamemode in the phase, never read by a view slot).
- **Lobby state is broadcast on change, not on a timer.** `LobbyExec` only publishes after a prune or
  a timeout; every path that alters visible player/session state (identity, create, join, leave,
  start, finish, moderation disconnect) must call `LobbyStateBroadcaster.BroadcastToAll`. The
  countdown itself is client-side (`UiState.Lobby.NowUnixMs`), so a lingering finished game does not
  need per-second server broadcasts.
- **Status is the match lobby's main panel.** It shows `WAITING FOR PLAYERS`, `RACE LIVE` or `GAME
  FINISHED`, with a standings table: final `SessionStanding`s after the race, or a live order while
  it runs. The old details card is gone; only the **Stage** row remains, and hovering the stage name
  opens `PREVIEW STAGE` (the stage's `desc()` briefing) and `GAME RULES` (gamemode/host/slots/options)
  popovers. Live order is PvP's `PvpServerGamemode.GetLiveState`, pushed by the GM to the lobby every
  two seconds (like chat) and forwarded to everyone not driving that race; the client applies it to
  the viewed session only, so room rows do not churn.
- **A finished race lingers for a minute.** `ReportMatchResultsAsync` stores self-contained
  `SessionStanding`s (names/cars/colours, so a later leave cannot blank a placing) and a
  `ClosesAtUnixMs` deadline; `PruneFinished` keeps the session until it passes, then clears the
  players' membership. The room row and match status show the countdown; `LobbyPhase` ticks
  `UiState.Lobby.NowUnixMs` while any finished session exists. `S2C_SessionLiveState` carries the
  live order and `S2C_LobbyState.GameSession` carries the final rows + deadline.
- **Chat has two channels on one transcript.** `LobbyChatLine.SessionId` is zero for room chat and the game id for
  match chat. The client sends `C2S_LobbyChatMessage.SessionId` (`0` in the room, its current game in a match view);
  the visible page chooses, not membership — a player who backs out to the room while still in a game must not turn
  room chat into game chat. The server refuses a session the sender is not in, and `ChatManager` routes room chat to
  everyone but match chat only to members. `GameOrchestrator` forwards a match-lobby line to the running race over `/race-chat`, and
  `RaceOrchestrator` forwards in-game chat (players and system lines) back over the lobby's `/race-chat`, so people
  waiting in the match lobby see the race talk. Both directions are HMAC-signed and versioned; neither reports the
  other's line back (no echo). The GM derives the chat URL from `LOBBY_RESULTS_URL` (same origin, path `/race-chat`);
  `LOBBY_CHAT_URL` overrides it when the lobby's chat endpoint is elsewhere. Live order uses the same
  derivation at `/race-live`, with `LOBBY_LIVE_URL` as its override.
- **The create page's stage list has two sources.** It renders `UiState.Lobby.CreateStages`, which
  `LobbyPhase` fills on open (`LobbyStageSelection.ShowLocal`) and filters: LOCAL is the shipped stages a session may
  use, ONLINE is the public archive searched on Enter (`GarageController.ContentClient`, `"Stage"`, eligible rows
  only). Selecting a stage that `MultiplayerContentPolicy` refuses sets the selector's restriction and never becomes
  the session's stage. The chosen stage survives a source switch — a public pick is re-added as its own row — so the
  highlight and `Lobby.SelectedStage` never disagree. The page is full-area, with the stage browser beside the
  gamemode/max-players/bots/options column; CREATE keeps the page open until the server lists the new game and then
  opens its match lobby.
- **Accounts are required.** `LobbyConnection.TryOpen` prompts for sign-in on the main menu before connecting;
  successful sign-in continues into the requested lobby on the game tick. Identity is sent only after authentication.
  The server refuses missing/invalid tokens and archive outages or missing `ARCHIVE_URL`; there is no guest fallback.
  `S2C_Authenticated.SessionRejected` clears the saved token only when the archive definitively rejects it, not for
  outages or duplicate-account refusals. Unauthenticated connections cannot act or receive lobby snapshots/chat.
  **One account, one connection, newest wins**: the connection that authenticates an account takes it over and the
  older one is refused and closed. Refusing the *newcomer* instead locked a player out for good — a socket that dies
  without a close stays in the registry until its receive loop ends, so their own reconnect was refused and every
  retry after it. Account changes still require reconnecting.
- **An evicted connection is not retried.** The server closes it with `PolicyViolation` after the refusal packet, and
  `WebSocketMultiplayerClientTransport` treats that status as final: reconnecting would take the seat back and evict
  the connection holding it now. A send that fails or a close that goes unanswered aborts the socket server-side, or
  the peer's receive loop never ends and it keeps its account seat for the life of the process.
- **A session always has a present owner.** Only the creator may start a race and the client hides START for
  everyone else, so `SessionManager` never leaves a session holding a departed creator: `RemovePlayer` (used by the
  disconnect handler, LEAVE and CREATE) drops the player and then either ends the session (nobody left) or hands
  `CreatorId`/`CreatorName` to the lowest remaining human slot, re-owning every bot to them (a bot is simulated by
  the client that fielded it). Creating a session leaves the one you were in first, and JOINing the session you are
  already in is a no-op. Without that, closing the game with a session open left it listed and joinable at
  `0/4`-with-a-name while nobody could start it — which is what made "close the game, reopen, join your old
  session" possible.
- **The handshake waits for an answer, and every request is answered.** `LobbyPhase.UpdateHandshake` re-sends
  `C2S_Authenticate` every 5 s (three attempts) instead of firing it once, and only then says the lobby did not
  answer; REFRESH retries it. On the server, `HandleAuthenticateAsync` wraps the verification so an unexpected
  failure is still refused rather than ignored, and a repeated token on an already-authenticated connection replays
  the acceptance instead of returning silently — that silence was indistinguishable from a reply lost on the wire.
- **Where players reach a game master** is `GM_PUBLIC_ADDRESS`, a
  `privateHost=publicHost[:udpPort][,...]` map applied by `GameMasterRegistry`. The lobby almost never
  reaches a GM at the address players do (compose network, tunnel, private LAN), and the wire carries a
  `CompactIpAddress`, not a name — so the lobby resolves the *public* host and sends that address.
  Unset, players are told the same address the lobby uses. The dev entry form is
  `host:udpPort+httpPort`; production is an SRV lookup, whose HTTP side is assumed to be `https://host:443`.
- **Pausing is local.** `RacePhase.IsPaused` shows the pause menu; only a `LocalRaceHost` also stops the tick
  (`GameTick` returns early). Online the world keeps running, the client just stops driving, and `RaceState` is left
  alone. `RaceHudView.Actions.Restart` is null online, so RESTART is not offered. Quitting an in-progress online race
  calls `IGamemode.Forfeit()` (PvP sends `ForfeitEvent`) and then drops the ENet transport in `Dispose` — the server
  places the leaver last and carries on for everyone still racing; the race only ends early when every player has
  forfeited.
- **Wasted credit.** `CarPhysics.LastDamageFrom` survives subsequent stage damage until two seconds of upright,
  damage-free wheel contact without skidding, or repair/reset. `PlayerState.DamageSource` carries the owner's credited
  roster index plus one (zero means no attacker), outside replay frames. The GM orders owner snapshots and emits one
  reliable chat announcement per wasted transition through `RaceWastedTracker`. Protocol 11 requires matching clients,
  lobby and GM.
- **Spectator feedback.** `SpectatorRaceFeedback` follows the camera's car independently of the local driver's
  announcer, including after death. Target changes seed checkpoint/stunt baselines without playing old events.
  `PlayerState.CompletedStunt` retains the latest awarded combo and sequence after the live counters clear; predicted
  remote physics must not overwrite it. Watched progress owns checkpoint sounds and model glow while spectating.
- **Departures.** ENet disconnect/timeout reasons reach clients through reliable `S2C_PlayerLeft` notices and race
  chat. Departed racers and their bots stay wasted, including after reconnect; fix hoops, pending repairs, resets
  and late owner state cannot revive them. Attach replays departed state silently alongside chat history.

| Gotcha | Rule |
|---|---|
| Rows are built once | a row from `Sx.For` never re-renders; anything that can change (highlight, selection) must be read inside a `Func` in a style/text slot, not captured as a local |
| Chat list keying | `LobbyChatLine` carries an `Id`; keyed by text, two identical lines collapse into one row |
| Status line | the connection text is only written when it changes, so a rejected-action notice is not wiped next frame |
| Membership actions | only the creator starts a race; a member leaves. The old port wired both to `StartRace`, so LEAVE launched a race. START RACE is only rendered for `CurrentSession.IsCreator` |
| Struct `FirstOrDefault` | a struct-valued lookup's default (`uint` → 0) is a real connection index/slot, so lookups that must distinguish "absent" need an explicit `T?` loop or a `.Any(...)` predicate — the GM's re-attach check and the lobby's duplicate-account check both had this |
| Roster order | `MatchGameplayInfo.Players` is keyed by **slot**, but the client gamemode addresses players by **array position**. Always build the array (and the id→index map) through `MatchGameplayInfo.InSlotOrder()`; a dictionary enumerates in no order, and building it as-is put each player in another player's slot, so relayed state landed on the wrong car and opponents never moved |
| Who is in the roster | `PlayerRegistry.ClientInfo` has an empty `Name` until the client authenticates, and `LobbyStateBroadcaster` skips connections that have not, so a half-open connection is not listed as a player |
| Names in the HUD | the sidebar, car markers, the arrow target and the announcer show `ClientSidePlayerInfo.PlayerName`, not `car.Stats.Name` — in a race every car can be the same model |
| Stage list | the create page renders `UiState.Lobby.CreateStages`, filled and filtered by `LobbyPhase` (`LobbyStageSelection`) — enumerate once when the page opens, never per frame. LOCAL lists only `MultiplayerContentPolicy.IsAcceptedStage` entries and accepts a typed archive id; ONLINE is an Enter-only archive search (`SearchStagesOnline`), not a filter of the LOCAL rows |
| Child collections | `Sx` flattens a nested `IEnumerable` child, but an unsupported child throws inside an effect — and the signal machinery swallows it, so the subtree silently fails to mount. Keep the flattened case covered (`SxRendererTests`) |
| WebSocket sends are serialized | a `WebSocket` allows one outstanding `SendAsync`; a second throws `InvalidOperationException` and that packet is lost. `WsClient` (server) and the client transport chain sends per connection, and the server's close is chained behind them so a refusal sent just before a disconnect still arrives |
| Archive outage vs rejection | `ArchiveClient.ValidateTokenAsync` returns `Unavailable` for anything it cannot read — HTML from a proxy, a bad charset, a timeout. Only `401` is `Rejected`, because only that may clear the player's saved token |

## Wire protocol version

`Protocol.Version` (`NFMWorld.Library/Mad/Multiplayer/Protocol.cs`) is declared by every peer — lobby client,
game master client and, as an HTTP header, the lobby→GM API. Packets are TightPack structs with hardcoded
opcodes, so a mismatch does not fail to parse: it parses into fields that mean something else. Both sides
settle the number before anything else is exchanged.

- **Gameplay sockets** — `BaseMultiplayerClientTransport` sends `C2S_Handshake` on every transition to
  `Connected` (`Protocol.ClientHandshakeOpcode = 14`) and intercepts the reply
  (`ServerHandshakeOpcode = -12`) before it reaches `GetNewPackets`. `BaseMultiplayerServerTransport`
  handles the handshake itself and refuses every other opcode from a connection that has not declared one.
  A refusal is an `S2C_Handshake { Accepted = false, Error }` followed by a disconnect; the error names
  both versions so the player knows which side to update.
- **Refusal before close** — the answer must reach the peer, so each transport chains the close behind its
  sends: `WsClient.CloseAsync` continues the send task, ENet queues a deferred `DisconnectLater(0)` marker,
  and Steam closes with `linger: true`.
- **No answer means too old** — a build from before the handshake ignores the opcode, so the client re-sends
  three times at 5 s (`HandshakeRetryDelayMs`/`MaxHandshakeAttempts`), then reports
  `HandshakeError = "The server did not answer the game version handshake…"` and stops reconnecting.
- **Consumers gate on it** — `LobbyPhase` refuses to authenticate or act until `HandshakeAccepted` and pops
  with an "Update required" dialog on `HandshakeError`; `NetworkRaceHost` holds `C2S_RaceLoaded` and reports
  the refusal as a failed race start. Both are default interface members on `IMultiplayerClientTransport`
  (`HandshakeAccepted => true` for transports that never handshake).
- **Lobby→GM HTTP** — `GameMasterHttpClient` adds `Protocol.HeaderName` (`X-Nfmw-Protocol`) to every request;
  `NFMWorld.Server.Game/Program.cs` checks it **before reading the body** and answers `409` with
  `StatusDescription = "protocol N"`. `/race-ended` on the lobby validates the header the same way. The
  header is independent of the HMAC, which would still verify across revisions.
- **Index reuse** — a transport that reuses connection ids (ENet does) calls `ForgetConnection` wherever it
  raises `ClientDisconnected`, or the next peer inherits a handshake it never sent.
- **When to bump** — any packet layout, packet meaning, or opcode-table change. Adding a packet is only a
  bump if an older peer would keep acting on the conversation after failing to send its half of it.
- **Tests** — `ProtocolVersionTests` (in-process stubs) and `ProtocolVersionTransportTests` (real WebSocket:
  refusal received before the close, unversioned packet never reaching a handler, real client accepted and
  usable).

## Virtual File System (Maxine.VFS)

`TheVFS.VFS` (`NFMWorld.Library/TheVFS.cs`) is the single entry point; everything is a `VirtualFS` over `IPath`.

| Type | Role |
|---|---|
| `IPath` | Abstract path interface |
| `MemoryPath` | In-memory path implementation |
| `IoPath` | Wraps real filesystem paths (internal; `IPath.IoPath.Instance`) |
| `FallbackFileSystem` | Chains multiple `ReadOnlyFileSystem`s, trying each in order |
| `MemoryFileSystem`, `ZipFileSystem`, `HttpFileSystem`, `MountingFileSystem` | Other backends |

**Tested behaviours (`Maxine.Extensions/Maxine.VFS.Test`, MSTest):** `GetFullPath` resolves `..`; `Combine` handles
both Windows (`C:\...`) and Unix (`/...`) absolute override; normalization converts `\` to `/`;
`FallbackFileSystem` falls through on `FileNotFoundException` (`OpenRead`/`GetAttributes`) and
`DirectoryNotFoundException` (enumerate) while other IO exceptions propagate.

**Rules:** always normalize `\` to `/` in game and test code; replicate `Path.Combine`'s absolute-path semantics
exactly. Never read files with `System.IO` directly — the browser's MEMFS has nothing in it.

---

## FixedMath / Fixed-Point Math

`FixedMathSharp` (`FixedMathSharp/src/FixedMathSharp/`) provides fixed-point arithmetic for deterministic simulation:
`fix64`, `f64Vector3`, `Fixed4x4`, `DeterministicRandom`, etc. Fixed → float conversions are **lossy** by design:
never use `==` between fixed-point and float values, use epsilon tolerance in tests. A `FixedMathSharp` bump breaks
every dependent project — run all downstream tests.

## Testing

- **Frameworks differ by project — match the one you're in:**
  - `NFMWorld.Library.Test` — **MSTest 4** (`[TestClass]`, `[TestMethod]`, `[DataRow]`, `Assert.AreEqual`). This is the
    project to extend for game/UI/HUD/plugin behaviour. Do not use NUnit APIs here.
  - `NFMWorld.Archive.Test` — **MSTest 4** as well. It boots the real archive through
    `WebApplicationFactory` against a throwaway SQLite file, so the endpoint pipeline and Razor pages are exercised as
    deployed; add archive behaviour tests here, not against mocks.
  - `HoleyDiver.UnitTest` — NUnit (`[Test]`, `[SetUp]`). Leave it NUnit.
  - `FixedMathSharp/tests/FixedMathSharp.Tests` and `Maxine.Extensions.Mathematics.Tests` — xUnit.
  - `Maxine.Extensions/Maxine.VFS.Test` — MSTest, but not in `nfm-world.slnx`; run it from its own folder.
- **Never delete a test.** If an interface changed, update the test to the new contract.
- **Don't run everything** when you touched one area: `NFMWorld.Library.Test` has plugin-loading and unload regression
  tests (`GamemodePluginUnloadTests`, `PluginSandboxTests`) that are slow and GC-sensitive.
- **Source-generated code is untracked build output** (`*/Generated/`, `GenerateCompilerGeneratedFiles` is on).
  After changing a generator, read the emitted `.g.cs` to confirm the shape; don't commit it, and don't rely on a clean
  build alone.

```csharp
[TestClass]
public class SomeTests
{
    [TestMethod]
    public void MethodName_Scenario_ExpectedBehavior()
    {
        var sut = new SystemUnderTest();
        var result = sut.DoThing();
        Assert.AreEqual(expected, result);
    }
}
```

### Phase lifecycle

`BasePhase.Enter()` calls `UiRenderer.ShowPhase(this)`; `Exit()` calls `HidePhase(this)`. Popped phases are queued and
disposed by `PhaseManager.FlushDisposals()` at end of frame (`WorldGame.Update`) — anything holding plugin objects must
be released there, before `PluginUnloadVerifier.Verify()`. There is no per-phase bridge to `Unregister()` any more.

---

## Polygon Triangulation (HoleyDiver)

`HoleyDiver/Program.cs` is the offline triangulator for non-planar n-gons with holes defined by self-intersecting
paths. **Two algorithms, and the default is LibTess**:

- `TriangulationAlgorithm.Libtess` (**default**) — GLU-style tessellation via **LibTessDotNet**: project to the
  best-fit plane, feed one contour, and let the tessellator handle self-intersections. An edge crossing has no source
  vertex, so the `combineCallback` snaps it to the heaviest-weight contributor.
- `TriangulationAlgorithm.Phyrexian` — the original pipeline: dedupe, `ExtractRegions` (mirrored-sequence hole
  detection, ported from a Python reference), reconstruct the outer boundary in path order, bridge-filter, then
  **Poly2Tri** constrained Delaunay, with an ear-cut fallback if Poly2Tri throws.

The choice is per polygon (`Rad3dPoly.TriangulationAlgorithm`, default `Libtess`), set by a `phyangulation` or
`libtess` line in the RAD file (`RadParser.cs`).

Both share `TryTriangulateSimple` (fast path for simple polygons) and the best-fit-plane projection with axis-aligned
fallback when the plane collapses.

**Phyrexian-path lessons (still valid if you touch it):**

**L1 — Convex hull destroys concave features. Never use it as an outer boundary replacement.** A Graham scan was
once used to "fix" incomplete outer boundaries and silently dropped concave indentations (a car rear panel's bottom
`(19,-43) (0,-45) (-19,-43)`). Reconstruct from the original path minus hole vertices, in path order
(`ComputeConvexHull` is still in the file but has no call sites — don't reintroduce one).

**L2 — `ExtractRegions` may return an incomplete outer boundary.** Rebuild `polyLines[0]` as
`initialPoly \ holeVertices`, preserving order and deduplicating.

**L3 — Filter bridge vertices out of hole definitions** or Poly2Tri sees duplicate constraints and fails/degenerates.

**L4 — Map `PolygonPoint` → unique index → `indexMap` → original index and reject any triangle with an unmappable
vertex**, or the triangle count goes non-integer.

**L5 — `ExtractRegions` is a direct port.** It tests all `(i,j)` pairs, walks forward from `i` and backward from `j`,
and requires `polyLines[0].Count >= 6`. Keep it a port.

**L6 — Poly2Tri handles hole winding itself.** Don't pre-reverse.

**L7 — Best-fit plane needs fallbacks**: covariance → Newell's method → `Vector3.UnitZ`; validate that no two 3D
points collapse to one 2D point.

**L8 — Do not add "safety" guards to the mirrored-sequence walker.** Extra bounds and `break` conditions diverged from
the reference and caused subtle mismatches. `k0` advances forward and `k1` backward; it terminates.

**L9 — The `le == 1` check is one-directional.** Only `AllPointsInPolygon(points0, pointsNew)` decides the swap; the
old `!newInsidePoly0` requirement was wrong. For `le > 1` the new region is always a hole.

**L10 — `CombineWithHoles` / `EarCutTriangulateSimple` are fallback-only** (Poly2Tri threw). Don't optimise them.

**L11 — Test polygons are embedded in `Main()`** (~28 vertices, one windshield-like case). Validate with
`dotnet run` and the printed `Plane normal` / `Regions Detected` / `Triangles` lines. The real coverage lives in
`HoleyDiver.UnitTest` (NUnit, 5 parameterised cases).

| Gotcha | Rule |
|---|---|
| Convex hull for outer boundary | **Never** — reconstruct from the original path minus hole vertices |
| Duplicate vertices | deduplicate at `1e-5` before region extraction |
| Bridge vertices in holes | filter out vertices that also appear in the outer polygon |
| Poly2Tri degenerate triangles | check `triIndices.Count == 3` before emitting |
| Hole marker convention | holes are prefixed with `-1` in the poly-lines list |
| Ear-cut fallback | only when Poly2Tri throws; filters by centroid-in-hole |
| Algorithm default | `Libtess`; `Phyrexian` is opt-in per RAD file |

---

## Agent working guidelines

### Before starting

- Identify the subsystem(s) involved and re-read the matching section above. If a doc or code comment mentions Luau,
  `data/uis/`, `LuaUiLibrary`, `NvgSharp`, `FNA`, `fxc.exe`, `.fxb`, or `Lua-CSharp`, it is stale — check the tree
  first. Current facts:
  - No `.luau` file is tracked outside the `luau/` upstream submodule. `NFMWorld.Library/data/` holds only
    `models/` and `stages/`; `bin/` still contains old `data/uis` and `preact-luau` copies from September builds.
    Old `LuaGamemode`/`LuaUiLibrary` doc comments in `ElStupidoAi.cs`, `Plugin.Plugins.targets`,
    `GameModes.cs`, `HudStateData.cs` and `ComponentChildCollection.cs` are leftovers (`LuaStateTextEndsAt` is a
    Unix-seconds marshalling property for `StateTextEndsAt` whose name outlived its consumer).
  - `NanoSvgSharp` was removed; SVG goes through Apos.Shapes (`ShapeSvg`, `IGraphics.LoadSvgImage`).
  - `NFMWorld.Reactor.Test` and `NFMWorld.Reactor.TestFixtures` are empty output folders with no project file.
  - `NFMWorld.FNA3D`, `NFMWorld.MojoShader` and `NFMWorld.ShaderSourceGen` are still in `nfm-world.slnx` but the game
    does not reference them; the FNA-style `*.g.cs` under `nfm-world/Generated/` are stale output.
  - `NFMW.GenerateSinCosTable`, `NFMWorld.GenerateHmacKey` and `NFMWorld.MsdfCache.Tool` are build/codegen tools.

### While working

1. **Todo list for multi-step tasks** — the codebase is large and losing track compounds errors.
2. **Verify generated output, not just build success.** After a generator or shader change, read the emitted file.
3. **Run the tests for the affected project** — not the whole suite (see Testing).
4. **Never delete a test.** Update it to the new contract.

### After completing

5. Tests green in affected projects.
6. If you introduced or significantly changed a subsystem, update the matching section here.

### Do NOT

- Use NUnit APIs in `NFMWorld.Library.Test` (MSTest), or MSTest APIs in `HoleyDiver.UnitTest` (NUnit).
- Rely on OS-native path separators in game or test code — use VFS normalization.
- Read game files with `System.IO` — go through `TheVFS.VFS`.
- Add a second asset-copy pipeline; extend `data\**\*`.
- Flatten MSBuild platform/RID conditionals without testing desktop and `browser-wasm`.

## Managed ENet

`NFMWorld.Multiplayer.ENet` replaces the native `ENet-CSharp` package with a managed port of its bundled enet fork
(nxrighthere/ENet-CSharp 2.4.8, `Source/Native/enet.h`). It keeps the wrapper's public surface (`ENet.Host`, `Peer`,
`Packet`, `Address`, `Event`, `Library`, `PacketFlags`, `EventType`, `PeerState`) and is wire-compatible with the
native library, so a managed peer interops with a native one in both directions.

- **What is ported** — host/peer/protocol state machines, connect/verify/ack handshake, reliable and unreliable
  channels, fragment reassembly (reliable and unreliable), unsequenced windows, timeouts/RTT/throttle, bandwidth
  limiting, CRC-64 checksum callback and broadcast. Compression is not in the fork and is not ported.
- **Fork specifics to keep when diffing against upstream enet** — `ENET_HOST_DEFAULT_MTU` is 1280, the sent-time
  header flag is bit 14 (compression and bit 15 are gone), `ENET_EVENT_TYPE_DISCONNECT_TIMEOUT` is the wrapper's
  `EventType.Timeout`, reliable commands with a payload live in `outgoingSendReliableCommands` (merged with
  `outgoingCommands` by `queueTime`), and timeouts reset `roundTripTimeout` from the current RTT instead of doubling.
- **Serialization is big-endian** (`ENetProtocol*` are `htons`/`htonl` fields); readers/writers live in
  `Protocol.cs`, and command sizes must stay in sync with the packed C structs.
- **Sockets** are `System.Net.Sockets`: servers bind dual-stack IPv6 with `DualMode`, clients bind an ephemeral
  port (ENet leaves them unbound, but .NET requires a local endpoint for `ReceiveFrom`), and IPv4 peers are stored
  normalized while destinations are mapped to IPv4-mapped IPv6.
- **Threading** is unchanged: one host per thread, `Service`/`Flush` drive send and receive; ENet state is not
  thread-safe.

---

## In-game archive integration

Added with the archive/content work; read this before touching content loading, the archive or the editors.

- **Typed radpack loading** — `RadpackReader` (`NFMWorld.Library/Radpack/`) is the only reader game code
  uses. It validates the header kind against the payload union (`AssetKinds` maps archive kinds:
  `Stage` ↔ `Track`, `StagePiece` ↔ `TrackPiece`) and applies `RadpackLimits`. `BackendGameSparker`
  keeps separate car/stage-piece caches; `dynamic_models` stays the car-only manual cache.
  `LoadModelDirectory` rejects a wrong-kind file instead of parsing it as another asset.
- **Gamemode packages** — `RadpackType.GamemodePackage` / `RadpackGamemode` append new tags; old tags
  never change. `PluginAssembly.Load(id, dllPath, packagePath)` prefers a loose DLL and unwraps a
  package only when no DLL exists. Downloadable packages require `PluginPolicy.Supported`; the
  browser's unsandboxed path is only for shipped DLLs.
- **Archive API** — `/assets/search` is keyset paged (`after`, stable id order, `nextCursor`); types
  are validated and kind names come from `AssetKinds`. `/assets/meta/{hash}` feeds eligibility.
  Visibility is one rule: `IsPublic && !IsTakedown`; eligibility additionally requires
  `IsValidated`. Legacy `.rad` uploads are validated out of process (`validator rad`), cannot claim
  Stage, and old rows are not trusted for multiplayer.
- **Stock content** — `StockCatalog` builds deterministic packages from an explicit shipped root with
  fixed metadata (`UnixEpoch`, no description). Stock is never inserted into Archive's asset table
  or upload storage. For replay validation, `stock:<hash>` asks the validator to resolve the claimed
  game id from its shipped local files and verify the canonical hash. Custom content still comes
  from public validated uploads. Startup removes legacy ownerless `Base game content` rows and
  their unreferenced files; user-owned assets and replay records are preserved.
- **Stage browsing** — `StageSelectView` has LOCAL/ONLINE sources. Local options come from
  `MenuPreferences.GetStages`, labelled by `MenuPreferences.GetStageNames`, which reads each stage's
  `name(...)` line without parsing the stage (packed stages keep their file name). The content
  header carries the *selected* stage's name, so `StageSelectPhase` draws no title over the preview
  (it used to, and it collided with the header). Online options
  come from `ArchiveContentClient.SearchAsync(..., "Stage", ...)`
  and carry `archive/<hash>` ids. Choosing an archive option prepares it with
  `ContentCache.PrepareAsync(..., AssetKind.Stage, ...)` before `LoadStage`, and RACE waits for that
  prepare. The lobby's create page uses the same compact selector over
  `UiState.Lobby.CreateStages`, so it offers both sources: LOCAL is the accepted shipped stages,
  ONLINE searches the archive and carries the author; a typed archive id is still accepted.
- **Content cache** — `ContentCache.Mount()` is called before `ArchiveMount`; prepared assets only
  appear after hash verification and typed decode, through a temp-file rename. Synchronous loaders
  read `archive/<sha256>` from the mount; `PrepareAsync` coalesces duplicate downloads and a corrupt
  cached file is deleted and refetched. Its id arguments take **either** a bare hash or the canonical
  `archive/<sha256>` reference (`ContentHashes.TryResolve`), because search results, the garage, the
  stage picker and the `PreparedContent` entries on the wire all carry the reference form — passing
  one through unchanged is correct, and slicing off the prefix first is not needed. Desktop caches to
  disk at `<LocalApplicationData>/NFMWorld/cache/archive` (`%LOCALAPPDATA%`, `~/Library/Application
  Support` or `~/.local/share`), not under the working directory, which varies by launch method and
  may be read-only; the write root is only a fallback when that folder is not writable. Each write
  uses its own temporary name, so a game and a game master under one account can fetch the same
  asset at once. Browser persistence is the VFS write root (MEMFS), so it is session-only.
- **Multiplayer policy** — `MultiplayerContentPolicy` accepts shipped car ids and canonical
  `archive/<hash>` only; `user/` and local paths are refused. The lobby applies it to identities and
  session creation, and the game master applies it again before constructing any stage.
- **Preparation before a race** — `RequiredArchiveContent` lists the stage and each distinct archive
  car. The lobby freezes the roster, sends `S2C_PrepareContent` (nonce) to every member and calls the
  game master's `/prepare-content` with the stage at the same time; members answer `C2S_ContentReady`
  and only then is the race created. A stale nonce, a selection change, a leave/disconnect, a refusal
  or the two-minute deadline calls the start off and restores the session. The game master builds no
  cars, so it is never asked for them; its download has its own HTTP timeout and never consumes the
  create-race window. Bots never acknowledge. A late spectator is not in that handshake: it downloads
  any missing asset itself and the race phase is pushed when the download finishes, so it never delays
  the racers.
- **Publishing** — `ArchivePublisher` packages a snapshot of the live editor document (never a
  reread of the file) and uploads it with `AccountManager`'s bearer session, or writes a local
  radpack with `ExportLocal*`. Both editors show name, description and that the publication is
  public; a failure leaves the document, its dirty flag and its history alone. Packaging failures
  are returned as messages rather than thrown, because the callers are fire-and-forget UI actions.
- **A published car is checked as a car** — `CarModelValidator` (`NFMWorld.Library/Mad/Rad/`) is the
  one rule, run by `ArchivePublisher.PublishModelAsync` before packaging and by the validator's
  `radpack`/`rad` commands before the archive stores it, so a hand-crafted upload cannot bypass it.
  Its structural half is `HasWheelsAndGeometry` (four wheels for the physics' contact set, polygons
  to draw) — the garage's `BackendCar.IsDrivable` delegates to it. The rest covers what nothing else
  checks: every stat defined (`CarStats.InvalidStat`, the silent form of `Validate`), the divisors
  the physics divides by positive (Simag, Grip, Swits[2], Powerloss), no negative or wrapping
  magnitudes, finite vertices, and triangle indices inside their own polygon. The same check runs on
  `ContentCache.TryDecode`, so a client refuses to cache a car it could not drive. Every shipped car
  passes (`CarModelValidatorTests.EveryShippedCar_IsPublishable`), and `powerloss`/`maxmag` at
  `int.MaxValue` are legitimate ("no drag", "never breaks") — only `fix64` stats are magnitude-capped.
- **Packed stage music** — editor exports/publications include both referenced soundtracks in a
  `RadpackTrackWithMusic` payload. The reader attaches these bytes to `StageLoader.EmbeddedMusic`
  for stream playback; it does not extract files or overwrite `data/music`. Canonical stock and
  replay packages keep the existing track format. New music-bearing packages need an updated
  Archive validator; existing packages must be re-exported to acquire missing music.
- **Time trials** — the client notifies `IClientCallbacks.TimeTrialCompleted` once per completed run
  (never for simulation), `TimeTrialSubmissions` writes the recording and a TightPack sidecar before
  network work, and `TimeTrialUploader` retries from the replay browser. The archive simulates the
  exact archived stage *and* car (`tt-sim` takes both paths). `GET /tt/replays` lists every valid
  replay (public, keyset-paged by id like `/assets/search`, optional `query` over stage, car and
  driver); `ArchiveReplayClient` reads it and fetches `/tt/replay/{id}`. `GET /tt/replay/{id}` and
  the browse list are public because they carry only a username, a stage/car name and a time.
- **Replay persistence** — Time Trial transfers the finished `SavedTimeTrial` to the host and stops
  recording into it. `TimeTrialUploader` serializes it once on `SerialWorkQueue`, atomically saves
  the best run and queue files, then uploads independently of that local queue. Capture the account
  before dispatching; workers must not read UI signals or use a later login's token. Browser scans
  run in the background and apply results on `GameThreadContext`. Desktop shutdown drains local
  writes only; an unavailable archive must not delay the next save or shutdown.
- **Replay viewer** — `ReplayBrowserPhase` reads bounded recordings and lists published replays
  from the archive (LOCAL/ONLINE tabs, paged, a manual UPLOAD for recordings still owed);
  `ReplayViewerPhase` applies recorded `CarFrame`s directly (no physics, gamemode, save or upload),
  uses the embedded `StageLoader`/`Rad3d` when present, and drives `RaceCameraDirector` itself.
  Frames are snapshots, not resumable checkpoints. Both views are overlays — the browser is a
  centred panel, the viewer a top-centre band — so neither hides the scene behind it. A frame
  carries `CameraAngles` (`Cxz`/`StaticXz`) so the replay's follow and follow-static cameras point
  where they did while recording; `ApplyToCar` writes them to `CarPhysics`. `CarFrame` is a
  fixed-layout unmanaged snapshot, so adding a field changes both the recording and the
  `PlayerState` wire layout: bump `SavedTimeTrial.CURRENT_VERSION` **and**
  `OLDEST_PLAYABLE_VERSION`. A physics-only change bumps just `CURRENT_VERSION`: older recordings
  still play from their frames, but `SavedTimeTrial.IsOutdated` marks them — a warning in the
  browser rows and the viewer band, no UPLOAD. The archive validator refuses non-current uploads;
  rows already stored at an older `ReplayVersion` stay in `/tt/replays` and `/tt/replay/{id}` but
  are excluded from `/tt/leaderboard`, listed under "Outdated replays" on `/leaderboards`, and
  replaced by the player's next current-version upload regardless of time.
- **Sx visual harness** — run `dotnet run --project NFMWorld -- --backend=d3d11 --sx-harness=NFMWorld/UiHarness/stage-editor-menu.json`
  from the repo root. It mounts the actual editor Sx tree with the real Apos/font renderer, sends
  direct UI events (never OS input), renders offscreen, and writes PNGs plus measured node/hover JSON
  under `.git/sx-menu-captures`. The script supports named-target or pixel `click`/`hover`, `text`,
  `key` (with an optional `"ctrl": true`), `scroll`, `capture`, `assert-hover`, `assert-visible`,
  `assert-aligned`, and `assert-vertical-scroll` (the target must have scrollable content); a `click`
  takes `"button": "secondary"` for the right-click paths. `view` selects
  `stage-editor` (default), `model-editor`, `garage`, `stage-select`, `stage-select-local` (the shipped
  stages with their real `name(...)` labels), `stage-select-compact` (the compact selector in a plain
  dialog), `stage-select-preview` (the live preview overlay with a `desc()` briefing floating over it), `text-input` (a dialog with one `TextInput`, for the editing shortcuts and its right-click
  menu — the harness installs an in-memory clipboard, since it never runs `WorldGame.Initialize`),
  `text-area` (a code editor holding more lines than it shows, for the multiline editor's menu and
  its scroll behaviour),
  `lobby-room` (the room's players rail, games table and room chat), `lobby-match` (a created game's
  slot grid, status and match chat), `lobby-match-live` (live order in the status panel plus the Stage
  hover preview/rules popovers), `lobby-match-finished` (final placings and the close countdown), and
  `lobby-create` (the full-area create page over the shipped
  stage list; its script switches LOCAL→ONLINE→pick→LOCAL, and the ONLINE rows are a fixed fixture set
  because the archive search is network work),
  `replay-browser`, `replay-viewer` or `replay-camera`
  (applies recorded headings through `RaceCameraDirector` and prints the resulting camera).
  `my-content.json` drives MY CONTENT: its rows are a fixed fixture (the archive is network work) and its
  script opens the page, the rename dialog and the delete prompt; `main-menu-workshop.json` asserts the WORKSHOP
  page carries the MY CONTENT row (main-menu rows are named `Main menu item <label>`).
  `--sx-scale=1.25` repeats at a different UI scale.
  This is a UI-only fixture: no gameplay, network, audio, populated stage or scene rendering; writes
  go to an output-local sandbox. Inspect captures with `view_image` and assertions before reporting
  visual fixes. Menu rows must be keyed by menu identity (`Sx.For`), not constructed in a reactive
  slot that accidentally subscribes to their hover signals and replaces them on every hover.
- **Stage editor UI** — the Sx tree lives in `StageEditorPhase.Sx.cs` and mirrors values through
  `_uiRevision`; the controller/history/camera code is unchanged. Panels are absolutely positioned so
  the viewport is the only non-UI area (`WantsMouse` must stay false over it); its measured
  `LayoutBorderPosition/Size` drive scissor, picking and gizmos. Part thumbnails are render targets
  wrapped with `TheGraphics.G.WrapTexture`; `_previewRevision` remounts the image slot when one
  finishes. Hierarchy rows are one `Sx.For` list (borders header, walls, group headers, pieces):
  press-and-drag reorders pieces or groups and moves pieces between groups, and right-click opens the
  context menu (`ContextMenuOverlay`), which is state-driven rather than a native popup. A drag
  tracks `_hierDropRow` plus `_hierDropAbove` (pointer in the row's top/bottom half) and draws an
  insertion line at that edge; the half decides insert-before vs insert-after in `ReorderPieces` and
  the `ReorderGroup` index. Pressing a row that is already part of a multi-selection drags the whole
  selection as one block (`ReorderPieces` / `MovePiecesToGroup` keep its order). Dropping on a group
  header is a membership move, shown as a row highlight instead; wall/border rows are never drop
  targets. Group membership is file order: `#editor_group(Name)` starts a block and
  `#editor_ungrouped` ends it — without that marker ungrouped pieces written after the last group
  were read back into it, which is what made a group swallow the rest of the stage. Load maps group
  pieces by a single cursor over `ScenePieces` (value matching collapsed identical pieces), and
  undo prunes ids that no longer exist so a re-used id cannot inherit an old group. Saving and
  publishing both serialize `BuildStageText()`; publishing parses that snapshot and never writes the
  working document. The parts library's ONLINE category searches the archive for public stage parts
  (`StageEditorPhase.OnlineParts.cs`): pressing a result downloads it through `ContentCache`, adds it
  to `_availableParts` under its `archive/<sha256>` reference and starts placement, so the saved line
  is the reference. `BackendGameSparker.GetStagePart` renames an archive piece to that reference —
  the package itself carries the author's local path — and `StageLoader.ReferencedArchiveModels`
  lets the editor and `ClientStage` prepare a stage's referenced parts before parsing, because a
  part is resolved synchronously. A published stage is self-contained either way: `StageLoader.pieces`
  serializes the resolved `Rad3d`s. The model editor's PUBLISH dialog chooses CAR or STAGE PART
  instead of inferring it from the file's folder.
- **phy/ds stage-format parity** — the remaining phy-addons directives are parsed by `StageLoader`:
  - **Spawn overrides** — `xstart`/`zstart`/`ystart`/`xzstart`/`xystart`/`zystart`, plus ds-addons'
    `angle` (yaw), are kept in `Starts` with the player count they were written for; phy applies a
    line only when that count matches the race, and `startheight` is a wildcard for every car.
    `BackendStage.GetStartTransform(index, playerCount)` resolves them (later lines win), and PvP,
    Rocket and the garage spawn through it. Its Y is the spawn *plane*: a caller subtracts the car's
    `GroundAt` and assigns `Rotation` after construction, the same way `HandleAerialFall` repositions.
  - **`pulsegrnd(a,b,c)`** — the ground and ground-polygon colours scale by `a + b·sin(t·c)` once per
    original tick. The meshes are baked, so the factor reaches `Ground`/`GroundPolys` as the `Tint`
    uniform in `Ground.fx`, and `World.GroundColor` is scaled for the `CGround` polys (which read it
    per draw). The renderer keeps the un-pulsed pair; the editor calls
    `RefreshGroundPulseBase()` after changing ground colours, or the pulse rescales the old pair.
  - **`flameColor1/2/3(r,g,b)` + `flameSnapDiv`** — `World.FlameOuter`/`FlameInner`/`FlameEmber` and
    `FlameSnapDiv`; `Flames` scales each channel by its own divisor (stock 4/3/2, not one shared
    value). `flameColor3` tints the pre-fire embers, which phy hardcodes.
  - **`fset`/`fchk`** — phy's float-compat ordering: the *absolute* height is index 3 and the angle
    index 4, where `set`/`chk` put the angle first and offset the height from the ground plane.
  - **`pile(seed,w,h,x,z)`** — a procedural tree cluster. `StagePileBuilder` ports phy's seeded blob
    into a `Rad3d` (five planes, five sloped collision boxes); `URandom(seed)` is the Java LCG, so
    every peer builds the same shape. Pile placements keep the seed/size so the editor writes
    `pile(...)` back rather than a `set` for a model that does not exist.
  - **`desc()`** — the stage briefing. Both of phy's spellings are read (`desc(index,text)` sets one
    line, `desc(text)` sets all of them, split on the `\n`/`\r` escapes a stage file uses) and the
    five-line limit is kept. `StageSelectPhase` publishes the loaded stage's lines to
    `StageSelectState.DescriptionLines`; `StageSelectView.Preview` floats them over the course in
    `Stage floating description` (no panel, so the stage stays the backdrop) and the phase bobs it.
    The editor writes the briefing back as one `desc(...)` line.
  - **Converging walls** — the PvP `walls` option (off, 1–10 minutes) drives `ClosingWalls`:
    phy's shrink curve, the 60/30/10/0-second announcements and out-of-bounds damage in `CarPhysics`.
    The collision quadtree is built once, so the moving boundaries are appended per query in
    `RetrievePointCollidables`; the wall *pieces* move as `StageObject` positions, which the renderer
    re-reads every tick, which is what makes the visuals follow.

- **Bottom dock tabs** — `StageEditorPhase.Properties.cs`. The dock the parts library lives in is
  tabbed: PARTS plus STAGE/ATMOSPHERE/GROUND/SCENERY/MUSIC/PHYSICS. The property tabs edit the
  `StageEditorTab` fields live (no apply button) and `BuildStageText` writes every one of them:
  `nlaps`, the spawn overrides, `pulsegrnd`, the flame palette, the `soundtrack*` group, `density`,
  `lightson`/`lightdir`, `aerial`, `mountaincoverage` and the phy-addons physics directives. Anything the tabs do not expose stays in
  `PreservedDirectives` (`_preservedDirectivePrefixes`), so a save never drops it — remove a prefix
  from that list when a tab starts writing the directive. MUSIC picks a track from a dropdown over
  `data/music` (VFS enumeration) and previews through `GameSparker.CurrentMusic`; the preview is
  disposed (and the previous track restored) on tab switch, tab close and phase exit. IMPORT uses
  SDL's native picker (`SdlFileDialog` in `NFMWorld.Platform.SDL3`, which marshals the UTF-8 filter
  list and hops the result back to the main thread) and copies the chosen files into
  `data/music/custom/`; `soundtrack()` splits on `,`/`)`, so those characters are stripped from
  imported names.
  Environment colours are **baked into vertex buffers** (`Sky`, `Ground`, `GroundPolys`, clouds,
  mountains read `World.*` and `.Snap(World.Snap)` in their constructors), so a colour edit must
  rebuild the affected object — `RecreateSky`/`RecreateGround`/`RecreatePolys`/`RecreateScenery`/
  `RecreateEnvironment` in `StageEditorPhase.Scene.cs`, each disposing the object it replaces.
  Fog/cloud tint only reaches the sky and cloud meshes, so those edits rebuild those two.
- **Piece identity** — `StageObject.Rad` is immutable and `ClientStageRenderer` keys its visual
  cache by **object instance** (`StageObjectVisualComparer`), so swapping a part or pasting a
  duplicate must replace the `StageObject` itself (`StagePieceInstance.ReplaceObject`) and the
  matching `Stage.Pieces` slot; changing only `PiecePlacement.Object` updates the hierarchy label
  and nothing else. Ctrl+V offsets by the copied pieces' footprint, not one grid cell — copies that
  land inside the originals have their surfaces culled by the coplanar resolver and look missing.
  The parts grid needs enough vertical room that the second tile row (and its label, the part users
  click) is inside the scroller's clip, or those presses are rejected.
- **Test drive** — `StageEditorPhase.TestDrive.cs`. The TEST DRIVE tab picks Solo / With AI cars
  (`FreePlayRoster.Create`, shared with the main menu's free play) and pushes a `GaragePhase` in
  `PhaseManager.Groups.Event` for the tab's **saved** stage (`user/<StageFileName>`); `CarSelected`
  replaces the group with a local `nfmm/pvp` `RacePhase`, and `RacePhase.Exited` pops the group
  back to the editor. Unsaved edits prompt first (`SAVE & DRIVE` / `CANCEL`), because both the
  garage and the race load the file by name. Pushing the garage calls `Exit()` on the editor, so
  `_preserveTabsOnExit` makes `Exit` keep the tabs and walls; `Enter` clears it and re-applies the
  tab's World values (the race's copy of the stage ran `World.ResetValues`).
