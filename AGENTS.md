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
| `NFMWorld.Graphics.OpenGL` | ANGLE/GLES backend (the non-Windows default). `NFMWorld.Graphics.DesktopGL` — desktop core GL. `NFMWorld.Graphics.D3D11` — the Windows default. Each has a `.Smoke` harness. |
| `NFMWorld.Platform.SDL3`, `NFMWorld.Platform.WebGL` | Window/input layer. |
| `NFMWorld.ShaderCompiler*` | HLSL → SPIR-V → cross-compiled bundles, at build time. |
| `NFMWorld.Gamemode.Pvp`, `.TimeTrial`, `.Hostile` | Gamemode **plugins** (see below). |
| `NFMWorld.Ai.Classic`, `NFMWorld.Ai.ElStupido` | AI **plugins**. |
| `NFMWorld.Server.Game`, `.Server.Lobby`, `.Server.SharedMemory` | Dedicated server, `RaceOrchestrator`. |
| `NFMWorld.Multiplayer.Base`, `NFMWorld.Api`, `NFMWorld.Sentry`, `NFMWorld.CrashReporter` | Transport, HTTP API, telemetry, crash reports. |
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
  - Run: `dotnet run --project NFMWorld/NFMWorld.csproj`
  - Tests: `dotnet test --no-build` from the solution root, or from a test project folder.
  - Browser: `dotnet build NFMWorld.Browser/NFMWorld.Browser.csproj` (it sets `browser-wasm` itself).

- **Compile symbols**: `ANGLE` is defined in the game project and the backends; `WEB_AUDIO` is set only by the browser
  head (which passes `NfmwBrowser=true` through its references). Keep both when touching compilation logic.

- **Don't** remove or flatten the MSBuild platform conditionals, or the `RuntimeIdentifier != 'browser-wasm'` guards
  that keep `PublishAot`/`NativeLib` off in the wasm head.

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
| Style structs are nullable | `Styles` fields are `T?` on purpose (merge semantics). Setting a field to its CLR default is not the same as leaving it null. |

---

## Gamemodes (plugins only)

Gamemodes are compiled `.NET` plugins — the Lua path is gone. Singleplayer and multiplayer share one code path.

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

### Plugin layout & lifecycle

- **Layout** — `data/gamemodes/{id}/gamemode.dll`, `data/ais/{id}/ai.dll`. The file name is identical in every
  plugin of a kind; the folder disambiguates.
- **Authoring** — derive from `PluginGamemode` / `PluginServerGamemode` (gamemodes) or `PluginAi` (AI), and mark the
  assembly with `[assembly: Gamemode(Name = ..., Description = ...)]` or `[assembly: Ai(...)]`, plus
  `GamemodeConfigProperty` / `GamemodeConfigPropertyOption`.
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

### Gotchas

| Gotcha | Rule |
|---|---|
| Assembly identity | Ship the file as `gamemode.dll` / `ai.dll` by **renaming at copy time** (`Plugin.props`), never via `TargetName`/`AssemblyName` — those rename the assembly *identity* too, and the runtime refuses to load two assemblies under one identity |
| Plugin dependencies | Reference shared assemblies (`NFMWorld.Library`, `MemoryPack`, …) with `Private="false" ExcludeAssets="runtime"` so the plugin resolves against the host's copies instead of shadowing them |
| `DisableTransitiveProjectReferences` | every plugin csproj sets it, so a type used from a transitive dependency needs its own explicit reference |
| Target resolution timing | read `$(TargetDir)` inside the target, not at evaluation time — the SDK sets it after the project body |
| Unload is silent | `AssemblyLoadContext.Unload()` on a context something still references does nothing and reports nothing. Any surviving reference — a gamemode instance, a `CarVisual`, a reflected `ConstructorInfo` — keeps the assembly loaded, so teardown drops them *in order* |
| MemoryPack pins the context | a plugin's generated formatters register into MemoryPack's **static** table from the plugin's module initializer (i.e. on load) and hold its types for the process lifetime. `MemoryPackFormatterRegistry` removes them in `Unload` by reflection (MemoryPack has no unregister API); without it the plugin never unloads, with no error to show |
| Teardown ordering | release the objects that reference the plugin *before* the factory's `End()`. `RacePhase.Dispose` does them in order: `_host.Dispose()`, `CurrentStage.SetPlayers(null)` (drops the `CarVisual`s), `GamemodeInstance?.End()`, `GamemodeInstance = null`, and only then `Gamemode.End()` — the factory release has to be last. `LocalRaceHost.Dispose` ends its server gamemode and then nulls the field for the same reason |
| Verifying an unload | never inside `Unload`/`End()`: the frames above are live roots, so a forced collection always finds the assembly alive. `PluginUnloadVerifier` does it from the host loop. Tests assert it through a `WeakReference` to the plugin's **assembly** after `GC.Collect` (`GamemodePluginUnloadTests`), never by trusting `Unload`'s return — there isn't one |

### Plugin sandbox (CasCore)

Every plugin assembly loads through `CasAssemblyLoader` (`PluginAssembly.Load`), so a plugin only reaches what the
policy admits. `PluginPolicy` builds it: CasCore's default sandbox plus whatever the host marked `[PluginVisible]` /
`[VendorPluginVisible]`. `Unsafe` is denied outright, and MemoryPack's reader/writer primitives are named one at a
time — they carry their lengths, `Unsafe` does not.

| Gotcha | Rule |
|---|---|
| CasCore is a fork | `CasCore/` is a submodule on `uwx/CasCore` branch `nfmw`, carrying two fixes upstream 0.3.0 needs on this runtime: `base.Foo()` calls went through the static guard and were always refused, and the JSON bindings name members .NET 11 no longer has. Rebase both on any upstream bump |
| Policy entries by token | `CasPolicy` identifies a member by **module handle + metadata token**, not by `MemberInfo` instance. Denying through a type-forwarder (`Unsafe` forwards to `System.Private.CoreLib`) removes unrelated members that share the arithmetic — filter to `DeclaringType == typeof(...)` |
| Accessibility is inclusive | `Private` ⊇ `Protected` ⊇ `Public`. `IncludingAllMembersOfType` binds public members plus public and protected constructors; any other protected member a plugin calls needs its own `[PluginVisible]` (`[method: PluginVisible]` on a ctor) |
| Generated MemoryPack code | formatters are ordinary plugin IL, so they only reach the policy. The generator emits `Unsafe.As`/`Unsafe.SizeOf`; those are patched to a cast and `MemoryPackUnmanaged.SizeOf<T>` so the sandbox can deny `Unsafe` (`MemoryPackGenerator.Emitter.cs`) |
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
| Shortcuts | by default `ChooseGoalPoint` targets `PathIndex + 1`, so the driver visits every `)p` node. `TryShortcut` may skip 1–2 non-checkpoint nodes when the skipped nodes stay within 1200 units of the straight car→candidate line (roughly a road width), the line saves at least 500 units, the heading change is at most 90°, and the car is under 50% damage. It never skips a gate or a `FixRoadStart`. More aggressive route choice would need road/wall queries; this corridor rule is the safe version |
| Gates | the next gate is `CurrentCheckpoint` into `TrackPath.CheckpointPath`. The driven point is clamped so it cannot pass an uncleared gate. A miss is the tracked point landing **just past** the gate in route order (`ForwardDistance(gate, index)` in `(0, small]`), not "more than half a lap to the gate" — that is true of normal layouts like stage 1 after its first gate, and sends the driver across the map. `DriverState.Missed` latches until the gate clears. Recovery is a normal-speed turn back to the gate; do not add a special slow-down, or the car ends up crawling and then reversing. Ramps within two path points of a gate are not taken, since jumping the box causes the same miss. The turn-in lookahead (`AimAtPathPoint`) never skips a checkpoint point: on Snake Dance the gates sit laterally off the `)p` line (x=0/3040 vs road nodes at 1520), so aiming at the next road node drives past the gate |
| Stuck recovery | `TrackStuck` samples position every 15 ticks. Four samples without movement start `DriveUnstuck`, which holds `Down` and steers for 40+ ticks. Without it a car pinned on a wall keeps holding `Up` forever. A sample where the heading changed by more than 8° resets the count: a missed gate or U-turn moves little while swinging round, and reversing out of that turn is wrong |
| Repairs | `HeadForFix` returns early while `Missed`, and while `NearNextCheckpoint` (the next gate is three path points away). A gate always outranks a fix hoop. It also only targets a hoop whose approach point is still ahead (`FixAhead`, within 16 path points), except that a `FixRoad` is reachable at any array distance when `Fixing == Nearest`: node-list position is not detour distance, and a critically damaged driver should go to an explicit `)po` lane; chasing an approach behind the car is what made a damaged driver U-turn and circle the hoop. The original's fix radius is 2000 in 1/100-unit squared distance, i.e. `200_000` in `DistanceSquared10` (1/10-unit) terms — the raw 2000 is 100× too tight. With no `)po` and no `)pr`, the approach is the path point nearest the hoop: a ground hoop on that line is repaired by driving through it, while an aerial one with no launch is skipped. A hoop above/below the path (`FixAerial`, >1500 Y apart) is only attempted when a ramp or `)po` route leads to it (`FixRamp`); the approach becomes the ramp, `RampChoice` is forced to `Take`, and `TargetsFix` keeps the turn-in from skipping it. A `)po` fix-road start (`FixRoad`, searched 12 points back for the furthest-back marker so a closing `)po` is not taken for the start) becomes the approach; while it is ahead and before the next gate the driver turns onto it even for `OnTheWay` repairs, then follows the branch. A hoop more than 8000 units from the tracked path is ignored — its fix lane was trimmed (or is a detached practice area), so it must not be paired with an unrelated main-line ramp. On a chosen `)po` lane the tracker is pinned to the entry: `ChoosePathPoint` aims at the **current** lane node until the car is within ~3000 units (`FixLaneCaptureSquared`), then advances one node; aiming at the next node made the car orbit the entry/next node forever. A lane is enterable from either end: `FixApproachEnd` is the closing marker, taken with `FixDirection = -1` (reverse). `FixLaneActive` records a deliberate entry so the initial nearest-point search landing on a lane node is not mistaken for one. Leaving a lane clears `Fixing` and starts a 120-original-frame cooldown so it does not immediately turn back in. A ramp-only fix (`)pr` with no `)po`) is not critical-damage-only: the `OnTheWay` branch takes the ramp while it is ahead |
| Survival | a car over 50% damage does not start attacks; it races for the line instead |
| Standings | PvP never calls `CheckPointHelper.CalculatePositions`, so `Placement` is 0 for everyone. A bot that needs rank computes it itself (`ClassicAi.RankCars`) — calling the helper would take over the HUD's standings as a side effect |
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
  `NFMWorld.Graphics.D3D11`, and `NFMWorld.Platform.WebGL` for the browser. Selection is a `switch` on `Renderer` in
  `WorldGame`'s constructor (`Renderer.Angle` / `D3d11` / `DesktopGl`), and the default branch picks **D3D11 on
  Windows** and **ANGLE everywhere else**. Each backend has a `.Smoke` project that runs it headless. The ANGLE arm is
  `#if ANGLE`, which the game and backend projects define.
- **Text/fonts** — `FontStashSharp` with pre-baked MSDF atlases. `NFMWorld.MsdfCache.targets` bakes
  `data/fonts/*.msdf` at build time (the game loads that file instead of baking ~220 glyphs at startup); adding a font
  means adding it under `data/fonts/`.
- **Audio** — FAudio (`NFMWorld.Audio.FAudio`) plus NAudio/LibOpenMPT for tracker formats, behind
  `NFMWorld.DriverInterface`. There is no BASS path.

## Shader Pipeline (HLSL → SPIR-V → C#)

Shaders live in `data/shaders/*.fx` with `*.fxh` headers (`Mad.fxh` is included by four shaders). At build,
`NFMWorld.ShaderCompiler.Task` — imported via `NFMWorld.ShaderCompiler.Task/NFMWorld.ShaderCompiler.targets` — runs
before `CoreCompile` and:

1. compiles HLSL with **shaderc** (`Silk.NET.Shaderc`), targeting Vulkan 1.1 SPIR-V,
2. reflects and cross-compiles with **spirv-cross** (`Silk.NET.SPIRV.Cross`) to the per-backend sources (HLSL for
   D3D11, GLSL for GL),
3. emits **one C# bundle class per technique** under `obj/…/ShaderCompiler/`, and `IncludeShaders` globs those into
   the compile.

There are no `.fxb` files, no `fxc.exe`, and no wine path; the old target that copied `.fxb` into publish is commented
out. To add a shader, drop the `.fx`/`.fxh` into `data/shaders/` — discovery is a glob, there is no `<CompileShader>`
item group to update. Two things that bite:

| Gotcha | Rule |
|---|---|
| Task host | the task is `net11.0` and loads out-of-process (`TaskHostFactory`); that is what lets its own project be rebuilt. It will not load under .NET Framework `msbuild.exe` |
| One-to-many | a `.fx` file can produce several techniques (`Nvg.fx` yields four, `Poly.fx` two), so nothing may name a bundle by `%(Filename)` |

`NFMWorld.ShaderSourceGen` still exists in the solution for the old FNA-style effect wrappers, but the game does not
reference it; new shader work goes through `NFMWorld.ShaderCompiler`.

---

## Assets & data

- Both the game project and `NFMWorld.Library` ship data with `None Include="data\**\*" CopyToOutputDirectory="Always"`.
  Follow that rule; don't invent a second asset pipeline.
- The browser head packs `nfm-world/data`, `NFMWorld.Library/data` **and** the built `data/` output into archives at
  build time — plugin DLLs only exist in the build output, never in the sources, which is why the output directory is
  in that list.
- RAD car/stage meshes are parsed by `NFMWorld.Library/Mad/Rad/RadParser.cs`; a `phyangulation` line selects the
  `Phyrexian` triangulation algorithm (see HoleyDiver below).
- Native libraries live under `NFMWorld.NativeLibs/libs/` and are resolved by the `DllImport` resolver in
  `NFMWorld/WorldGame.cs` — add a new native file there, not next to the consuming code.

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
