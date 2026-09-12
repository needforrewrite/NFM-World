# Agent instructions — NFM-World

Keep guidance short and actionable. Reference files and patterns below when making changes.

DO NOT write PowerShell or shell scripts for code-editing tasks. ALWAYS use the code-editing tools available to you.

When writing Luau, ALWAYS write type-safe code with type annotations (like if you were writing TypeScript!). Don't just stuff `any` everywhere. Check your Luau code with `luau-analyze` in strict mode and fix any errors unless fixing them is impossible within the type system or would strongly reduce readability.

Don't run all Lua-CSharp tests unless you've touched Lua-CSharp code, as they have a bunch of regression tests that take minutes to execute.

For writing code with Sx cross-reference the [sx guide](NFMWorld.Library\data\library\sx\GUIDE.md).

NFM World is a custom game engine and game written primarily in **C#**, targeting `net10.0`. The playable app lives in `nfm-world/` (`NFMWorld.csproj`) and depends on many sibling projects — notably `NFMWorld.Library`, `FNA.Core` (via NvgSharp), and `MonoGame.ImGuiNet`. Treat `nfm-world/` as the app entry point; engine/framework code is in `FNA/`; rendering and GUI glue is under `NvgSharp/`, `FontStashSharp/`, and `MonoGame.ImGuiNet/` (FontStashSharp is in the solution but not a direct ProjectReference of the app).

- **Big picture:** The playable app lives in `nfm-world/` (`NFMWorld.csproj`) and depends on many sibling projects (notably `NFMWorld.Library`, `NvgSharp.FNA.Core`, `NvgSharp.Text.FNA.Core`, `MonoGame.ImGuiNet`). Treat `nfm-world` as the app entry; engine/framework code is in `FNA/` and rendering/GUI glue under `NvgSharp/`, `FontStashSharp/`, and `MonoGame.ImGuiNet/`.

Key characteristics:
- **Luau/Yoga-based UI** — the UI is written in Luau (`data/uis/` + `NFMWorld.Library/data/library/`) and rendered by a Yoga layout host (`nfm-world/UI/UiRenderer.cs` / `NFMWorld.Library/Util/Lua/LuaUiLibrary.cs`). Replaces the legacy XAML, Reactor VDOM, and CEF systems. New UI work targets **Sx**; preact-luau remains for unmigrated routes.
- A custom **shader pipeline**: shaders in `data/shaders/*.fx` are compiled to `.fxb` via `fxc.exe` during build.
- **Fixed-point math** (`FixedMathSharp`) for deterministic physics and gameplay logic.
- A **virtual file system** (`Maxine.VFS`) with path abstraction over real and in-memory backends.
- A Blender-based asset pipeline using the proprietary **RAD 3D** car format.

- **Build / run:** Use the .NET SDK (this repo targets `net10.0`). Typical commands:
  - Build entire workspace: `dotnet build nfm-world.slnx -c Debug`
  - Build single project: `dotnet build nfm-world/NFMWorld.csproj`
  - Run: `dotnet run --project nfm-world/NFMWorld.csproj`
  - Run tests: `dotnet test --no-build` from solution root or test project folder.

- **Shaders & tools:** Shaders in `data/shaders/*.fx` are compiled to `.fxb` via `fxc.exe` during build (`BuildShaders` target). On non-Windows builds the project expects `wine` + a Windows DirectX SDK `fxc.exe` (winetricks `dxsdk_jun2010`) or a `tools/fxc.exe` helper. If altering shader handling, preserve the MSBuild targets in `nfm-world/NFMWorld.csproj` that produce and include `.fxb` files.

- **Platform nuances:**
  - The project sets `AllowUnsafeBlocks` and several compile symbols (e.g. `USE_BASS`). Keep those when editing compilation logic.

- **Project patterns / conventions:**
  - Most subprojects are referenced with `ProjectReference` from `NFMWorld.csproj`; prefer keeping cross-project ref changes small and use `dotnet sln` only when adding/removing whole projects.
  - Game logic vs UI: `NFMWorld.Library` contains backend/game systems; UI, rendering and native interops live in `nfm-world/`, `NvgSharp/`, and `FNA/`. The Luau/Yoga UI lives in `data/uis/` (routes/components) and `NFMWorld.Library/data/library/` (reactlib + Sx), hosted by `nfm-world/UI/UiRenderer.cs` and `NFMWorld.Library/Util/Lua/LuaUiLibrary.cs`.
  - Data and assets: NFMWorld and NFMWorld.Library include `None Include="data\**\*" CopyToOutputDirectory=...` — follow existing CopyToOutputDirectory semantics rather than inventing new asset pipelines.

- **Dependencies & runtime notes:**
  - NuGet packages used by the app include `ImGui.NET`, `ManagedBass` (and related). When adding packages, prefer matching versions already in the csproj.
  - For local developer builds on Linux/macOS, ensure native dependencies (OpenGL drivers, libSDL, wine for shader compilation) are present.

- **Tests and CI hints:**
  - Run `dotnet test` at repo root; test projects are co-located with their libraries (e.g. `HoleyDiver.UnitTest`).
  - CI should `dotnet restore` then `dotnet build` then `dotnet test`. If CI runs on Linux/macOS, ensure native copy targets won't fail due to missing platform files — add conditional guards or include stub files as needed.

- **When editing MSBuild targets:** Inspect `nfm-world/NFMWorld.csproj` for patterns: shader compilation targets, copy-to-output items, and platform-specific Publish hooks. Changes here affect runtime asset layout; run a local `dotnet publish` to validate.

- **Where to look for behavior:**
  - Initialization / main loop: `nfm-world/NFMWorld.csproj` → `WorldGame.cs`, `NFMWorld.csproj` references `WorldGame.cs` as a logical entry point.
  - Luau/Yoga UI host: `nfm-world/UI/UiRenderer.cs`, `NFMWorld.Library/Util/Lua/LuaUiLibrary.cs` (Yoga layout, events, `defer` batching).
  - Luau UI views: `data/uis/` (`router.luau`, `routes/{mainmenu,garage,racehud,test}.luau`, `components/`).
  - Reactive UI framework: `NFMWorld.Library/data/library/sx/` (Sx)
  - Game backend: [NFMWorld.Library](../NFMWorld.Library/NFMWorld.Library.csproj)
  - Rendering and fonts: `NvgSharp/`, `FontStashSharp/` and `FNA/`.

- **Examples to follow:**
  - Adding native files: put in the right location under NFMWorld.NativeLibs to load via the DllImport resolver.
  - Adding compiled assets (shaders): add `.fx` to `CompileShader` ItemGroup so builders include shader compilation automatically.

- **Do NOT:**
  - Remove or flatten the MSBuild platform conditionals without testing on all OSes.
  - Change shader/tool expectations without keeping a non-Windows fallback path (`tools/fxc.exe` or documented wine steps).


If anything above is unclear or you want examples inserted for a specific task (adding a native plugin, publishing for Linux, or modifying shader flow), tell me which area to expand and I will update this file.

---

## Build, Run & CI

```bash
# Build entire solution
dotnet build nfm-world.slnx -c Debug

# Build single project
dotnet build nfm-world/NFMWorld.csproj

# Run
dotnet run --project nfm-world/NFMWorld.csproj

# Run all tests
dotnet test --no-build          # from solution root
dotnet test                     # from individual test project folder
```

**CI pipeline:** `dotnet restore` → `dotnet build` → `dotnet test`. On Linux/macOS, add conditional guards to ensure platform-specific MSBuild copy targets don't fail for missing Windows-only native files.

### Shaders

Shaders in `data/shaders/*.fx` are compiled to `.fxb` by the `BuildShaders` MSBuild target via `fxc.exe`. On non-Windows:
- Use `wine` + a Windows DirectX SDK `fxc.exe` (via `winetricks dxsdk_jun2010`), **or**
- Provide a `tools/fxc.exe` helper shim.

To add a new shader, add the `.fx` source to the `<CompileShader>` ItemGroup in `NFMWorld.csproj`. Do not manually copy `.fxb` files.

### UI (Luau/Yoga)

The UI is written in Luau and rendered by a Yoga layout host — there is **no separate frontend build step**. Views live in `data/uis/` and ship via the existing `data/**` copy rule. See the [Fine-Grained Reactive UI (Sx)](#fine-grained-reactive-ui-sx) section and `NFMWorld.Library/data/library/sx/GUIDE.md` for the framework and gotchas.

Yoga is configured with web defaults, so:
- flexDirection defaults to row
- alignContent defaults to stretch
- flexShrink defaults to 1
- position defaults to static

But:
- boxSizing defaults to border-box

### Source generator output (Reactor, legacy)

```bash
# Force regeneration of all source-generated files
Remove-Item -Recurse nfm-world/Generated
dotnet build nfm-world/NFMWorld.csproj
```

Generated files appear in `nfm-world/Generated/NFMWorld.Reactor.Generator/.../*.g.cs`. The csproj must have `<Compile Remove="Generated/**" />` to prevent double-compilation.

**Note:** The Reactor VDOM framework has been replaced by the Luau/Yoga UI. New UI work should target **Sx**.

---

## Gamemodes (Lua-driven)

Gamemodes are written in **Luau** and share one code path for singleplayer and multiplayer.

### Architecture

- **One race phase** — `nfm-world/Mad/Gameplay/RacePhase.cs` is the only in-race phase. It composes `RaceInputController` and `RaceCameraDirector` and talks to an `IRaceHost` (`NFMWorld.Library/Gamemodes/RaceHost/`):
  - `LocalRaceHost` — singleplayer: runs the factory's `IServerGamemode` in-process, so SP exercises the same client/server split as online play.
  - `NetworkRaceHost` (`nfm-world/Mad/Gameplay/RaceHost/`) — multiplayer: bridges an `IMultiplayerClientTransport` (join token, C2S/S2C packets) to the same interface.
- **Contracts** — `IGamemode` (client lifecycle/input/render/results) and `IServerGamemode` (server lifecycle + `IServerGamemodeData`), both in `NFMWorld.Library/Gamemodes/`.
- **Players as the single source of truth** — gamemodes own `ObservableUnlimitedArray<ClientSidePlayer>` (`Players`, each with an `IInGameCar? Car`). `ClientStage.SetPlayers(...)` observes collection + `CarChanged` events and creates one `CarVisual` per car. There is no separate `CarsInRace` array on the race phase (garage/menu phases still use `BaseStageRenderingPhase.CarsInRace` with `ClientStage.SetCars`).

### Lua framework (`NFMWorld.Library/Gamemodes/Lua/`)

- Scripts live at `data/gamemodes/{id}/client.luau` and `server.luau` (shipped via the existing `data/**` copy rule). `LuaGamemodeFactory` / `GamemodeRegistry.RegisterLua(id, path)` wire them up; `nfmm/racing|wasting|both` → `pvp/`, `nfmm/timetrial` → `timetrial/`.
- Globals injected into scripts: `stage` (`LuaStage`), `players` (`LuaPlayers` — generic `UnlimitedArray<T>` constructed types only get opaque Lua stubs, so use this wrapper), `hud` (`LuaHudState`, writes through to `HudStateData`), `physics` (`PhysicsController`), `time_trial` (`LuaTimeTrial` ghost/recording helper), `config` (JSON table from the factory), plus functions `create_car`, `drive`, `physics_tick`, `calculate_positions`, `handle_checkpoint`, `handle_fix_hoops`, `send_event`, `countdown_interval`, `client_index`, `attach_bot` (C# `ElStupido` for now), `reset_client_state`, `update_hud`, `add_ghost_player`, `remove_fake_players`. Server scripts get `server` (`LuaServerData`), `broadcast_event`, `finish_race`.
- Callback contract (invoked synchronously each tick): `on_begin`, `on_end`, `on_reset`, `on_game_tick`, `on_render`, `on_key_pressed(key)` / `on_key_released(key)` / `on_key_typed(char)` (keys passed as ints), `on_server_event(type, table)` / `on_client_event(playerId, type, table)` (server), `on_ai_tick(car, index)` (bots via `LuaBot`).
- Events between client and server are `LuaEventEnvelope { Type, JsonPayload }` (MemoryPack + JSON Lua table) — not MemoryPack unions. `LuaJson` handles Lua table ↔ JSON.
- `wait()`-style coroutine suspension is **not** supported yet — callbacks must be synchronous per-tick (countdown is tick-counted in Lua).

### Lua binding pipeline

- `[LuaVisible]` / `[LuaName]` / `[LuaHidden]` (in `NFMWorld.Lua`). Marking a class/struct LuaVisible makes the `NFMWorld.LuaSourceGenerator` emit a partial `T : ILuaUserData` with metatables, type tables, and `data/lua/library/*.lua` stubs — **declare such types `partial`**.
- Hidden ctor pattern: `[LuaHidden]` on constructors whose parameters the generator can't marshal.
- `LuaVisibleTypeRegistry.RegisterAll(state)` installs namespaces/types into a `LuaState`; call it after `OpenStandardLibraries()`.
- Cars cross to Lua via `LuaValue.FromUserData(car, LuaVisibleTypeMetatableRegistry<IInGameCar>.Metatable)` (see `LuaGamemode.ToLua`).
- Lua-CSharp fork: sync execution via `DoString`/`DoFile`/`Run` (throws `LuaYieldException` on suspension); `LuaNfmwPlatform` is VFS-backed.

### Gotchas

| Gotcha | Rule |
|---|---|
| New `[LuaVisible]` type | declare the class/struct `partial` |
| Ctor with unmarshalable params | `[LuaHidden]` on the constructor |
| `UnlimitedArray<T>` in Lua | opaque stub — wrap it (`LuaPlayers`) |
| Cars in Lua | use the `IInGameCar` metatable, not `FromObject` |
| `HudStateData` (DriverInterface) | never mark `[LuaVisible]` — use `LuaHudState` |
| Race finish broadcasts | guard with `_finished` / `ResultsBroadcasted` (done in both hosts) |
| TT preview/simulation | still C# (`TimeTrialPreviewGamemode`/`TimeTrialSimulationGamemode` derive from C# `TimeTrialGamemode`) |

---

## Lua Table Storage (`Lua-CSharp`)

`LuaTable` splits its storage three ways: a dense array part (integer keys near the array
length), an **embedded struct** `LuaStringDictionary` (string keys — the common
record-shaped case, so it costs no separate object per table), and a lazily created
`LuaValueDictionary` class (everything else, including sparse integers). Both dictionaries
use the same **signature-bucket** layout, ported from Faster.Map's `BlitzMap`:

- A bucket is one `ulong`: `next << 32 | (signature | slot)`. `signature = hash & ~mask`, so
  it is a multiple of the power-of-two bucket length and its low bits are free for the slot
  index. An empty bucket is all ones; `next == 0xFFFFFFFF` ends a chain.
- `FindValue` filters with `packed ^ signature <= mask`, which is an **exact** signature
  match (not probabilistic) because both signatures are mask-aligned. A mismatch therefore
  costs no entries-array access — that is the entire point of the layout.
- The bucket length is a power of two and at least 2, so a signature is always even and can
  never equal the all-ones empty marker. This is load-bearing: don't "simplify" the marker.
- Entries are compact in slot order and removal **swap-erases** the last slot into the hole,
  repairing the bucket that pointed at it. There is no free list and no stored hash.
- Iteration order is array part → string dictionary → generic dictionary, and is stable
  across a resize because slots are preserved.
- `T` (`hash & mask`) must be ≤ 0.8 of the bucket array so `FindEmptyBucket` always has slack;
  growth lands on exact powers of two (`maxCount = length / 2`).
- `Entry` is 48 bytes for string keys (vs 56 before) and 80 for `LuaValue` keys (vs 96), at
  the cost of a `ulong[]` bucket array. Net allocation is ~+4-7% (measured) for all three
  table shapes in `LuaTableBenchmark`.

### Lua table gotchas

| Gotcha | Rule |
|---|---|
| nil assignment | Stores a **dead entry** (keeps the key findable so `next` can continue from it) instead of removing it. Never change this to a real remove — `tests-lua/nextvar.lua` clears keys mid-`pairs` and expects every key to be visited. |
| Liveness | Counted, not derived: `LiveCount` scans slots. `Count` includes dead entries, so never use it as a "size". |
| Resizing re-hashes | Entries don't store their hash, so the bucket rebuild (and `Insert`'s owner check) re-invokes `GetHashCode` on stored keys. Keep stored-key hashing off the hot read/update path. |
| Sentinel safety | `signature != 0xFFFFFFFF` relies on the bucket length being a power of two ≥ 2. A length of 1 would make every live bucket look empty. |
| Hash quality | `double.GetHashCode()` leaves the low ~20 bits of small integers zero, so `hash & mask` is 0 for all of them and sparse numeric keys (`t[1000]`) collapse into one chain. Measured ~15x slower than string lookups. A hash-mixing step in `ComputeHash` would fix it if it ever matters. |
| Load factor | `_maxCount` must keep at least ~1/5 of the bucket array free, or `FindEmptyBucket` degrades to a long scan. |

---

## Fine-Grained Reactive UI (Sx)

A SolidJS/dom-expressions-style fine-grained reactivity UI framework for the Lua `UiLib`,
built to fix the per-frame re-render cost of preact-luau. It is a **sibling** to
preact-luau — new UI work should target Sx; preact-luau stays for unmigrated routes.

- **Where:** `NFMWorld.Library/data/library/sx/` (`signals.luau`, `host.luau`, `h.luau`,
  `dom.luau`, `styled.luau`, `index.luau`, `declarations.d.luau`, `GUIDE.md`). Ships via the existing `data/**` copy rule.
- **Beginner guide:** `NFMWorld.Library/data/library/sx/GUIDE.md` teaches the framework from
  zero (no web/signals/SolidJS assumed): mental model, `x`, `For`/`Show`/`Switch`, events,
  `styled`, the game bridge, and gotchas.
- **Public module `Sx`** (`index.luau`): `createSignal/createMemo/createEffect/createRoot/
  batch/untrack/onCleanup/onMount/getOwner/runWithOwner/setScheduler/flushSync`, the
  hyperscript `x`, `render`, `Fragment`, and flow components `Show/Switch/Match/For/Index`.
- **Core model** (port of Solid `reactive/signal.js`): signals are cells; memos are lazy
  pull computations (recompute on read when stale); effects are eager push computations.
  Reading a signal/memo during a computation subscribes it. Writing a signal marks
  observers STALE and propagates through memos, queueing effects. Effects run once per
  batched flush in dependency order; memos recompute lazily during effect execution.
- **Renderer** (`dom.luau`): components run **ONCE** under their own owner (`createRoot`)
  and return a descriptor built by `x`. Static children/props mount once; **function-valued
  children are installed as per-slot effects** — a dynamic text child updates via a single
  `commitTextUpdate` on its anchor node; a function-valued (non-`on`) prop updates via a
  single `setProperty`. Flow components use a hidden empty `TextNode` anchor as the slot
  terminator.
  **`Switch` mounts the selected `Match`'s children under a fresh isolated root** (not owned
  by the switch effect) and fast-paths out when the chosen Match descriptor is unchanged.
  This is required so a `Match.when` that reads a frequently-changing signal (e.g. a
  settings loading-state `when` reading `config()`) does NOT cause the whole subtree to
  remount on every write: without the isolated root, the switch effect's re-run would
  `cleanNode` (dispose) the mounted subtree's owned reactive effects and leave them dead,
  and without the fast path it would rebuild the entire chosen branch. Only a change of
  WHICH match is selected does structural work. (Same latent pattern applies to `Show` and
  `For`/`Index` — keep their `when`/`each` reads to rarely-changing signals.)
- **Events:** props with an `on` prefix are event handlers, wired **once** at
  `createInstance` (the host replaces, never `+=`) — they are static, not reactive
  accessors. A non-`on` function prop is treated as a **reactive accessor** (Solid
  gotcha) — name callbacks with an `on` prefix.
- **`styled(tag)(baseStyle)`** (`styled.luau`): CSS-in-JS wrapper returning a component
  that merges the base style + caller `style` override + a `hover` variant driven by a
  hover signal. Wires `onmouseenter`/`onmouseleave` ONLY when a `hover` variant exists.
- **Scheduler:** auto-wired at module load — `dom.luau` calls `Sx.setScheduler(Host.defer)`
  (→ `UiLib.defer` → `GameThreadContext.Post`), so a frame's signal writes coalesce into
  ONE end-of-frame effect pass (the game's per-frame batching). Tests use a synchronous
  fake `defer`; opt out with `Sx.setScheduler(nil)`. NOTE: navigation ordering (the page
  must mount before C# pushes phase data, e.g. `main-menu:account`) is NOT handled in Lua
  — do NOT add a Lua `flushSync` workaround; it is a pending C#-side fix.
- **Ports:** all views are Sx ports — `data/uis/router.luau` (route signal +
  `Sx.Switch`/`Match` + default-route fallback), `data/uis/routes/{mainmenu,garage,
  racehud,test}.luau`, and `data/uis/components/{glasscard,settings,pausemenu}.luau`.
  Shared primitives are reactivity-aware: `GlassCard`/`StatBar`/`CenterText` accept
  accessor props (color/value/text) and only re-set the affected node. `settings` uses
  `Fragment` for its tabs and a `config` GETTER passed to tab content (reactive reads).
- **Renderer perf:** the reactive-prop effect skips `setProperty` when the accessor
  returns the same reference as last time, and `GlassCard` caches its merged style by
  input reference — so changing one garage car's selection updates only that card.
- **Benchmark:** `bench/luau-benchmark/scripts/hud_sx.luau` mirrors `hud_render.luau`
  (17-node HUD, signals feed dynamic text + reactive bar widths). Scenario `hud_sx`.
  Expected: ~2 setProp + ~2 commitText per frame, 0 structural ops (vs preact 13–39
  setProps/f), and ~3–4x less wall time on the same machine.
- **Tests:** `Lua-CSharp/tests/Lua.Tests/SxReactiveTests.cs` (NUnit, loads the real
  `.luau` via memory FS + fake host) covers signal/memo/effect semantics, batch
  coalescing, root disposal, Show/Switch, For keyed single-row update (1 commit), the
  single-leaf HUD case (1 commit, 0 structural), and end-to-end port tests: router +
  mainmenu (account, PLAY/BACK, fallback), all routes (garage with collections/stats,
  race telemetry, test counter, back to main menu), and Settings loading state.
- **DevTools:** an in-game inspector overlay (`data/uis/components/devtools.luau`, mounted
  in `router.luau` behind the route Switch) shows a component/flow/host **tree** and a live
  **signals/memos/effects** list. It's backed by optional introspection in the sx core,
  all inert unless enabled:
  - `sx/signals.luau` keeps a `LiveNodes` registry of every signal + computation
    (`Sx.debug.list()`) and an injectable `Sx.debug.setOnChange(fn)` hook fired after any
    signal write that marks observers stale (used by auto-refresh).
  - `sx/dom.luau` builds a component tree during mount via `Dom.devtools` (`enable` /
    `snapshot`); each component/host/flow node records a name + children. Component names
    come from `debug.info(fn, "n")`. Lua-CSharp tracks declaration names on
    `Prototype.Name` (parser records them for `local function X()`, `function X()`, and
    `local X = function()`) and exposes them via the Luau-style `debug.info(fn, "n")`
    (added to `DebugLibrary`; `debug.getinfo` stays conformant). `devName` falls back to
    `debug.getinfo(fn)` → `short_src:linedefined` for unnamed functions. Styled components
    are anonymous closures, so `styled.luau` registers a readable `Styled<tag>` name (e.g.
    `StyledView`) via `Sx.debug.setStyledName`; `devName` checks `Sx.debug.styledName(fn)`
    first (weak-keyed registry — Lua-CSharp functions can't hold properties).
  - `devName` prefers the **creation call site** over the declaration site: `x` (in
    `h.luau`) records the `short_src:line` of each `x(...){...}` call onto the descriptor
    (`desc.callSite`) when capture is on, and `Dom.devtools` toggles it through
    `h.setCaptureCallSites` (wired in `enable`/`disable`). `captureCallSite()` (h.luau)
    walks `debug.traceback("", 2)` and returns the FIRST frame not inside the Sx library
    (path contains `sx/`) — so it resolves to the user's source even when flow/effect
    machinery (`For`/`Show`/`Switch`, signals, `styled`) sits between the render function
    and the `x` call (a `<For>` row fn lives in the user's module, not dom.luau).
    `captureCallSite` is called in the **outer** `x(vtype)` builder, NOT the returned
    function: `x(ItemBtn){...}` is `x(ItemBtn)({...})`, so `x(ItemBtn)` is never in a tail
    position and its caller frame survives — whereas capturing in the returned builder
    would lose a `<For>` row's frame to tail-call elimination (a row's `return
    x(ItemBtn){...}` resolved to the mount line instead of the user's row fn).
    `mountComponent` passes `descriptor.callSite` to `devName`, so each tree node shows
    where its instance was created in the source, falling back to the declaration site (or
    `"anonymous"`) when there is no captured call site (e.g. the root mounted via `render`).
  - `Sx.devtools` / `Sx.debug` are exported from `sx/index.luau`.
  - The pane re-snapshots only when the tree's structural signature changes (preserves
    expand/collapse across value-only updates); auto-refresh is opt-in and guarded against
    feedback loops (a `refreshing` flag suppresses the hook for its own writes).

### Sx gotchas

| Gotcha | Rule |
|---|---|
| Reactive text children | Must be accessor functions: `x(Text){ () => item().label }`, not `x(Text){ item.label }` (plain reads don't update). |
| `For`/`Index` | Solid semantics: `For` keys by value identity, `Index` by position. No per-element `key` prop. |
| Non-`on` function props | Treated as reactive accessors (Solid gotcha). Use `on`-prefixed names for callbacks. |
| `and/or` falsy trap | `(type(w)=="function") and w() or w` returns `w` when `w()` is `false`/`nil`. Use an explicit `if`. |
| Memos must store values | `updateMemo` assigns `node.value = result`; a memo whose result is discarded returns `nil` forever. |
| Flow `when`/`each` reading hot signals | `Switch` mounts its chosen `Match` under an isolated root and fast-paths when the chosen descriptor is unchanged, so a `Match.when` reading a frequently-changing signal (e.g. settings `config()`) won't remount the subtree. Keep `Show`/`For`/`Index` `when`/`each` reads to rarely-changing signals — they lack the isolated-root fast path. |
| Empty `TextNode` anchors | `Show`/`Switch`/`For`/dynamic slots use an invisible empty `TextNode` as the insertion anchor. These are direct children of Views, interleaved with Components. The host's `insertBefore` must map the all-children index to the Component-only Yoga index (`ComponentChildCollection.InsertItem`) or `YogaNode.InsertChild` throws `ArgumentOutOfRangeException`. |
| Lua 5.3 `%d` | `("%d%%"):format(v * 100)` errors ("number has no integer representation") for non-exact floats like `0.8*100`. Always `math.floor(v * 100 + 0.5)` first. |

---

## Shader Pipeline (HLSL / SPIR-V)

Shaders live in `data/shaders/*.fx` and are compiled to `.fxb` by the `BuildShaders` MSBuild target via `fxc.exe`. The `ShaderSourceGen` Roslyn source generator additionally wraps compiled shaders and emits C# binding code.

**`ShaderSourceGen` naming:** generated C# shader wrapper files use a deterministic naming convention based on shader entry point and target profile. Do not rename shaders without updating all downstream C# references.

---

## Virtual File System (Maxine.VFS)

Provides a path-abstraction layer decoupling game code from the real filesystem.

**Key types:**

| Type | Role |
|---|---|
| `IPath` | Abstract path interface |
| `MemoryPath` | In-memory path implementation |
| `IoPath` | Wraps real filesystem paths (internal) |
| `FallbackFileSystem` | Chains multiple `ReadOnlyFileSystem` implementations, trying each in order |

**Tested behaviours (MSTest):**
- `GetFullPath` resolves `..` segments correctly.
- `Combine` handles absolute path override on both Windows (`C:\...`) and Unix (`/...`).
- Path normalization converts `\` to `/`.
- `FallbackFileSystem` falls through on `FileNotFoundException` (in `OpenRead`/`GetAttributes`) and `DirectoryNotFoundException` (in `EnumerateFiles`/`EnumerateDirectories`). Other IO exceptions propagate immediately.

---

## FixedMath / Fixed-Point Math

`FixedMathSharp` provides fixed-point arithmetic for deterministic simulation:
- `Fixed4x4` — 4×4 transformation matrix
- Various `Fixed*` scalar and vector types

Fixed → float conversions are **lossy** by design. Never use `==` between fixed-point and float values; use epsilon tolerance in tests.

---

## Testing Infrastructure

- **Test framework:** MSTest (`[TestClass]`, `[TestMethod]`, `Assert.AreEqual`, `Assert.ThrowsException<T>`, `Assert.IsNotNull`). The project was converted from NUnit. **Never use NUnit APIs** (`[Test]`, `[TestFixture]`, `Assert.That`, `Assert.Throws`, etc.).
- **Test runner:** `dotnet test` from the solution root or individual test project folder.
- **Test projects:** `NFMWorld.Reactor.Test`, `NFMWorld.Library.Test`, `Maxine.VFS.Test`, `Maxine.Extensions.Test`, `HoleyDiver.UnitTest`.

**MSTest pattern:**

```csharp
[TestClass]
public class SomeTests {
    [TestMethod]
    public void MethodName_Scenario_ExpectedBehavior() {
        // Arrange
        var sut = new SystemUnderTest();

        // Act
        var result = sut.DoThing();

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Method_InvalidInput_Throws() { ... }
}
```

**Coverage priorities:**
- VFS path operations (`GetFullPath`, `Combine`, normalization, `FallbackFileSystem`)
- Lua generator output correctness (method table structure, metamethods, constructor presence/absence, operator overloads, InlineArray indexers)
- FixedMath conversion accuracy
- Polygon triangulation correctness (HoleyDiver)

---

## Lessons Learned by Subsystem

### Shader Pipeline

**L1 — Do not rename shaders without updating downstream C# references.** `ShaderSourceGen` generates wrapper classes with deterministic names based on shader entry points.

### VFS / Path Handling

**L1 — Replicate `Path.Combine` semantics exactly.** An absolute path on the right-hand side must discard the left-hand side. Test with both Windows and Unix absolute paths.

**L2 — Always normalize `\` to `/`.** Code consuming VFS paths must not assume OS-native separators.

**L3 — `FallbackFileSystem` falls through on `FileNotFoundException` (in `OpenRead`/`GetAttributes`) and `DirectoryNotFoundException` (in enumerate methods).** Other IO exceptions propagate immediately. Test this boundary explicitly.

### FixedMath

**L1 — Fixed → float is lossy. Never use `==`.** Use epsilon-based comparison in all tests.

**L2 — `FixedMathSharp` updates break dependent projects.** Run all downstream test suites after any bump.

### Polygon Triangulation (HoleyDiver)

The `HoleyDiver` project (`HoleyDiver/Program.cs`) provides a robust polygon triangulator for non-planar 3D n-gons with holes defined by self-intersecting paths. It uses **Poly2Tri** (constrained Delaunay) as the primary triangulator with an ear-cut fallback.

**Pipeline overview:**

1. **Best-fit plane projection** — Compute centroid + covariance matrix → eigenvector for plane normal. Project 3D vertices to 2D via `GetProjectionBasis`. Falls back to axis-aligned projections (XY/XZ/YZ) if the best-fit plane collapses distinct 3D points.
2. **Vertex deduplication** — Merge vertices within epsilon (`1e-5`) using `Vector2.Distance`. Build an `indexMap` from original indices → unique indices.
3. **Region extraction** (`ExtractRegions`) — Detects holes in self-intersecting paths by finding mirrored vertex sequences. Ported from a Python reference algorithm.
4. **Outer boundary reconstruction** — After `ExtractRegions`, the outer polygon is reconstructed from the original path by **excluding all hole vertices** (in path order). This is critical: do NOT use convex hull as it strips concave features.
5. **Bridge vertex filtering** — Vertices shared between outer and holes (bridge points from self-intersection) are removed from hole definitions to avoid duplicate constraints in Poly2Tri.
6. **Poly2Tri triangulation** — Outer polygon + cleaned holes passed as `Polygon` constraints. Triangles mapped back through unique→original indices.
7. **Incomplete triangle filtering** — Only triangles with all 3 vertices successfully mapped to original indices are emitted. Poly2Tri may produce degenerate triangles when hole vertices share edges with the outer boundary.

**Key types / entry point:**

| Type | Role |
|---|---|
| `PolygonTriangulator.Triangulate(IReadOnlyList<Vector3>)` | Main entry — returns `TriangulationResult` with `Triangles`, `PlaneNormal`, `Centroid`, `RegionCount` |
| `ExtractRegions(List<int>, List<Vector2>)` | Mirrored-sequence hole detection; returns list of poly-lines with holes marked by `-1` prefix |
| `Poly2Tri.Polygon` / `DTSweepContext` | Constrained Delaunay triangulation |

**Lessons learned:**

**L1 — Convex hull destroys concave features. Never use it as an outer boundary replacement.**
The Graham scan convex hull was initially used to "fix" incomplete outer boundaries from `ExtractRegions`. This silently removed concave indentations (e.g., the bottom indentation of a car rear panel: vertices `(19,-43)`, `(0,-45)`, `(-19,-43)` were dropped). The correct outer boundary **must** be reconstructed from the original path by filtering out hole vertices while preserving path order.

**L2 — `ExtractRegions` may produce incomplete outer boundaries. Always validate and reconstruct.**
The mirrored-sequence detection algorithm can leave the outer polygon with only a subset of vertices. The workaround: collect all vertices belonging to non-outer regions (holes), then rebuild `polyLines[0]` as `initialPoly \ holeVertices` (in original path order, with deduplication).

**L3 — Poly2Tri requires clean hole definitions. Filter bridge vertices.**
Self-intersecting path holes share "bridge" vertices with the outer polygon (the points where the path crosses itself). These must be removed from hole vertex lists before passing to Poly2Tri, otherwise the triangulator sees duplicate constraints and may fail or produce degenerate output.

**L4 — Map triangles through unique→original indices carefully. Reject incomplete ones.**
Poly2Tri works with the deduplicated unique vertex set. Each triangle vertex must be mapped: `PolygonPoint` → `uniqueVertices` index → `indexMap` → original 3D vertex index. If any of the 3 vertices fails to map (e.g., Poly2Tri created a triangle using a point not in the original set), **reject the entire triangle**. Without this filter, the triangle count can be non-integer.

**L5 — The Python hole-finding algorithm was ported directly.**
The `ExtractRegions` method is a direct C# translation of a Python reference implementation. It finds mirrored sequences in a self-intersecting path by testing all `(i,j)` pairs, walking forward from `i` and backward from `j`, measuring the length of matching vertices. When `le == 1` (mirror length 1), it checks containment via `AllPointsInPolygon` to decide whether to swap outer/hole roles. The algorithm requires `polyLines[0].Count >= 6` to continue (minimum path for a hole).

**L6 — Polygon winding direction matters for Poly2Tri but is handled automatically.**
Poly2Tri's `Polygon` constructor and `AddHole` handle winding internally. Do NOT manually reverse hole winding before passing to Poly2Tri — the library expects holes in their natural winding and will reverse them if needed.

**L7 — Best-fit plane fallback is essential for near-planar or degenerate input.**
When the covariance matrix produces a near-zero normal (length < `1e-10`), fall back to Newell's method (sum of cross products of adjacent edges). If that also fails, default to `Vector3.UnitZ`. The projection validator also checks that no two 3D points collapse to the same 2D point under the chosen projection.

**L8 — Do NOT add "safety" guards to the mirrored-sequence walker.**
The Python algorithm walks `while k0 != k1 && poly[0][k0] == poly[0][k1]`. An earlier C# implementation added `maxMatchIterations` bounds and extra `break` conditions (nextK0 == k1, k0 == nextK1) that diverged from the reference, causing subtle mismatches. The walker naturally terminates because `k0` advances forward and `k1` advances backward — they either meet or mismatch. Trust the reference algorithm.

**L9 — The `le == 1` containment check differs from `le > 1`.**
When the mirrored sequence length is exactly 1, the Python algorithm checks if all points of `polyLines[0]` are inside the new region (`AllPointsInPolygon(points0, pointsNew)`). If so, it swaps them (the new region becomes the outer). The old C# code also required `!newInsidePoly0` (an "only one contains" check), which was wrong. For `le > 1`, no containment check is performed — the new region is always treated as a hole.

**L10 — The `CombineWithHoles` / ear-cut path is fallback-only.**
`CombineWithHoles` (bridge-based hole merging for ear-cut) and `EarCutTriangulateSimple` are the fallback path used only when Poly2Tri throws. They are NOT exercised during normal operation. Changes to the primary pipeline should focus on `ExtractRegions` + Poly2Tri.

**L11 — Test polygons are embedded in `Main()`.**
Two test cases exist in `Program.Main()`: (1) a windshield-shaped polygon with 1 rectangular hole (19 vertices, planar Z≈207.4), (2) a car rear panel with 2 holes and concave bottom indentation (19 vertices, near-planar Z≈-103). Swap between them by commenting/uncommenting vertex blocks. Validate with `dotnet run 2>$null | Select-String -Pattern 'Plane|Regions|Triangles:'`.

**Common gotchas:**

| Gotcha | Rule |
|---|---|
| Convex hull for outer boundary | **Never** — reconstruct from original path minus hole vertices |
| Duplicate vertices | Deduplicate with epsilon before region extraction |
| Bridge vertices in holes | Filter out vertices that also appear in outer polygon |
| Poly2Tri degenerate triangles | Check `triIndices.Count == 3` before emitting |
| Hole marker convention | Holes are prefixed with `-1` in the poly-lines list |
| Ear-cut fallback | Only used when Poly2Tri throws; filters triangles by centroid-in-hole test |
| Safety guards in walker | Do NOT add `maxMatchIterations` or extra `break` conditions — trust the reference algorithm |
| `le == 1` containment check | Check only `AllPointsInPolygon(points0, pointsNew)` — not both directions |
| Test polygon swapping | Comment/uncomment vertex blocks in `Main()`; validate with `dotnet run` |

---

## Agent Working Guidelines

### Before starting any task

- Identify which subsystem(s) are involved and re-read the relevant section of this document.

### While working

1. **Set up a todo list for multi-step tasks.** The codebase is complex enough that losing track mid-task causes compounding errors.
2. **Verify generated output, not just build success.** After any source generator change, read the corresponding `.g.cs` file and confirm the emitted C# is structurally correct.
3. **Run the full test suite for the affected project** — many edge cases have dedicated tests (e.g., `NFMWorld.Library.Test`, `Maxine.VFS.Test`).
4. **Never delete a test.** If an interface changed, update the test to match the new contract.

### After completing a task

5. Ensure all tests pass in affected projects.
6. If you introduced or significantly changed a subsystem, update the relevant section of this document.

### Do NOT

- Remove or flatten the MSBuild platform conditionals without testing on all OSes.
- Change shader/tool expectations without keeping a non-Windows fallback (`tools/fxc.exe` or documented wine steps).
- Use NUnit APIs — the project uses MSTest.
- Rely on OS-native path separators anywhere in game or test code — use VFS normalization.

### Common gotchas at a glance

| Gotcha | Rule |
|---|---|
| Test framework | MSTest only — no `Assert.That`, `[Test]`, `[TestFixture]` |
| Phase bridge cleanup | Always call `Unregister()` in `Phase.Exit` |
| Source gen output | Check `nfm-world/Generated/` — do not trust a clean build alone |

---