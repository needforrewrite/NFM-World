using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using Perfolizer.Metrology;
using Pragmastat.Metrology;

namespace LuauBenchmark;

/// <summary>
/// The columns that carry <see cref="BenchMetrics"/>' numbers into the summary: the in-Lua CPU
/// cost per operation, its run-to-run spread, and the host-op counters. See <see cref="BenchMetrics"/>
/// for why this is a column reading the child's stdout rather than a diagnoser metric.
/// </summary>
abstract class BenchMarkerColumn : IColumn
{
    public abstract string Id { get; }
    public abstract string ColumnName { get; }
    public abstract string Legend { get; }

    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Metric;
    public abstract int PriorityInCategory { get; }
    public virtual bool IsNumeric => true;
    public virtual UnitType UnitType => UnitType.Dimensionless;

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public bool IsAvailable(Summary summary) => summary.Reports.Any(report => !BenchMetrics.Read(report).IsEmpty);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        if (!summary.HasReport(benchmarkCase))
        {
            return "NA";
        }

        // HasReport above guarantees the report exists; the indexer is annotated nullable.
        var samples = BenchMetrics.Read(summary[benchmarkCase]!);
        return samples.IsEmpty ? "NA" : Format(samples, style);
    }

    protected abstract string Format(BenchSamples samples, SummaryStyle style);

    public override string ToString() => ColumnName;
}

/// <summary>
/// The in-Lua <c>os.clock()</c> delta per operation — process CPU time, taken inside the script,
/// so it is the number that stays comparable across VM implementations and across a loaded machine
/// (it excludes time the process spent descheduled, unlike BDN's wall clock).
///
/// It is not a precise instrument, and the reason is worth stating because it is not obvious from
/// the number. <c>os.clock()</c> is <c>Process.GetCurrentProcess().TotalProcessorTime</c>, which on
/// Windows advances only when the OS updates process CPU accounting: <b>in 15.625 ms ticks</b>.
/// Verified on this machine — all 45 marker readings in a calibration run, multiplied by their
/// case's <c>OperationsPerInvoke</c>, are exact integer multiples of 15.625 ms. So a single sample
/// cannot resolve anything finer than a tick: ~1.6 % of a 1 s invocation, and the whole
/// measurement when an invocation is shorter than a few ticks. That is why the inner-loop counts
/// target ~1 s rather than the ~0.2 s the iteration overhead alone would allow.
///
/// Two consequences to read the column with. Averaging over the measured iterations reduces the
/// quantization error, so the Mean is better than any single sample. And because the clock is
/// process-wide, it counts every thread's CPU during the window, not just the interpreter's —
/// measured on the state-free <c>vmcore</c> case, CPU per walk ran 1.3x its wall clock, which no
/// single thread can do.
/// </summary>
sealed class LuaCpuColumn : BenchMarkerColumn
{
    public override string Id => BenchMetrics.Ids.LuaCpu;
    public override string ColumnName => "Lua CPU";
    public override int PriorityInCategory => 0;
    public override UnitType UnitType => UnitType.Time;

    public override string Legend =>
        "Mean in-Lua os.clock() delta per operation: process CPU time, measured inside the script, "
        + "per render / frame / walk / iteration (see OperationsPerInvoke). os.clock() is "
        + "TotalProcessorTime, which advances in 15.625 ms ticks on Windows (about 1.6 % of a 1 s "
        + "invocation per sample), and is process-wide, so it also counts what other threads (GC, "
        + "tiered JIT compilation) burned while the script ran and can exceed the wall-clock Mean.";

    protected override string Format(BenchSamples samples, SummaryStyle style)
    {
        var perOpNs = samples.Mean(BenchMetrics.Keys.CpuNs);
        if (double.IsNaN(perOpNs))
        {
            return "?";
        }

        // Same formatter and presentation BDN's own time columns use, so this column lines up with
        // Mean/StdDev instead of inventing a second convention.
        var measurement = TimeInterval.FromNanoseconds(perOpNs).ToMeasurement(style.TimeUnit);
        return PerfolizerMeasurementFormatter.Instance.Format(
            measurement,
            "N3",
            style.CultureInfo,
            new UnitPresentation(style.PrintUnitsInContent, minUnitWidth: 0, gap: true));
    }
}

/// <summary>
/// The spread of that CPU figure across the measured iterations, as a percentage.
///
/// This is deliberately <em>not</em> presented as a stationarity check, which is what it was
/// originally added for. Two things other than the workload move it, and on a state-free case they
/// move it a lot:
///
///   * the 15.625 ms CPU-clock tick (see <see cref="LuaCpuColumn"/>) — a whole measurement when an
///     invocation is short. Before the counts were raised, a 600-frame <c>hud_sx</c> invocation
///     took 47-78 ms in total, i.e. 3-5 ticks, so its per-op CPU could barely take three distinct
///     values and the spread was mostly the clock's step size;
///   * background CPU. On <c>vmcore</c>, which reuses no state by construction (fresh
///     <c>LuaState</c> and host per iteration, and no C# host in the script at all), CPU per walk
///     ran 1.3x the wall clock across a 24-41 tick range — a process that spends more CPU than
///     wall time has other threads running, and no leak can produce that either.
///
/// So read this as a reliability bound on the Lua CPU column, not as evidence about the workload.
/// <see cref="WallTrendColumn"/> is the stationarity check.
/// </summary>
sealed class CpuSpreadColumn : BenchMarkerColumn
{
    public override string Id => BenchMetrics.Ids.CpuRsd;
    public override string ColumnName => "CPU RSD";
    public override int PriorityInCategory => 1;

    public override string Legend =>
        "Relative standard deviation (percent) of the in-Lua CPU per operation across the measured "
        + "iterations. Bounded below by the CPU clock's 15.625 ms tick (about 1.6 % of a 1 s "
        + "invocation) and inflated by any other thread's CPU — so read it as a reliability bound "
        + "on the Lua CPU column, not as a statement about the workload, and read Trend for whether "
        + "the iterations were stationary.";

    protected override string Format(BenchSamples samples, SummaryStyle style)
    {
        var rsd = samples.RelativeStandardDeviation(BenchMetrics.Keys.CpuNs);
        return double.IsNaN(rsd) ? "?" : rsd.ToString("N2", style.CultureInfo) + " %";
    }
}

/// <summary>
/// The <c>LuaUiHostStats</c> counters per operation, as one compact field: for the HUD cases these
/// are the per-frame host calls that separate the fresh-props regime (a setProperty per node per
/// frame) from the stable-props one (essentially none). Non-HUD cases show "NA".
/// </summary>
sealed class HostOpsColumn : BenchMarkerColumn
{
    public override string Id => BenchMetrics.Ids.HostOps;
    public override string ColumnName => "Host ops";
    public override int PriorityInCategory => 2;
    public override bool IsNumeric => false;

    public override string Legend =>
        "LuaUiHostStats calls per operation: setProperty / commitText / create(instance+text) / "
        + "structural(appendChild+insertBefore+removeChild). Recorded only by the HUD scenarios.";

    protected override string Format(BenchSamples samples, SummaryStyle style)
    {
        if (!samples.HasHostOps)
        {
            return "NA";
        }

        var culture = style.CultureInfo;
        return string.Join(" ", new[]
        {
            Field("setProp", BenchMetrics.Keys.SetProperty, samples, culture),
            Field("commit", BenchMetrics.Keys.CommitText, samples, culture),
            Field("create", BenchMetrics.Keys.Create, samples, culture),
            Field("struct", BenchMetrics.Keys.Structure, samples, culture),
        });
    }

    static string Field(string name, string key, BenchSamples samples, CultureInfo culture)
        => name + " " + samples.Mean(key).ToString("N1", culture);
}

/// <summary>
/// The table's stationarity check, and the direct answer to "the results are extremely noisy run to
/// run": the slope of the per-operation wall time across the measured iterations.
///
/// Near zero means every iteration did the same work. A rising figure is the signature of state
/// accumulating between iterations — the old harness's reused <c>LuaState</c>, where run N also drove
/// the trees mounted by runs 1..N-1, so each run cost more than the last (see
/// <see cref="LuaScenarioBenchmark"/> for the diagnosis).
///
/// It reads BenchmarkDotNet's own per-iteration measurements — the same runs the Mean column
/// averages — rather than the marker series, because wall time cannot be inflated by another
/// thread, so this column separates "this iteration did more work" from "something else was busy"
/// in a way the CPU columns cannot.
/// </summary>
sealed class WallTrendColumn : IColumn
{
    public string Id => "WallTrend";
    public string ColumnName => "Trend";
    public int PriorityInCategory => 3;
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Metric;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;

    public string Legend =>
        "Least-squares slope of the per-operation wall time over the measured iterations, in percent "
        + "of the mean per iteration. Near zero: every iteration did the same work. Positive: each "
        + "iteration cost more than the last, which is what state accumulating between them looks "
        + "like.";

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public bool IsAvailable(Summary summary) => summary.Reports.Any(report => PerOperation(report).Count >= 3);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        if (!summary.HasReport(benchmarkCase))
        {
            return "NA";
        }

        var slope = SlopePercentPerIteration(PerOperation(summary[benchmarkCase]!));
        return double.IsNaN(slope) ? "NA" : slope.ToString("+0.00;-0.00;0.00", style.CultureInfo) + " %";
    }

    /// <summary>Per-operation wall time of each measured iteration, in run order.</summary>
    static List<double> PerOperation(BenchmarkReport report)
    {
        var values = new List<double>();
        foreach (var run in BenchMetrics.MeasuredRuns(report))
        {
            values.Add(run.Nanoseconds / Math.Max(run.Operations, 1));
        }

        return values;
    }

    /// <summary>
    /// Least-squares slope over the iteration index, as a percentage of the mean. Below three points
    /// a slope is not worth reporting, so those cases read "NA".
    /// </summary>
    static double SlopePercentPerIteration(List<double> values)
    {
        var n = values.Count;
        if (n < 3)
        {
            return double.NaN;
        }

        double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXX += (double)i * i;
            sumXY += i * values[i];
        }

        var denominator = n * sumXX - sumX * sumX;
        var mean = sumY / n;
        if (denominator == 0.0 || mean == 0.0)
        {
            return double.NaN;
        }

        return (n * sumXY - sumX * sumY) / denominator / mean * 100.0;
    }

    public override string ToString() => ColumnName;
}
