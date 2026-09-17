using System.Globalization;
using System.Text;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Reports;
using NFMWorldLibrary.Util;

namespace LuauBenchmark;

/// <summary>
/// The per-invocation numbers this suite wants beside BDN's own statistics: the in-Lua
/// <c>os.clock()</c> delta (the fair cross-VM metric) and the <see cref="NFMWorldLibrary.Util.LuaUiHostStats"/>
/// host-op counters, which for the HUD cases record the host calls per frame that separate a
/// render-driven frame from a leaf update.
///
/// How they reach the summary is not obvious, so, briefly. BenchmarkDotNet runs the benchmark body
/// in a child process, and for <c>RunMode.NoOverhead</c> diagnosers it calls
/// <c>IDiagnoser.ProcessResults</c> in the <b>host</b> process with a <c>DiagnoserResults</c> that
/// carries only what BDN itself ferried across (measurements, GC and threading stats) — there is no
/// channel in it for a custom scalar, and a static filled in by the child is empty in the host. What
/// <i>is</i> there host-side is the child's raw standard output: BDN redirects stdout and publishes
/// every line as <c>ExecuteResult.StandardOutput</c>, reachable from a column as
/// <c>summary[benchmarkCase].ExecuteResults</c>. So the transport is:
///
///   child: <c>[IterationCleanup]</c> → <see cref="Emit"/> → one <c>// LUAU_BENCH ...</c> line on stdout
///   host:  <c>BenchColumns</c> → <see cref="Read"/> → mean / relative stddev per case
///
/// No case key is needed for the correlation — BDN already knows which report belongs to which case.
/// The line is emitted from the cleanup (outside the measured region, and after the iteration's
/// numbers are in), so it costs the measurement nothing.
/// </summary>
static class BenchMetrics
{
    /// <summary>Prefix of the marker line; kept in the "//" namespace so it reads as BDN output.</summary>
    public const string Marker = "// LUAU_BENCH";

    /// <summary>Wire keys of the marker line. Short and stable: they are parsed back host-side.</summary>
    public static class Keys
    {
        public const string CpuNs = "cpuNs";
        public const string SetProperty = "setProperty";
        public const string CommitText = "commitText";
        public const string Create = "create";
        public const string Structure = "structure";
    }

    /// <summary>Column ids; also the sample keys the columns ask <see cref="BenchSamples"/> for.</summary>
    public static class Ids
    {
        public const string LuaCpu = "LuaCpu";
        public const string CpuRsd = "CpuRsd";
        public const string HostOps = "HostOps";
    }

    static readonly object Gate = new();
    static readonly Dictionary<string, double> Current = new(StringComparer.Ordinal);

    /// <summary>
    /// Starts a fresh invocation. Called from the benchmark body, so what a marker line reports is
    /// exactly one <c>run()</c> call — never an accumulation over iterations.
    /// </summary>
    public static void Reset()
    {
        lock (Gate)
        {
            Current.Clear();
        }
    }

    public static void Record(string key, double value)
    {
        lock (Gate)
        {
            Current[key] = value;
        }
    }

    /// <summary>Records the host-op counters the HUD scenarios use, per operation.</summary>
    public static void RecordHostStats(int operationsPerInvoke)
    {
        var ops = Math.Max(operationsPerInvoke, 1);
        Record(Keys.SetProperty, LuaUiHostStats.SetPropertyCount / (double)ops);
        Record(Keys.CommitText, LuaUiHostStats.CommitTextCount / (double)ops);
        Record(Keys.Create, (LuaUiHostStats.CreateInstanceCount + LuaUiHostStats.CreateTextCount) / (double)ops);
        Record(Keys.Structure, LuaUiHostStats.StructureCount / (double)ops);
    }

    /// <summary>
    /// Writes this invocation's numbers to stdout for the host-side columns to collect. Runs in the
    /// measured process; <see cref="Console.Out"/> there is the process's real stdout, which BDN
    /// captures (it also echoes it live, which is why the rates can be watched as the run proceeds).
    /// </summary>
    public static void Emit()
    {
        lock (Gate)
        {
            if (Current.Count == 0)
            {
                return;
            }

            var line = new StringBuilder(Marker);
            foreach (var (key, value) in Current)
            {
                line.Append(' ').Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
            Console.WriteLine(line.ToString());
        }
    }

    /// <summary>
    /// The iterations BenchmarkDotNet's own statistics are computed from: <c>Workload</c> runs at
    /// the <c>Result</c> stage — i.e. the measured iterations, without the overhead runs (which time
    /// an empty method) and without the jitting/pilot/warm-up stages. <see cref="WallTrendColumn"/>
    /// fits its slope over exactly these, so Trend and Mean describe the same samples.
    /// </summary>
    public static List<Measurement> MeasuredRuns(BenchmarkReport report)
    {
        var runs = new List<Measurement>();
        foreach (var measurement in report.AllMeasurements)
        {
            if (measurement.IterationMode == IterationMode.Workload && measurement.IterationStage == IterationStage.Result)
            {
                runs.Add(measurement);
            }
        }

        return runs;
    }

    /// <summary>
    /// The samples for one case, read back from the child's output. Only the measured iterations are
    /// kept: the process emits one marker per <em>workload</em> iteration (overhead runs have no
    /// setup/cleanup), and the earlier ones — the jitting iteration and the warm-ups — are cold.
    /// The count comes from the report rather than from <see cref="BenchConfig.IterationCount"/>, so
    /// a <c>--iterationCount</c> override on the command line cannot silently fold warm-up samples
    /// into the mean.
    /// </summary>
    public static BenchSamples Read(BenchmarkReport report)
    {
        var parsed = new List<Dictionary<string, double>>();

        foreach (var execute in report.ExecuteResults)
        {
            foreach (var line in execute.StandardOutput)
            {
                if (TryParse(line, out var sample))
                {
                    parsed.Add(sample);
                }
            }
        }

        // Exactly the runs BenchmarkDotNet's own statistics are computed from, so this column and
        // Mean describe the same iterations. The sanity check guards the case where the report has
        // no measured runs (or more than the marker stream could hold — there is always at least one
        // non-measured workload iteration, the jitting one); then fall back to the configured count.
        var measured = MeasuredRuns(report).Count;
        var take = measured > 0 && measured < parsed.Count
            ? measured
            : Math.Min(BenchConfig.IterationCount, parsed.Count);
        var series = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        for (var i = parsed.Count - take; i < parsed.Count; i++)
        {
            foreach (var (key, value) in parsed[i])
            {
                if (!series.TryGetValue(key, out var values))
                {
                    series[key] = values = [];
                }
                values.Add(value);
            }
        }

        return new BenchSamples(series);
    }

    static bool TryParse(string line, out Dictionary<string, double> sample)
    {
        sample = [];

        if (!line.StartsWith(Marker, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var field in line[Marker.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = field.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (double.TryParse(field[(separator + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                sample[field[..separator]] = value;
            }
        }

        return sample.Count > 0;
    }
}

/// <summary>One case's recorded series, with the two summaries the columns show.</summary>
sealed class BenchSamples(IReadOnlyDictionary<string, List<double>> series)
{
    /// <summary>True when the case recorded nothing at all (no markers on its stdout).</summary>
    public bool IsEmpty => series.Count == 0;

    /// <summary>Mean of the measured iterations, NaN when the case recorded nothing under this key.</summary>
    public double Mean(string key)
        => series.TryGetValue(key, out var values) && values.Count > 0 ? values.Average() : double.NaN;

    /// <summary>
    /// Relative standard deviation as a percentage. Read it as a reliability bound on the CPU
    /// figure rather than as a statement about the workload: the process CPU clock's 15.625 ms
    /// tick puts a floor under it, and any other thread's CPU raises it (see
    /// <see cref="CpuSpreadColumn"/>). <see cref="WallTrendColumn"/> is what tests stationarity.
    /// </summary>
    public double RelativeStandardDeviation(string key)
    {
        if (!series.TryGetValue(key, out var values) || values.Count < 2)
        {
            return double.NaN;
        }

        var mean = values.Average();
        if (mean == 0.0)
        {
            return double.NaN;
        }

        var sum = 0.0;
        foreach (var value in values)
        {
            sum += (value - mean) * (value - mean);
        }

        var stdDev = Math.Sqrt(sum / (values.Count - 1));
        return stdDev / Math.Abs(mean) * 100.0;
    }

    /// <summary>Whether the case reported any host-op counter (HUD cases do, the rest do not).</summary>
    public bool HasHostOps => !double.IsNaN(Mean(BenchMetrics.Keys.SetProperty));
}
