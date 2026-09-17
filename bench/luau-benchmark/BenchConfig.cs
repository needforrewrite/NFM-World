using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace LuauBenchmark;

/// <summary>
/// The measurement discipline for every case. One job, so the numbers are comparable across
/// cases; counts live here so they can be changed in one place.
///
/// Two settings are load-bearing rather than taste:
///
///   * <c>InvocationCount(1)</c> + <c>UnrollFactor(1)</c> pin one <c>run()</c> call per
///     <c>[IterationSetup]</c>. Each invocation needs its own fresh <see cref="BenchmarkHost"/>
///     (see <see cref="LuaScenarioBenchmark"/> for why), and the pilot stage would otherwise be
///     free to raise the invocation count so that several <c>run()</c> calls shared one host —
///     which is precisely the state-accumulation bug this port exists to remove.
///   * the explicit warm-up/iteration counts skip the pilot stage entirely, so every case costs
///     exactly <c>WarmupCount + IterationCount</c> invocations.
///
/// The scripts' inner loops are sized so one invocation takes about a second — shorter than the
/// old multi-second runs, and longer than the iteration schedule alone would need, because the
/// in-Lua CPU metric is read from a clock that ticks in 15.625 ms steps (<see cref="LuaCpuColumn"/>)
/// and a short invocation cannot resolve it. <c>OperationsPerInvoke</c> on each <c>[Benchmark]</c>
/// keeps the reported figures per unit of work, so they stay comparable with the older
/// long-running runs.
///
/// These two constants are the supported way to make a run longer or shorter (the README's
/// "faster/slower" examples). The BenchmarkDotNet CLI switches <c>--warmupCount</c> /
/// <c>--iterationCount</c> override them and still leave the columns correct, because the columns
/// read the real counts back out of the report rather than assuming these values.
/// </summary>
sealed class BenchConfig : ManualConfig
{
    public const int WarmupCount = 3;

    /// <summary>
    /// Seven rather than five: an iteration is one ~1 s invocation, so two more samples cost a
    /// couple of seconds per case and tighten the 99.9 % confidence interval noticeably, give
    /// <see cref="WallTrendColumn"/> enough points for a meaningful fit, and average the CPU
    /// clock's 15.625 ms tick down by another factor. It also keeps the <c>Error</c> column
    /// meaningful — that column is the CI half-width, and at two or three samples the
    /// 99.9 % t-multiplier is large enough to print an Error several times the Mean.
    /// </summary>
    public const int IterationCount = 7;

    public BenchConfig()
    {
        AddJob(Job.Default
            .WithWarmupCount(WarmupCount)
            .WithIterationCount(IterationCount)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        AddDiagnoser(MemoryDiagnoser.Default);

        // The in-Lua os.clock delta and the LuaUiHostStats counters, as extra columns. These read
        // the numbers the benchmark bodies print to stdout, so no diagnoser is involved -- see
        // BenchMetrics for why that is the transport and not an IDiagnoser metric.
        // WallTrendColumn is the odd one out: it reads BenchmarkDotNet's own per-iteration
        // measurements, since a stationarity check needs a signal background threads cannot inflate.
        AddColumn(new LuaCpuColumn(), new CpuSpreadColumn(), new HostOpsColumn(), new WallTrendColumn());
    }
}
