// LLM maintained.
//
// The MSBuild task that turns `data/shaders/*.fx` into C# bundles during a build.
//
// It is a thin adapter: everything it does is a call into NFMWorld.ShaderCompiler, plus the two
// jobs that only exist because this runs inside MSBuild's process rather than in an app of its
// own - loading the natives (see NativeLibraries) and reporting diagnostics as build messages and
// errors rather than as console output.
//
// It takes the shader list as a single item array and loops over it internally rather than letting
// MSBuild batch the task. That is deliberate: the compiler's include cache and its spirv-cross
// context are per-file state, so batching would be safe here, but a batched task also loses the
// whole batch on the first failure. Looping keeps one error report listing every shader that
// failed, which is what an author fixing a header that four shaders include actually wants.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NFMWorld.ShaderCompiler.Task;

public sealed class ShaderCompileTask : Microsoft.Build.Utilities.Task
{
    /// <summary>The <c>.fx</c> sources to compile.</summary>
    [Required]
    public ITaskItem[] Shaders { get; set; } = [];

    /// <summary>The directory the bundles are written to; created if absent.</summary>
    [Required]
    public string OutputDirectory { get; set; } = "";

    /// <summary>
    /// A file touched once every shader has compiled, used by the importing target as its single
    /// up-to-date marker.
    ///
    /// The target cannot list the bundles as outputs, because a source maps to a variable number of
    /// them (Nvg.fx yields four techniques, Poly.fx two), so no <c>%(Filename)</c> expression names
    /// the real set. A stamp is written last rather than first so a failed batch leaves the previous
    /// one in place and the next build retries instead of treating the failure as up to date.
    ///
    /// The name encodes the flavour: see <see cref="NoWgslStampFile"/>. The target names this one
    /// after what it <em>expects</em> beside the task, and the task writes whichever it actually
    /// produced - a native that is present but cannot be loaded is indistinguishable from a file
    /// test's point of view, and stamping the expected name there would keep bundles no WebGPU
    /// pipeline can be created from.
    /// </summary>
    public string StampFile { get; set; } = "";

    /// <summary>
    /// The stamp for a batch that carries no WGSL, and so the one <see cref="StampFile"/>'s tree
    /// must not have. Written instead of <see cref="StampFile"/> when
    /// <see cref="WgslCompiler.Available"/> is false, with <see cref="StampFile"/> deleted, so the
    /// target finds no stamp for its expected flavour and compiles again - each build, until the
    /// translator works, which is what a host that cannot load it deserves.
    /// </summary>
    public string NoWgslStampFile { get; set; } = "";

    /// <summary>
    /// The paths written, one item per bundle. An output rather than something the target globs for
    /// afterwards, because the target has to add them to <c>Compile</c> explicitly - they live under
    /// <c>obj/</c>, which the SDK's default <c>**/*.cs</c> glob excludes - and a glob would pick up
    /// bundles from a previous build that no longer correspond to any shader.
    /// </summary>
    [Output]
    public ITaskItem[] GeneratedFiles { get; private set; } = [];

    public override bool Execute()
    {
        NativeLibraries.EnsureLoaded();

        // A warning, not a message: the bundles this writes cannot start the WebGPU backend, and the
        // failure otherwise appears at pipeline creation in a built game with nothing pointing here.
        if (WgslCompiler.UnavailableReason is { } noWgsl)
            Log.LogWarning($"{noWgsl}; the bundles this build writes carry no WGSL, so the WebGPU backend cannot start.");

        if (Shaders.Length == 0)
        {
            Log.LogMessage(MessageImportance.High, "No shaders to compile.");
            return true;
        }

        var written = new List<ITaskItem>();
        var failed = false;

        foreach (var shader in Shaders)
        {
            var path = shader.GetMetadata("FullPath");
            try
            {
                foreach (var bundle in ShaderBuild.CompileFile(path, line => Log.LogMessage(MessageImportance.Low, line)))
                {
                    Directory.CreateDirectory(OutputDirectory);
                    var outputPath = Path.GetFullPath(Path.Combine(OutputDirectory, bundle.ProgramName + ".g.cs"));

                    // Only rewrite when the content actually changed. The C# compile downstream is
                    // incremental on timestamps, so touching twelve unchanged bundles every build
                    // would recompile the world each time and make the whole target pointless.
                    if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != bundle.Source)
                        File.WriteAllText(outputPath, bundle.Source);

                    written.Add(new TaskItem(outputPath));
                }
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                // A shader that does not compile is a build error, not a task failure: the task
                // reports it and keeps going, so one bad shader in a batch does not hide the state
                // of the other eleven behind a single exception.
                Log.LogError(null, null, null, path, 0, 0, 0, 0, e.Message);
                failed = true;
            }
        }

        GeneratedFiles = written.ToArray();

        // Remove bundles whose shader or technique no longer exists. The target globs this
        // directory into Compile, so a leftover bundle would compile forever (and a
        // recycled program name would define the same type twice).
        if (!failed && Directory.Exists(OutputDirectory))
        {
            var writtenPaths = new HashSet<string>(
                written.Select(item => Path.GetFullPath(item.ItemSpec)),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var stale in Directory.EnumerateFiles(OutputDirectory, "*.g.cs"))
            {
                if (writtenPaths.Contains(Path.GetFullPath(stale)))
                    continue;

                try
                {
                    File.Delete(stale);
                    Log.LogMessage(MessageImportance.Low, $"Removed stale shader bundle {stale}");
                }
                catch (IOException e)
                {
                    Log.LogWarning(null, null, null, stale, 0, 0, 0, 0, $"Could not remove stale shader bundle: {e.Message}");
                }
            }
        }

        if (failed)
            return false;

        if (StampFile.Length > 0)
        {
            // The stamp that matches what this batch actually carries, not the one the target
            // expected: a native that is present but cannot be loaded passes a file test and then
            // produces no WGSL, and stamping the expected name there keeps bundles no WebGPU
            // pipeline can be created from. The other stamp is removed so a tree can never hold
            // both and be decided by whichever one MSBuild happened to compare.
            var carriesWgsl = WgslCompiler.Available;
            var achieved = carriesWgsl || NoWgslStampFile.Length == 0 ? StampFile : NoWgslStampFile;
            var superseded = carriesWgsl ? NoWgslStampFile : StampFile;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(achieved))!);
            File.WriteAllText(achieved, $"{DateTime.UtcNow:O}\n");

            if (superseded.Length > 0
                && !string.Equals(superseded, achieved, StringComparison.Ordinal)
                && File.Exists(superseded))
            {
                try
                {
                    File.Delete(superseded);
                }
                catch (IOException e)
                {
                    Log.LogWarning(
                        null,
                        null,
                        null,
                        superseded,
                        0,
                        0,
                        0,
                        0,
                        $"Could not remove the superseded shader stamp: {e.Message}"
                    );
                }
            }
        }

        Log.LogMessage(
            MessageImportance.Normal,
            $"Compiled {Shaders.Length} shader source(s) into {GeneratedFiles.Length} bundle(s)."
        );
        return true;
    }
}
