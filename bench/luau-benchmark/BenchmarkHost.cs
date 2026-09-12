using System.Diagnostics;
using System.Threading.Tasks;
using Lua;
using Lua.IO;
using Lua.Platforms;
using Lua.Standard;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.LuaSourceGenerator.Generator.NFMWorld.Library;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace LuauBenchmark;

/// <summary>
/// The single VM-specific entry point for the benchmark.
///
/// Written against the Lua-CSharp API. To compare against the real Luau VM, fork this
/// file (and Program.cs's scenario dispatch) to use NuLua.Luau — the scripts under
/// scripts/ are VM-agnostic and shared unchanged between the two branches.
///
/// What this host does, mirroring the real game (nfm-world/UI/UiRenderer.cs):
///   1. Create a Lua-CSharp LuaState with RequireByString + os.clock + `unpack`.
///   2. Install a GameThreadContext + a no-op DummyBackend so no FNA/graphics is touched.
///   3. Register the REAL LuaUiLibrary host (createInstance/setProperty/etc. -> View/Yoga).
///   4. Load the real react.luau (preact-luau) module; `_G.UiLib` is captured at module load.
///   5. Load and run a benchmark script that drives renders / fixed64 math, returning CPU time.
/// </summary>
public sealed class BenchmarkHost : IDisposable
{
    readonly LuaState _state;
    readonly string _libraryRoot;

    public View? ActiveRoot { get; private set; }

    readonly Dictionary<int, (string Event, Action<LuaValue> Callback)> _handlers = new();
    readonly object _gate = new();
    int _eventId;

    public BenchmarkHost(string libraryRoot)
    {
        _libraryRoot = Path.GetFullPath(libraryRoot);

        _state = CreateState(_libraryRoot);

        // Headless: no real graphics/input backend, and a SynchronizationContext so
        // LuaUiLibrary's `defer` (which would otherwise throw without one) is satisfied.
        GameThreadContext.Install();
        IBackend.Backend = new DummyBackend();

        // Register the real host BEFORE loading react.luau: preact-luau/src/ui.luau
        // captures `_G.UiLib` at module-load time.
        LuaUiLibrary.Register(_state, SetActiveRoot, Call, OnEvent);

        var react = LoadReact();
        _state.Environment["React"] = react;

        var sx = LoadSx();
        _state.Environment["Sx"] = sx;

        RegisterBenchGlobals();
    }

    // ---- LuaState setup (Lua-CSharp) ---------------------------------------

    static LuaState CreateState(string libraryRoot)
    {
        var platform = new LuaPlatform(
            FileSystem: new LibraryFileSystem(libraryRoot),
            OsEnvironment: new LuaNfmwPlatform.SystemOsEnvironment(),
            StandardIO: new ConsoleStandardIO(),
            TimeProvider: TimeProvider.System)
        {
            RequireByString = true
        };

        var state = LuaState.Create(platform);
        state.OpenStandardLibraries();          // includes fixed64 / f64math
        LuaVisibleTypeRegistry.RegisterAll(state);
        state.Environment["unpack"] = new LuaFunction("unpack", TableLibrary.Unpack);
        return state;
    }

    // ---- LuaUiLibrary stand-in delegates ------------------------------------

    void SetActiveRoot(View view) => ActiveRoot = view;

    void Call(string method, LuaValue payload) { }

    Action OnEvent(string @event, Action<LuaValue> callback)
    {
        int id;
        lock (_gate) id = _eventId++;
        _handlers[id] = (@event, callback);
        return () => { lock (_gate) _handlers.Remove(id); };
    }

    /// <summary>
    /// Dispatch a C#→Lua event to every handler subscribed via UiLib.onEvent, matching
    /// <see cref="UiRenderer.PushToLua"/> in the real game. Payload is a Lua value (the
    /// repro passes a fresh Lua table, mirroring a freshly-built HudStateData each frame).
    /// </summary>
    public void PushEvent(string evt, LuaValue payload)
    {
        lock (_gate)
        {
            foreach (var (_, h) in _handlers)
            {
                if (h.Event == evt)
                {
                    h.Callback(payload);
                }
            }
        }
    }

    /// <summary>
    /// Run the deferred UI flush, mirroring the end of WorldGame.Update(): every
    /// setState a frame queued via UiLib.defer → GameThreadContext.Post is drained here
    /// (one preact `process()` that re-renders the dirty components).
    /// </summary>
    public void FlushPendingTasks()
    {
        GameThreadContext.Current.ExecutePendingTasks();
    }

    void RegisterBenchGlobals()
    {
        _state.Environment["__bench_push"] = new LuaFunction("__bench_push", (context, ct) =>
        {
            var evt = context.GetArgument<string>(0);
            var payload = context.GetArgument(1);
            PushEvent(evt, payload);
            return new ValueTask<int>(context.Return());
        });
        _state.Environment["__bench_flush"] = new LuaFunction("__bench_flush", (context, ct) =>
        {
            FlushPendingTasks();
            return new ValueTask<int>(context.Return());
        });
        _state.Environment["__bench_reset_stats"] = new LuaFunction("__bench_reset_stats", (context, ct) =>
        {
            LuaUiHostStats.Reset();
            return new ValueTask<int>(context.Return());
        });
    }

    // ---- Script loading ------------------------------------------------------

    LuaValue LoadReact()
    {
        var reactPath = Path.Combine(_libraryRoot, "react.luau");
        var code = File.ReadAllText(reactPath);
        var closure = _state.Load(code, "react.luau");
        var results = _state.Call(closure, []);
        return results.Length > 0 ? results[0] : LuaValue.Nil;
    }

    /// <summary>
    /// Load the Sx fine-grained reactive UI module (data/library/sx/index.luau). Its
    /// `require('./signals'|'./h'|'./dom')` graph resolves through the LibraryFileSystem
    /// rooted at data/library; dom.luau captures `_G.UiLib` at load (registered above).
    /// </summary>
    LuaValue LoadSx()
    {
        var sxPath = Path.Combine(_libraryRoot, "sx", "index.luau");
        var code = File.ReadAllText(sxPath);
        var closure = _state.Load(code, "sx/index.luau");
        var results = _state.Call(closure, []);
        return results.Length > 0 ? results[0] : LuaValue.Nil;
    }

    /// <summary>
    /// Load a benchmark script (scripts/*.luau) and invoke its returned `run(...)` function.
    /// The script chunk must return a single `run` function.
    /// Returns (cpuSeconds from os.clock, wallSeconds from a C# Stopwatch, all returns).
    /// </summary>
    public (double CpuSeconds, double WallSeconds, LuaValue[] Returns) RunScript(
        string scriptPath,
        params LuaValue[] args)
    {
        var code = File.ReadAllText(scriptPath);
        var closure = _state.Load(code, "bench_" + Path.GetFileName(scriptPath));
        var module = _state.Call(closure, []);
        var runFn = module.Length > 0 ? module[0] : LuaValue.Nil;

        var sw = Stopwatch.StartNew();
        var results = _state.Call(runFn, args);
        sw.Stop();

        double cpu = double.NaN;
        if (results.Length > 0 && results[0].TryRead<double>(out var c))
        {
            cpu = c;
        }
        return (cpu, sw.Elapsed.TotalSeconds, results);
    }

    // ---- Real-filesystem ILuaFileSystem rooted at data/library -----------------

    /// <summary>
    /// Read-only real-filesystem backend rooted at the repo's data/library so the
    /// `./`-relative preact-luau require graph resolves from disk (no VFS needed).
    /// </summary>
    sealed class LibraryFileSystem : ILuaFileSystem
    {
        readonly string _root;

        public LibraryFileSystem(string root) => _root = Path.GetFullPath(root);

        string Map(string path)
        {
            var rel = path.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_root, rel));
        }

        public bool IsReadable(string path) => File.Exists(Map(path));

        public bool DirectoryExists(string path) => Directory.Exists(Map(path));

        public ValueTask<ILuaStream> Open(string path, LuaFileOpenMode mode, CancellationToken ct)
            => ValueTask.FromResult(ILuaStream.CreateFromStream(File.OpenRead(Map(path)), LuaFileOpenMode.Read));

        public ValueTask Rename(string oldName, string newName, CancellationToken ct)
            => throw new NotSupportedException();

        public ValueTask Remove(string path, CancellationToken ct)
            => throw new NotSupportedException();

        public string DirectorySeparator => "/";

        public string GetTempFileName() => Path.GetTempFileName();

        public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken ct)
            => throw new NotSupportedException();
    }

    public void Dispose() => _state.Dispose();
}
