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
    /// </summary>
    public string StampFile { get; set; } = "";

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

        if (failed)
            return false;

        if (StampFile.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(StampFile))!);
            File.WriteAllText(StampFile, $"{DateTime.UtcNow:O}\n");
        }

        Log.LogMessage(MessageImportance.Normal,
            $"Compiled {Shaders.Length} shader source(s) into {GeneratedFiles.Length} bundle(s).");
        return true;
    }
}
