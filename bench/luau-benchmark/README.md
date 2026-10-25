# LuauBenchmark — Lua VM benchmark (lua-csharp vs NuLua/Luau)

A standalone benchmark that runs the **same VM-agnostic Luau workloads** under two Lua VMs so they
can be compared apples-to-apples:

- **This branch**: the Lua-CSharp VM (`Lua-CSharp/src/Lua`), driven via the real game UI host.
- **The other (Luau) branch**: a fork that swaps the VM glue to `NuLua.Luau` and runs the
  **identical** `.luau` scripts.

It runs headlessly — no FNA/MonoGame rendering. The UI uses the real `LuaUiLibrary` C# host and a
`DummyBackend` so no graphics/input device is touched.

Measurement is **BenchmarkDotNet** (wall-clock statistics, outlier detection, allocations). For an
invocation this prints its own numbers — the in-Lua `os.clock()` delta and the host-call counters —
which BenchmarkDotNet shows as extra columns (`Lua CPU`, `CPU RSD`, `Host ops`).

## Benchmarks

| Case | Script | Work per invocation | `OperationsPerInvoke` (the "op") |
|---|---|---|---|
| `PreactSmallFresh` | `preact_render.luau` | 400 re-renders of a 16-node tree, fresh style/text tables each render | 1 render |
| `PreactSmallStable` | `preact_render.luau` | 2000 re-renders, module-constant style/text refs | 1 render |
| `PreactLargeFresh` | `preact_render.luau` | 20 re-renders of a 1024-node tree, fresh props | 1 render |
| `PreactLargeStable` | `preact_render.luau` | 6 re-renders of a 1024-node tree, stable props | 1 render |
| `HudBenchmark` | `hud_render.luau` | 100 per-frame flushes of the timetrial HUD (17 nodes, 3 dirty components), `[Params] FreshProps=true/false` | 1 frame |
| `HudSxBenchmark` | `hud_sx.luau` | 600 per-frame flushes of the same HUD built with Sx (leaf updates only) | 1 frame |
| `VmcoreBenchmark` | `vmcore.luau` | 1200 reconciler-core walks at depth 8, no C# host at all | 1 walk |
| `Fixed64Benchmark` | `fixed64_kernel.luau` | 2000 min-distance scans over 500 nodes + trig | 1 iteration |

The two prop regimes are what separate the bottlenecks: **fresh props** make preact's `diffProps`
fire a host `setProperty`/`commitTextUpdate` per node, so the loop is C#-interop-bound; **stable
props** find equal references and skip the host entirely, so it is bound by the pure-Lua reconciler
walk. Both `freshProps` combinations are now measured for both tree sizes, including the two
(`small`/`stable`, `large`/`fresh`) that the older hard-coded scenarios never covered.

Counts are single named constants on each benchmark class. They are deliberately **shortened** from
the older sizes (`10_000` and `100`) so that one invocation takes ~0.2–1 s, which is what a
BenchmarkDotNet iteration with one invocation needs. Because `OperationsPerInvoke` divides by the
loop count, the reported per-op figures stay comparable with the old totals. The fidelity cost is
that each script mounts its tree **once per `run()`** and then updates it in place, so the one-time
mount is a larger share of a short invocation — material only for `PreactLargeFresh`/`Stable`
(turbo ~20 % of the invocation at 6–20 renders, versus ~2 % at 100). The `Lua CPU` column excludes
the mount entirely (the script starts `os.clock()` after it), so compare per-op CPU there and read
the wall-clock `Mean` with the mount in mind.

## Build & run

```bash
# Build
dotnet build bench/luau-benchmark/LuauBenchmark.csproj -c Release

# List every case (with its parameters)
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- --list flat

# Run everything  (WarmupCount 3, IterationCount 5 per case, one process per case)
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release

# Filter to one case / one group
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- --filter *Vmcore*
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- --filter *Hud*

# Faster/slower run: BenchmarkDotNet's own switches
... -- --warmupCount 1 --iterationCount 3
... -- --join            # one process for all cases instead of one per case
```

Summaries, logs and the generated project land in `BenchmarkDotNet.Artifacts/` (git-ignored);
`BenchmarkDotNet.Artifacts/results/` has the CSV/Markdown/HTML versions of the table. The first run
of a session also builds a generated project that references this one, which takes a few minutes;
later runs are incremental. **Run with `-c Release`** — BenchmarkDotNet refuses Debug builds.

### Columns

| Column | Meaning |
|---|---|
| `Mean`/`Error`/`StdDev` | BenchmarkDotNet's wall-clock statistics, per invocation (and per op, via `OperationsPerInvoke`). |
| `Gen0`/`Allocated` | `MemoryDiagnoser`: allocations during the measured invocation. |
| `Lua CPU` | Mean **in-Lua `os.clock()` delta per op**: process CPU time measured inside the script. This is the fair cross-VM metric — it excludes time the process spent descheduled, which is why it is the one to compare across VMs and across a busy machine. |
| `CPU RSD` | Relative standard deviation of that CPU figure across the measured iterations. Near zero means every iteration did the same work; a figure that climbs means state is accumulating between iterations. |
| `Host ops` | `LuaUiHostStats` per op: `setProp` / `commit` / `create` / `struct` calls. Only the HUD cases record these (non-HUD rows show `NA`). |

`os.clock` is process-wide CPU time, so it also counts background GC threads' work; treat `Lua CPU`
as "CPU the process burned while the VM ran this workload", not as "CPU spent inside the interpreter".

### Legacy console scenarios

`Program.cs` also keeps the original console scenarios, for the things a BenchmarkDotNet table does
not do — allocation *type* traces, the `profile-*` breakdowns, the `fixed64` checksum:

```bash
# fixed64 | preact-small | preact-large | hud | hud_sx | vmcore | all
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- vmcore 3
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- profile-hud-sx
dotnet run --project bench/luau-benchmark/LuauBenchmark.csproj -c Release -- trace-preact-large
```

Their numbers are `best of N` on a single long-lived `LuaState` and are **not** stationary — see the
diagnosis below — so treat them as diagnostics, not as measurements. Anything that is not one of
those scenario names is passed straight to BenchmarkDotNet, which is the measurement path.

`fixed64` (in both modes) also prints a **checksum** (a `tostring(fixed64)` sentinel) that must
match across VMs — it guards against arithmetic divergence between Lua-CSharp and Luau. It used to be
an empty stub that measured nothing; it now actually runs.

## Why one fresh `LuaState` per iteration

The old harness was "extremely noisy run to run", and the dominant cause was not machine noise: it
reused a single `LuaState` for its whole `best of N` loop and for every scenario in the process.
Each `run()` mounts a new tree with `UiLib.createRoot()` + `React.render(...)` **without unmounting
the previous one**, so the old tree's `useEffect` handlers stay subscribed to `UiLib.onEvent` forever
(`scripts/hud_sx.luau` works around this by hand with `unsubHudState()`), and `PushEvent` dispatches
to *every* registered handler. Run N therefore drove all N−1 stale trees as well: in one `hud 1`
process the second regime reported 6 component types rendering 300× each — its own three
(`PowerDamageBars`, `Splits`, `Speed`) plus three more as `function:<id>` closures from the reloaded
chunk. `best of N` always ended up picking run 1, and any scenario measured after another in the same
process was systematically inflated. **Recorded numbers from the old harness are polluted by this;
they should not be compared with new ones.**

Two further causes, both fixed here: `best of N` was a crude substitute for JIT warm-up (the current
config pins `WarmupCount` and `IterationCount` explicitly), and one long-lived process carried heap
and finalizer state from scenario to scenario (now one process per case, and one forced full GC per
iteration).

So: `[IterationSetup]` builds a fresh `BenchmarkHost` (fresh `LuaState`, fresh preact/Sx modules) and
runs a forced, blocking, compacting GC; `[IterationCleanup]` disposes it. `InvocationCount` and
`UnrollFactor` are pinned to 1 so exactly one `run()` call is measured per iteration — if they were
left to BenchmarkDotNet's pilot stage, several calls would share one host and the accumulation would
come back. The shared runner warns loudly on stderr if the body ever runs twice without an
intervening setup, so that cannot regress silently.

## Layout

```
bench/luau-benchmark/
├── LuauBenchmark.csproj      # net10.0 console; refs NFMWorld.Library + Lua-CSharp + BenchmarkDotNet
├── Program.cs                # CLI dispatch: BenchmarkDotNet args, or a legacy console scenario
├── BenchSupport.cs           # shared paths (repo root, scripts) + quiet-logging module initializer
├── BenchConfig.cs            # the BenchmarkDotNet job: warm-up/iteration/invocation counts
├── Benchmarks.cs             # the benchmark classes + the per-iteration host lifecycle
├── BenchMetrics.cs           # in-Lua CPU + host-counter collection, and the stdout transport
├── BenchColumns.cs           # the Lua CPU / CPU RSD / Host ops columns
├── BenchmarkHost.cs          # ★ THE VM-SPECIFIC GLUE (fork this to NuLua)
├── scripts/                  # VM-agnostic .luau — SHARED unchanged across branches
│   ├── fixed64_kernel.luau
│   ├── preact_render.luau
│   ├── hud_render.luau
│   ├── hud_sx.luau
│   └── vmcore.luau
└── README.md
```

### How the custom columns work

BenchmarkDotNet runs the benchmark body in a child process, and for diagnosers it calls
`IDiagnoser.ProcessResults` in the **host** process with only the data it ferried across itself
(measurements, GC/threading stats) — there is no channel there for a scalar the body computed, and a
static filled in by the child is empty in the host. What *is* available host-side is the child's
standard output (`ExecuteResult.StandardOutput`, reachable as
`summary[benchmarkCase].ExecuteResults`). So the body prints one `// LUAU_BENCH cpuNs=… …` line per
iteration from `[IterationCleanup]` — outside the measured region — and `BenchColumns` parses those
lines back and reports the mean (and spread) per case. No `IMetricDescriptor`/metric plumbing is
involved, and the numbers land in the CSV/Markdown/HTML summaries like any other column.

## How the host works

`BenchmarkHost` mirrors the real game wiring (`nfm-world/UI/UiRenderer.cs`), minus rendering:

1. Create a Lua-CSharp `LuaState` (`LuaPlatform` with `RequireByString=true`, `SystemOsEnvironment`
   for `os.clock`, `unpack` global, `LuaVisibleTypeRegistry.RegisterAll`).
2. `GameThreadContext.Install()` + `IBackend.Backend = new DummyBackend()` → headless.
3. `LuaUiLibrary.Register(state, setActiveRoot, call, onEvent)` — registers the **real** C# UI
   host (`createInstance`/`setProperty`/… → `View`/`Component`/Yoga).
4. Load the real `react.luau` (preact-luau). `preact-luau/src/ui.luau` captures `_G.UiLib` at module
   load, so the host is registered **before** `react.luau` is loaded.
5. Load `sx/index.luau` for the Sx scenarios, and register the `__bench_push` / `__bench_flush` /
   `__bench_reset_stats` globals the HUD scripts drive the deferred flush through.
6. `LibraryFileSystem` (an `ILuaFileSystem`) roots the `./`-relative preact-luau require graph at
   `NFMWorld.Library/data/library` on the real filesystem — no VFS mount needed.

Benchmark scripts are plain chunks that `return run(...)`; the host loads them, calls them with
args, and reads back the `os.clock()` delta. Script paths are resolved by walking up from the
executing assembly to the repo root, which also works from BenchmarkDotNet's generated-project
folder.

## Forking to the real Luau VM (NuLua)

The `.luau` scripts are intentionally VM-agnostic (no table destructuring, no Luau-only syntax) and
must stay byte-identical between branches. To compare against the real Luau VM, rewrite **only** the
VM glue:

1. **`BenchmarkHost.cs`** — swap `LuaState`/`LuaPlatform`/`LuaVisibleTypeRegistry` (Lua-CSharp) for
   the `NuLua.Luau` API: `LuauState.Create()` / `OpenLibraries()`, `state["X"] = …`, `DoString`,
   `LuaValue.FromPrimitive(id, v)` (id 0 = Fixed64) for the fixed64 type, `state.CreateUserData`.
   The `fixed64(...)` ctor and `f64math.*` globals must be registered in NuLua (the game's Lua
   runtime migration handles this). The `LuaUiLibrary` C# host also needs a NuLua counterpart.
2. **`LuauBenchmark.csproj`** — the `Lua-CSharp/src/Lua` `ProjectReference` is the one the fork swaps.
3. Everything else (`Program.cs`, `BenchConfig.cs`, `Benchmarks.cs`, `BenchMetrics.cs`,
   `BenchColumns.cs`, the scripts) is VM-agnostic; only fix the host type where `Benchmarks.cs`
   constructs it.
4. `scripts/` — leave **unchanged**.

`IBackend.Backend = new DummyBackend()`, `GameThreadContext.Install()`, and the `LuaUiLibrary`
stand-in delegates carry over unchanged (they're VM-agnostic C#).

## Notes

- **Quiet logging**: the game logs every `setProperty`/`commitTextUpdate` at `Debug` (floods stdout
  and skews timing). The level is raised to `Warning` before the host is created — from a
  `[ModuleInitializer]` in `BenchSupport.cs`, because BenchmarkDotNet never runs this project's
  `Main` in its measured child process, and from the top of `Program.cs` for the console path. This
  is backed by a small, backwards-compatible env-var override in `NFMWorld.Library/Logging.cs`
  (defaults to the previous Trace-in-Debug / Debug-in-Release behaviour when unset).
- **No new `CefBrowser`/UI phase** is created; this is a plain console process.
- Only `os.clock`-compatible runtimes can share the timing contract — both Lua-CSharp and Luau
  expose `os.clock`, so CPU numbers are directly comparable.
- `vmcore.luau`'s third parameter (`freshProps`) is inert — it reads it as
  `freshProps ~= nil and freshProps or true`, i.e. always true — so `VmcoreBenchmark` has no
  `[Params]` on it and the script is left as-is.
- `preact_render.luau`'s mount is inside the invocation but outside its `os.clock()` window, so
  `Lua CPU` reflects the re-renders only; see the note under the case table.
