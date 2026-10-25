using System.Runtime.CompilerServices;

namespace LuauBenchmark;

/// <summary>
/// Plumbing shared by both entry points — the BenchmarkDotNet benchmarks
/// (<see cref="Benchmarks"/>) and the legacy console scenarios (<c>Program.cs</c>).
/// </summary>
static class BenchSupport
{
    /// <summary>
    /// Quiet the game's per-node Debug logging (LuaUiLibrary logs every setProperty/commitTextUpdate),
    /// which would otherwise flood stdout and skew timing. Default to Warning; override to e.g.
    /// "Trace" if you want verbose logs.
    ///
    /// This is a module initializer rather than a line at the top of <c>Main</c> because
    /// BenchmarkDotNet does NOT run this project's <c>Main</c> in the measured child process — its
    /// generated program has its own entry point — so a top-of-Main assignment would only ever
    /// apply to the legacy console path. The variable has to be set before the first
    /// <c>NFMWorld.Library.Logging</c> use (host creation); nothing touches Logging before the
    /// benchmark body does, so loading this module first is early enough.
    /// </summary>
    [ModuleInitializer]
    internal static void Init()
    {
        QuietLogging();
    }

    /// <summary>Idempotent: also called by the console path, which cannot rely on ordering.</summary>
    public static void QuietLogging()
    {
        if (Environment.GetEnvironmentVariable("NFMW_LOG_MIN_LEVEL") is null)
        {
            Environment.SetEnvironmentVariable("NFMW_LOG_MIN_LEVEL", "Warning");
        }
    }

    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>
    /// The named console scenarios the dispatcher in <c>Program.cs</c> recognises; anything else on
    /// the command line is BenchmarkDotNet's. This is the one list to extend when a scenario is added.
    /// </summary>
    public static IReadOnlySet<string> ConsoleScenarios { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fixed64",                     // heavy fixed64 + f64math interop, prints its checksum
        "preact-small",                // 16-node tree, fresh props
        "preact-large",                // 1024-node tree, stable props
        "hud",                         // preact HUD, both prop regimes
        "hud_sx",                      // Sx HUD
        "vmcore",                      // reconciler core, no C# host
        "all",                         // every scenario above, in one process
        "profile-hud-sx",              // GC + table/call/GTC/VM-instruction breakdown
        "profile-preact-small",
        "profile-preact-large",
        "trace-preact-large",          // per-type allocation traces
        "trace-preact-small",
        "trace-preact-large-mount",    // first render only, i.e. the mount
    };

    /// <summary>The <c>data/library</c> root the Lua require graph is resolved against.</summary>
    public static string LibraryRoot => Path.Combine(RepoRoot, "NFMWorld.Library", "data", "library");

    public static string Script(string name) => Path.Combine(RepoRoot, "bench", "luau-benchmark", "scripts", name);

    /// <summary>
    /// Walk up from the executing assembly until the repo's <c>NFMWorld.Library/data/library</c> shows
    /// up. Unlike the working directory this also resolves from BenchmarkDotNet's generated-project
    /// folder (<c>bin/Release/net10.0/&lt;guid&gt;/bin/Release/net10.0/</c>), whose depth inside
    /// <c>bench/luau-benchmark</c> is why a fixed relative path would not do.
    /// </summary>
    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "NFMWorld.Library", "data", "library")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root not found (NFMWorld.Library/data/library)");
    }
}
