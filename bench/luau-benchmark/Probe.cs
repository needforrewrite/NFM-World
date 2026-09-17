using BenchmarkDotNet.Attributes;
using Lua;

namespace LuauBenchmark;

// TEMPORARY -- calibration probe, delete after use.
//
// One invocation of a case is F + N*m, where F is the per-invocation fixed cost (fresh LuaState,
// chunk compile, module load, tree mount, the script's warm-up frames) and m is the marginal cost
// of one unit of its inner loop. The suite's counts want N = (1 s - F) / m.
//
// F cannot be read off a per-op Mean from other runs: this machine's throughput moves 30 % between
// runs with the background load, so a value derived from run A's per-op figure and run B's is
// mostly the load difference. Each pair below therefore measures F (the case's own script with a
// zero inner-loop count, so `Mean` *is* F) and the current N in one run, and
// `m = (Mean_N - Mean_F) / N`. Both methods carry OperationsPerInvoke = 1 so `Mean` and `Lua CPU`
// are whole-invocation figures; the second argument to Run() is 1 for the same reason (it divides
// the CPU delta).

[Config(typeof(BenchConfig))]
public class CalibPreactSmallFresh : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: preact-small fresh, 0 renders")] public void F() => Run("preact_render.luau", 1, new LuaValue(0.0), new LuaValue(16.0), new LuaValue(true));
    [Benchmark(Description = "N: preact-small fresh, 1600 renders")] public void N() => Run("preact_render.luau", 1, new LuaValue(1600.0), new LuaValue(16.0), new LuaValue(true));
}

[Config(typeof(BenchConfig))]
public class CalibPreactSmallStable : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: preact-small stable, 0 renders")] public void F() => Run("preact_render.luau", 1, new LuaValue(0.0), new LuaValue(16.0), new LuaValue(false));
    [Benchmark(Description = "N: preact-small stable, 1300 renders")] public void N() => Run("preact_render.luau", 1, new LuaValue(1300.0), new LuaValue(16.0), new LuaValue(false));
}

[Config(typeof(BenchConfig))]
public class CalibPreactLargeFresh : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: preact-large fresh, 0 renders")] public void F() => Run("preact_render.luau", 1, new LuaValue(0.0), new LuaValue(1024.0), new LuaValue(true));
    [Benchmark(Description = "N: preact-large fresh, 7 renders")] public void N() => Run("preact_render.luau", 1, new LuaValue(7.0), new LuaValue(1024.0), new LuaValue(true));
}

[Config(typeof(BenchConfig))]
public class CalibPreactLargeStable : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: preact-large stable, 0 renders")] public void F() => Run("preact_render.luau", 1, new LuaValue(0.0), new LuaValue(1024.0), new LuaValue(false));
    [Benchmark(Description = "N: preact-large stable, 7 renders")] public void N() => Run("preact_render.luau", 1, new LuaValue(7.0), new LuaValue(1024.0), new LuaValue(false));
}

[Config(typeof(BenchConfig))]
public class CalibHud : LuaScenarioBenchmark
{
    protected override bool UsesHostStats => true;

    [Benchmark(Description = "F: hud, 0 frames")] public void F() => Run("hud_render.luau", 1, new LuaValue(0.0), new LuaValue(true));
    [Benchmark(Description = "M: hud, 100 frames")] public void M() => Run("hud_render.luau", 1, new LuaValue(100.0), new LuaValue(true));
    [Benchmark(Description = "N: hud, 800 frames")] public void N() => Run("hud_render.luau", 1, new LuaValue(800.0), new LuaValue(true));
}

[Config(typeof(BenchConfig))]
public class CalibHudSx : LuaScenarioBenchmark
{
    protected override bool UsesHostStats => true;

    [Benchmark(Description = "F: hud_sx, 0 frames")] public void F() => Run("hud_sx.luau", 1, new LuaValue(0.0));
    [Benchmark(Description = "M: hud_sx, 600 frames")] public void M() => Run("hud_sx.luau", 1, new LuaValue(600.0));
    [Benchmark(Description = "N: hud_sx, 11000 frames")] public void N() => Run("hud_sx.luau", 1, new LuaValue(11000.0));
}

[Config(typeof(BenchConfig))]
public class CalibVmcore : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: vmcore, 0 walks")] public void F() => Run("vmcore.luau", 1, new LuaValue(0.0), new LuaValue(8.0), new LuaValue(true));
    [Benchmark(Description = "N: vmcore, 5800 walks")] public void N() => Run("vmcore.luau", 1, new LuaValue(5800.0), new LuaValue(8.0), new LuaValue(true));
}

[Config(typeof(BenchConfig))]
public class CalibFixed64 : LuaScenarioBenchmark
{
    [Benchmark(Description = "F: fixed64, 0 scans")] public void F() => Run("fixed64_kernel.luau", 1, new LuaValue(0.0), new LuaValue(500.0));
    [Benchmark(Description = "N: fixed64, 7200 scans")] public void N() => Run("fixed64_kernel.luau", 1, new LuaValue(7200.0), new LuaValue(500.0));
}
