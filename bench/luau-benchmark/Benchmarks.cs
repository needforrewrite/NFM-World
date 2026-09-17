using BenchmarkDotNet.Attributes;
using Lua;
using NFMWorldLibrary.Util;

namespace LuauBenchmark;

/// <summary>
/// Shared plumbing for every case: one virgin <see cref="BenchmarkHost"/> per measured iteration.
///
/// That is the whole point of the port. The old harness kept a single <c>LuaState</c> for its
/// best-of-N loop and for every scenario in the process, and each <c>run()</c> mounts a new tree
/// without unmounting the previous one — the old tree's <c>useEffect</c> handlers stay subscribed
/// to <c>UiLib.onEvent</c> forever (see <c>scripts/hud_sx.luau</c>, which works around it by hand),
/// so <c>PushEvent</c> drove every stale tree and run N did roughly N× the listener work. A fresh
/// host — fresh LuaState, fresh preact/Sx modules — per iteration makes every invocation stationary,
/// and a forced full GC before it means the previous iteration's garbage is not collected inside
/// the measured region.
///
/// Derived classes only supply the script, its arguments, and how much work one invocation is.
/// </summary>
public abstract class LuaScenarioBenchmark
{
    BenchmarkHost? host;
    bool ranSinceSetup;

    /// <summary>
    /// Whether this scenario's script reports work through <see cref="LuaUiHostStats"/> (the HUD
    /// cases do; the pure-VM ones do not). Turning it on costs one branch per host call, so it is
    /// left off where there is nothing to count.
    /// </summary>
    protected virtual bool UsesHostStats => false;

    [IterationSetup]
    public void IterationSetup()
    {
        // Set here rather than in a [GlobalSetup]: with a joined run (--join) BenchmarkDotNet runs
        // every benchmark's global setup before the first case, which would leave this flag at
        // whatever the last class set and silently report zero host ops for the HUD cases.
        LuaUiHostStats.Enabled = UsesHostStats;

        host = new BenchmarkHost(BenchSupport.LibraryRoot);

        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);

        ranSinceSetup = false;
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        // Outside the measured region (BDN stops the clock before the cleanup), so printing here
        // costs the iteration nothing. The host-side columns read these lines back out of the
        // captured stdout -- see BenchMetrics.Emit.
        BenchMetrics.Emit();

        host?.Dispose();
        host = null;
    }

    /// <summary>
    /// One measured invocation: runs the script and files its numbers as BDN metrics.
    /// <paramref name="operationsPerInvoke"/> is the script's inner-loop count, which is what turns
    /// the totals into per-op figures — it must match the <c>OperationsPerInvoke</c> on the
    /// <c>[Benchmark]</c> so the CPU column and the wall-clock columns are per the same unit.
    /// </summary>
    protected void Run(string script, int operationsPerInvoke, params LuaValue[] args)
    {
        var h = host ?? throw new InvalidOperationException(
            $"{nameof(IterationSetup)} did not run before the benchmark body");

        if (ranSinceSetup)
        {
            // On stdout rather than stderr: BenchmarkDotNet captures the child's stdout and echoes
            // it into the run log, but leaves stderr attached to the console
            // (RedirectStandardError=false), so stderr would never reach the log. The "//" prefix
            // puts this in the prefixed-lines channel, which BenchmarkDotNet ignores when it does
            // not recognise the tag — a plain line would be parsed as a measurement instead.
            Console.WriteLine(
                $"// LUAU_BENCH-GUARD {GetType().Name}: the body ran twice without an intervening "
                + "IterationSetup, so this invocation is running on a host whose previous tree is "
                + "still mounted and still subscribed to events. Keep InvocationCount/UnrollFactor "
                + "at 1 in BenchConfig.");
        }
        ranSinceSetup = true;

        BenchMetrics.Reset();
        LuaUiHostStats.Reset();

        var (cpuSeconds, _, _) = h.RunScript(BenchSupport.Script(script), args);

        var ops = Math.Max(operationsPerInvoke, 1);
        BenchMetrics.Record(BenchMetrics.Keys.CpuNs, cpuSeconds * 1e9 / ops);
        if (UsesHostStats)
        {
            BenchMetrics.RecordHostStats(ops);
        }
    }
}

/// <summary>
/// 16-node tree, freshly allocated style tables and text every render, so preact's diffProps
/// fires a host call per node: the C#-interop-bound regime.
/// </summary>
[Config(typeof(BenchConfig))]
public class PreactSmallFreshBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 1400 re-renders ≈ 1.0 s/invocation: F = 5.0 ms fixed (the script's mount and three warm-up
    /// frames) + 1400 x 717 us per render. Calibrated in a single run — see the README's
    /// "Calibrating the counts" for why a per-op figure from another run cannot be used for this.
    /// </summary>
    const int Renders = 1400;
    const int Size = 16;

    [Benchmark(Description = "preact-small, fresh props (16-node tree)", OperationsPerInvoke = Renders)]
    public void Render() => Run("preact_render.luau", Renders, new LuaValue((double)Renders), new LuaValue((double)Size), new LuaValue(true));
}

/// <summary>
/// The same 16-node tree with module-constant style/text references, so diffProps finds equal
/// references and makes ~no host calls: the pure-Lua reconciler regime at a small tree size.
/// </summary>
[Config(typeof(BenchConfig))]
public class PreactSmallStableBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 1700 re-renders ≈ 1.0 s/invocation: F = 3.7 ms fixed + 1700 x 577 us per render, from the
    /// same single-run calibration as <see cref="PreactSmallFreshBenchmark"/>.
    /// </summary>
    const int Renders = 1700;
    const int Size = 16;

    [Benchmark(Description = "preact-small, stable props (16-node tree)", OperationsPerInvoke = Renders)]
    public void Render() => Run("preact_render.luau", Renders, new LuaValue((double)Renders), new LuaValue((double)Size), new LuaValue(false));
}

/// <summary>
/// 1024-node tree with fresh props: both regimes at once, 2048 host nodes' worth of VNodes
/// rebuilt and every property re-applied per render. Measured cost is ~129 ms/render, and the
/// script's one-time mount is a material share of a loop this short — see the note on
/// <see cref="Renders"/> and the README.
/// </summary>
[Config(typeof(BenchConfig))]
public class PreactLargeFreshBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 6 re-renders ≈ 1.0 s/invocation: a measured 271 ms fixed cost (the tree mount plus the
    /// script's three warm-up frames) plus 6 x 129 ms.
    ///
    /// That fixed cost is 26 % of the invocation, and it is why this loop is the shortest in the
    /// suite rather than merely short. <c>preact_render.luau</c> mounts the tree once per
    /// <c>run()</c>, inside the invocation but outside its <c>os.clock()</c> window — so it lands
    /// on the wall-clock columns and not on <c>Lua CPU</c>, which excludes it to the millisecond
    /// (measured here: Mean - Lua CPU = 272 ms, against the 271 ms the zero-render probe measured
    /// for the same fixed cost). Reach for <c>Lua CPU</c> when comparing per-render cost; the
    /// README has the figures and how to trade runtime for a smaller share.
    /// </summary>
    const int Renders = 6;
    const int Size = 1024;

    [Benchmark(Description = "preact-large, fresh props (1024-node tree)", OperationsPerInvoke = Renders)]
    public void Render() => Run("preact_render.luau", Renders, new LuaValue((double)Renders), new LuaValue((double)Size), new LuaValue(true));
}

/// <summary>
/// 1024-node tree, stable props: the reconciler walk without host calls, which is where a VM's
/// raw table/dispatch speed shows up. A handful of renders is enough — measured 134 ms/render.
/// </summary>
[Config(typeof(BenchConfig))]
public class PreactLargeStableBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 6 re-renders ≈ 1.0 s/invocation: F = 274 ms fixed + 6 x 134 ms. Matched to
    /// <see cref="PreactLargeFreshBenchmark"/>'s count so the two large cases differ only in the
    /// prop regime, and carrying the same caveat about the fixed mount's share of the wall clock
    /// (see that class for the measurement showing <c>Lua CPU</c> excludes it).
    /// </summary>
    const int Renders = 6;
    const int Size = 1024;

    [Benchmark(Description = "preact-large, stable props (1024-node tree)", OperationsPerInvoke = Renders)]
    public void Render() => Run("preact_render.luau", Renders, new LuaValue((double)Renders), new LuaValue((double)Size), new LuaValue(false));
}

/// <summary>
/// The timetrial HUD (17 host nodes, 3 dirty components per frame) driven through the real
/// deferred path: <c>__bench_push</c> → <c>GameThreadContext.ExecutePendingTasks</c>.
///
/// No <c>[Params]</c> for the script's <c>freshProps</c> flag, and the flag is passed as
/// <c>true</c>, because the flag never reaches the components that use it. <c>hud_render.luau</c>
/// documents a fresh-props/stable-props pair, but it hands the flag down as a <em>prop</em>
/// (<c>x(PowerDamageBars) { fresh = freshProps }</c>) while the components read it as their first
/// positional argument — so inside them it is that props table, which is always truthy, and every
/// regime allocates fresh style tables. Measured on both former param values: exactly
/// <c>setProp 13 commit 2</c> per frame, 108.23 KB allocated per frame, and the same CPU per frame.
/// Two rows of the same workload would only invite reading a difference into the noise, so the
/// measurement carries one. The shared script is left byte-identical between branches (see README);
/// the older "stable props" HUD figures were this same workload plus leak inflation.
/// </summary>
[Config(typeof(BenchConfig))]
public class HudBenchmark : LuaScenarioBenchmark
{
    /// <summary>800 frames ≈ 1.0 s/invocation (measured ~1.2 ms/frame).</summary>
    const int Frames = 800;

    protected override bool UsesHostStats => true;

    [Benchmark(Description = "hud, 800 frames (17-node preact tree, real defer path)", OperationsPerInvoke = Frames)]
    public void Render() => Run("hud_render.luau", Frames, new LuaValue((double)Frames), new LuaValue(true));
}

/// <summary>
/// The same HUD built with Sx instead of preact: the tree is mounted once and each frame's
/// <c>race:hudState</c> write only dirties a few leaves, so a flush is a handful of host calls
/// instead of a reconcile. Fine-grained enough to need a longer loop.
/// </summary>
[Config(typeof(BenchConfig))]
public class HudSxBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 11 000 frames ≈ 1.0 s/invocation (measured 88 us/frame). Much the longest loop in the
    /// suite because a frame is only a handful of host calls — but at the ~47 ms a 600-frame loop
    /// took, one tick of the process CPU clock (15.625 ms — see <see cref="LuaCpuColumn"/>) was
    /// 30 % of the whole measurement.
    /// </summary>
    const int Frames = 11000;

    protected override bool UsesHostStats => true;

    [Benchmark(Description = "hud_sx, 11000 frames (Sx fine-grained tree, real defer path)", OperationsPerInvoke = Frames)]
    public void Render() => Run("hud_sx.luau", Frames, new LuaValue((double)Frames));
}

/// <summary>
/// The reconciler-core microbenchmark: VNode construction, diffProps and the parent/child walk
/// with no C# host in the loop at all, so the same script can run under luau.exe and the gap is
/// VM-against-VM rather than host-bound.
///
/// No <c>[Params]</c> on the script's third argument: <c>vmcore.luau</c> reads it as
/// <c>freshProps ~= nil and freshProps or true</c>, i.e. always true, so a param would be a
/// second identical case. The shared script is deliberately left as-is.
/// </summary>
[Config(typeof(BenchConfig))]
public class VmcoreBenchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 6250 walks at depth 8 ≈ 1.0 s/invocation: F = 0.75 ms fixed + 6250 x 160 us per walk (the
    /// legacy console scenario in <c>Program.cs</c> uses 2000, which is the same work per walk).
    /// </summary>
    const int Walks = 6250;
    const int Depth = 8;

    [Benchmark(Description = "vmcore, 6250 reconciler-core walks (depth 8, no host)", OperationsPerInvoke = Walks)]
    public void Walk() => Run("vmcore.luau", Walks, new LuaValue((double)Walks), new LuaValue((double)Depth), new LuaValue(true));
}

/// <summary>
/// Heavy fixed64 arithmetic plus f64math interop, distilled from the AI's per-frame math. The
/// script also returns a checksum, which is the cross-VM guard: if a port's arithmetic diverges,
/// the checksum changes even when the timings look plausible. The legacy console scenario prints
/// it; here it is not a metric (BDN columns are numeric and per-op).
/// </summary>
[Config(typeof(BenchConfig))]
public class Fixed64Benchmark : LuaScenarioBenchmark
{
    /// <summary>
    /// 7600 scans of 500 nodes ≈ 1.0 s/invocation: F = 1.2 ms fixed + 7600 x 132 us per scan.
    /// (The script requires nothing, so this is the smallest fixed cost in the suite — most of the
    /// others are paying to load preact/Sx and mount a tree.)
    /// </summary>
    const int Iterations = 7600;
    const int NodeCount = 500;

    [Benchmark(Description = "fixed64, 7600 node-scan + trig iterations (500 nodes)", OperationsPerInvoke = Iterations)]
    public void Scan() => Run("fixed64_kernel.luau", Iterations, new LuaValue((double)Iterations), new LuaValue((double)NodeCount));
}
