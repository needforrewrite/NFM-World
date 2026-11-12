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
| `NFMWorld.Graphics.OpenGL` | ANGLE/GLES backend (the non-Windows default). `NFMWorld.Graphics.DesktopGL` — desktop core GL. `NFMWorld.Graphics.D3D11` — the Windows default. Each has a `.Smoke` harness. |
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
| Rows in a scroll container | Yoga's web default `flexShrink: 1` squeezes list rows to their padding, so a long list shows crushed rows (or one solid block) instead of scrolling. List rows need `flexShrink: 0` — the main-menu items and `LobbyView.Row` set it. |
| Scroll is both axes | `Overflow.Scroll` has no X/Y split in `Styles`, so any direct child wider than the viewport adds a horizontal scrollbar. Put rows and messages in a `width: 100%`, `flexShrink: 0` column inside the scroller; long messages use `Sx.WrappingText` at 100% width. |

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

### Car selection and Time Trial

- Main-menu GARAGE commits with SELECT CAR/Enter; Back cancels. `MenuHistory.Car` is the shared car for
  free play, time trials and lobby identity, persisted through VFS in `data/cfg/menu-history.json`.
  Entering a race remembers only its stage; test drives and dev commands must not overwrite the garage choice.
  The ONLINE list shows each public car's publisher (`CarStatsViewModel.Author`, from the archive
  search result) instead of the collection name; it is remembered while that car is previewed.
- The lobby offers PvP and Time Trial through `GamemodeCatalog.Ids`; config controls follow the selected mode.
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
- **Config in the UI** — the lobby's create dialog renders whatever the gamemode declares, read through
  `GamemodeCatalog.ConfigProperties(id)`, which loads the plugin only to read its schema and releases it again. Nothing
  in the client hardcodes a gamemode's options.
- **Bots in a session** — the creator's client simulates every bot it fielded, and only that client
  (`(player.Info.IsBot && player.Info.OwnerId == ClientPlayer.Info.Id)`). Bots take real slots with fresh ids
  (`SessionBots.Place`, filled at *race start*, so a human joining the lobby first takes the slot), get no join token,
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
| Version gates | The wire format changed wholesale in the migration, so `SavedTimeTrial.CURRENT_VERSION` is 7 and `Rad3dPoly.CurrentTriangulationVersion` is 3. `RadpackType` byte values and union tags were *not* renumbered. |
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
- **Two upload surfaces, one rule set.** `POST /assets/upload` (the game client) and `Pages/Upload.cshtml`
  (the website) both publish data assets, and they must refuse the same inputs: signed in, `Archive:ReadOnly`,
  `AssetKinds.IsPublishable`, the `.radpack`/`.rad` extension allowlist, `AssetKinds.IsUploadExtensionAllowed`
  (a bare `.rad` model has no kind of its own, so it can only claim Car/StagePiece/Wheel), the size cap, the
  out-of-process validator, and the kind-inside-the-file check. Keep new gates in `AssetKinds` and call them from
  both — the website silently lost its extension check once and would store a `.txt` with no validation at all.
  Replays have no website path: `IsPublishable` excludes them, and only `/tt/upload` ranks a time, after simulating it.
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
  `NFMWorld.Game.csproj`, emitted as `AssemblyMetadata` and read by `LobbyEndpoint.cs`. Debug defaults to
  `localhost:7001` over `ws` (the release lobby refuses a debug client); Release is `lobby.nfmw.net:443` over `wss`,
  because the client presents its session token on this socket. `connect <host> [port]` reaches any other lobby, and
  it always uses plain `ws`.
- **The port is the lobby's HTTP/WebSocket listener** (`LOBBY_HTTP_ENDPOINT`, default 7001), not the game master's
  ENet port. A public lobby must sit behind a TLS terminator that proxies `wss` to it.
- **One transport per phase, and the phase owns it.** `LobbyConnection.TryOpen` is the only entry point (menu item and
  the `connect` command), so the browser guard and `ENetMultiplayer.Init` live in one place. `Dispose` must call
  `transport.Stop()` — otherwise the server keeps a ghost player and the receive loop runs for the process.
- **The view reads `UiState.Lobby` only.** The phase writes it from `GameTick`. `LobbyView.Actions` carries every
  callback; JOIN takes the clicked row's id (`Action<uint>`), because a button stops the press reaching its row, so
  the row's own click handler never runs.
- **Accounts are required.** `LobbyConnection.TryOpen` prompts for sign-in on the main menu before connecting;
  successful sign-in continues into the requested lobby on the game tick. Identity is sent only after authentication.
  The server refuses missing/invalid tokens and archive outages or missing `ARCHIVE_URL`; there is no guest fallback.
  `S2C_Authenticated.SessionRejected` clears the saved token only when the archive definitively rejects it, not for
  outages or duplicate-account refusals. Unauthenticated connections cannot act or receive lobby snapshots/chat.
  **One account, one connection**: duplicate account authentication is refused; account changes require reconnecting.
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
| Stage list | enumerate once per dialog mount (`CreateDialog`), not per frame. The create dialog lists only `MultiplayerContentPolicy.IsAcceptedStage` entries (a local `user/` stage cannot be raced) and accepts a canonical archive id typed into search |
| Child collections | `Sx` flattens a nested `IEnumerable` child, but an unsupported child throws inside an effect — and the signal machinery swallows it, so the subtree silently fails to mount. Keep the flattened case covered (`SxRendererTests`) |

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
  fixed metadata (`UnixEpoch`, no description). `StockSeeder` inserts them at archive startup when
  `Archive:StockContentPath` is set. A mod shadowing a stock name does not change the catalog hash.
- **Stage browsing** — `StageSelectView` has LOCAL/ONLINE sources. Local options come from
  `MenuPreferences.GetStages`, labelled by `MenuPreferences.GetStageNames`, which reads each stage's
  `name(...)` line without parsing the stage (packed stages keep their file name). Online options
  come from `ArchiveContentClient.SearchAsync(..., "Stage", ...)`
  and carry `archive/<hash>` ids. Choosing an archive option prepares it with
  `ContentCache.PrepareAsync(..., AssetKind.Stage, ...)` before `LoadStage`, and RACE waits for that
  prepare. The lobby's create dialog uses the compact selector and still accepts a typed archive id.
- **Content cache** — `ContentCache.Mount()` is called before `ArchiveMount`; prepared assets only
  appear after hash verification and typed decode, through a temp-file rename. Synchronous loaders
  read `archive/<sha256>` from the mount; `PrepareAsync` coalesces duplicate downloads and a corrupt
  cached file is deleted and refetched. Browser persistence is the VFS write root (MEMFS), so it is
  session-only.
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
- **Time trials** — the client notifies `IClientCallbacks.TimeTrialCompleted` once per completed run
  (never for simulation), `TimeTrialSubmissions` writes the recording and a TightPack sidecar before
  network work, and `TimeTrialUploader` retries from the replay browser. The archive simulates the
  exact archived stage *and* car (`tt-sim` takes both paths). `GET /tt/replays` lists every valid
  replay (public, keyset-paged by id like `/assets/search`, optional `query` over stage, car and
  driver); `ArchiveReplayClient` reads it and fetches `/tt/replay/{id}`. `GET /tt/replay/{id}` and
  the browse list are public because they carry only a username, a stage/car name and a time.
- **Replay viewer** — `ReplayBrowserPhase` reads bounded recordings and lists published replays
  from the archive (LOCAL/ONLINE tabs, paged, a manual UPLOAD for recordings still owed);
  `ReplayViewerPhase` applies recorded `CarFrame`s directly (no physics, gamemode, save or upload),
  uses the embedded `StageLoader`/`Rad3d` when present, and drives `RaceCameraDirector` itself.
  Frames are snapshots, not resumable checkpoints. Both views are overlays — the browser is a
  centred panel, the viewer a top-centre band — so neither hides the scene behind it. A frame
  carries `CameraAngles` (`Cxz`/`StaticXz`) so the replay's follow and follow-static cameras point
  where they did while recording; `ApplyToCar` writes them to `CarPhysics`. `CarFrame` is a
  fixed-layout unmanaged snapshot, so adding a field changes both the recording and the
  `PlayerState` wire layout: bump `SavedTimeTrial.CURRENT_VERSION` and refuse older recordings
  (`ReplayBrowserPhase.IsCompatible`), which the archive validator already does.
- **Sx visual harness** — run `dotnet run --project NFMWorld -- --backend=d3d11 --sx-harness=NFMWorld/UiHarness/stage-editor-menu.json`
  from the repo root. It mounts the actual editor Sx tree with the real Apos/font renderer, sends
  direct UI events (never OS input), renders offscreen, and writes PNGs plus measured node/hover JSON
  under `.git/sx-menu-captures`. The script supports named-target or pixel `click`/`hover`, `text`,
  `key`, `scroll`, `capture`, `assert-hover`, `assert-visible`, and `assert-aligned`; `view` selects
  `stage-editor` (default), `model-editor`, `garage`, `stage-select`, `stage-select-local` (the shipped
  stages with their real `name(...)` labels), `replay-browser`, `replay-viewer` or `replay-camera`
  (applies recorded headings through `RaceCameraDirector` and prints the resulting camera).
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
  context menu (`ContextMenuOverlay`), which is state-driven rather than a native popup. Saving and
  publishing both serialize `BuildStageText()`; publishing parses that snapshot and never writes the
  working document.
