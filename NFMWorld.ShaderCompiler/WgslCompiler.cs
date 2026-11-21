// LLM maintained.
//
// HLSL -> SPIR-V -> WGSL, the last step being Dawn's `tint` CLI.
//
// The SPIR-V is already there - it is the same module the other backends are cross-compiled from -
// so only two things happen here, and both exist because of rules WebGPU has and the other targets
// do not:
//
// 1. Binding numbers are reassigned first. HLSL's register spaces do not survive glslang in a form
//    WebGPU can bind: b0, t0 and s0 all land on set 0 binding 0, and two resources on one binding is
//    a validation error. The one block, then every texture, then every sampler, in that order, is
//    also the layout the backend's bind group is created from.
// 2. tint's output needs two repairs before naga will read it (see PostProcess).
//
// Neither is a workaround for a bad shader; both are properties of the tool chain this tree pins,
// which is why they live here rather than in the `.fx` sources.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NFMWorld.ShaderCompiler;

/// <summary>Runs tint over one stage's SPIR-V and returns WGSL.</summary>
public static class WgslCompiler
{
    // SPIR-V opcodes and decorations this file reads. Only decorations are rewritten, so the rest of
    // the module is left byte-for-byte alone.
    private const int OpDecorate = 71;
    private const int DecorationBinding = 33;
    private const int DecorationDescriptorSet = 34;

    /// <summary>
    /// Whether tint is present, i.e. whether <see cref="Compile"/> can run.
    /// </summary>
    public static bool Available => FindTint() is not null;

    /// <summary>
    /// Translates one stage.
    /// </summary>
    /// <param name="spirv">The stage's SPIR-V, as shaderc emitted it.</param>
    /// <param name="bindings">
    /// The WGSL binding each decorated SPIR-V variable must carry. Every variable that carries a
    /// binding decoration has to appear - a resource left out would keep a number that collides with
    /// another, which WebGPU reports at pipeline creation with nothing to say which shader.
    /// </param>
    /// <param name="entryPoint">The SPIR-V entry point to translate.</param>
    public static string Compile(byte[] spirv, IReadOnlyDictionary<int, int> bindings, string entryPoint)
    {
        var tint = FindTint()
                   ?? throw new InvalidOperationException(
                       "tint was not found. It ships from the Stride.Dependencies.Tint package and is "
                       + "copied beside the shader compiler's assembly by the build; if this is a "
                       + "hand-built tree, run the compiler through its project or put tint on PATH."
                   );

        var directory = Directory.CreateTempSubdirectory("nfmw-wgsl-");
        try
        {
            var input = Path.Combine(directory.FullName, "stage.spv");
            File.WriteAllBytes(input, RetargetBindings(spirv, bindings));

            var start = new ProcessStartInfo(tint)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("--input-format");
            start.ArgumentList.Add("spirv");
            start.ArgumentList.Add("--format");
            start.ArgumentList.Add("wgsl");
            // The derivative-uniformity rule is a WGSL 1.1 restriction that predates the shaders in
            // this tree; the directive that silences it is itself rejected by the naga this
            // wgpu-native links, so it is stripped again in PostProcess.
            start.ArgumentList.Add("--allow-non-uniform-derivatives");
            start.ArgumentList.Add("--entry-point");
            start.ArgumentList.Add(entryPoint);
            start.ArgumentList.Add(input);

            using var process = Process.Start(start)
                                ?? throw new InvalidOperationException("tint could not be started.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"SPIR-V -> WGSL failed for entry point '{entryPoint}' (tint exit {process.ExitCode}):\n"
                    + (stderr.Length > 0 ? stderr : stdout).Trim()
                );
            }

            return PostProcess(stdout);
        }
        finally
        {
            try
            {
                directory.Delete(recursive: true);
            }
            catch (IOException)
            {
                // A temp directory that outlives the build is not worth failing one over.
            }
        }
    }

    /// <summary>
    /// Removes the two constructs this naga will not read.
    ///
    /// <c>diagnostic(off, derivative_uniformity);</c> is what <c>--allow-non-uniform-derivatives</c>
    /// emits instead of failing, and it is a directive the naga inside the pinned wgpu-native does not
    /// know at all. The <c>return;</c> after a <c>discard;</c> is tint's demote-to-helper expansion
    /// leaving the rest of the helper body unreachable, which is valid WGSL and rejected by that same
    /// naga as "instruction after return". Dropping it only ever lets a discarded invocation run code
    /// whose stores go nowhere, which is what a discarded invocation is.
    /// </summary>
    private static string PostProcess(string wgsl)
    {
        var text = Regex.Replace(wgsl, @"^[ \t]*diagnostic\([^)]*\);[ \t]*\r?\n", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"(?m)^([ \t]*)discard;[ \t]*\r?\n[ \t]*return;[ \t]*\r?\n", "$1discard;\n");
        return text;
    }

    /// <summary>
    /// Rewrites the SPIR-V's binding decorations and forces every descriptor set to 0.
    ///
    /// A literal in place, so the module's instruction count and ids never change. Anything carrying a
    /// binding that the caller did not name is a mismatch between the reflection and the module, which
    /// is a compiler bug rather than a shader author's problem - hence the throw.
    /// </summary>
    private static byte[] RetargetBindings(byte[] spirv, IReadOnlyDictionary<int, int> bindings)
    {
        if (spirv.Length % 4 != 0)
            throw new InvalidOperationException($"SPIR-V length {spirv.Length} is not a multiple of 4.");

        var words = new uint[spirv.Length / 4];
        Buffer.BlockCopy(spirv, 0, words, 0, spirv.Length);

        if (words.Length < 5 || words[0] != 0x07230203)
            throw new InvalidOperationException("Not a SPIR-V module.");

        var seen = new HashSet<int>();
        for (var i = 5; i < words.Length;)
        {
            var count = (int)(words[i] >> 16);
            var opcode = (int)(words[i] & 0xffff);
            if (count == 0)
                throw new InvalidOperationException("Malformed SPIR-V: an instruction has a zero word count.");

            if (opcode == OpDecorate && count >= 4)
            {
                var target = (int)words[i + 1];
                var decoration = (int)words[i + 2];
                if (decoration == DecorationBinding)
                {
                    if (!bindings.TryGetValue(target, out var binding))
                        throw new InvalidOperationException(
                            $"SPIR-V variable %{target} carries a binding the reflection did not report; "
                            + "the two cannot be reconciled, and leaving it would collide with another "
                            + "resource on the same binding."
                        );

                    words[i + 3] = (uint)binding;
                    seen.Add(target);
                }
                else if (decoration == DecorationDescriptorSet)
                {
                    words[i + 3] = 0;
                }
            }

            i += count;
        }

        if (seen.Count != bindings.Count)
        {
            var missing = bindings.Keys.Where(id => !seen.Contains(id)).ToList();
            throw new InvalidOperationException(
                $"The reflection reported {bindings.Count} bound variable(s) but the module decorates "
                + $"{seen.Count}; missing: {string.Join(", ", missing.Select(id => "%" + id))}."
            );
        }

        var result = new byte[spirv.Length];
        Buffer.BlockCopy(words, 0, result, 0, result.Length);
        return result;
    }

    /// <summary>
    /// The tint executable, from beside this assembly first.
    ///
    /// Beside the assembly rather than <see cref="AppContext.BaseDirectory"/> first because the shader
    /// build runs as an MSBuild task under <c>TaskHostFactory</c>, where the base directory is the
    /// SDK's own folder rather than the task's.
    /// </summary>
    private static string? FindTint()
    {
        var fileName = OperatingSystem.IsWindows() ? "tint.exe" : "tint";

        var overridePath = Environment.GetEnvironmentVariable("NFMW_TINT");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return overridePath;

        foreach (var directory in new[]
                 {
                     Path.GetDirectoryName(typeof(WgslCompiler).Assembly.Location),
                     AppContext.BaseDirectory,
                 })
        {
            if (string.IsNullOrEmpty(directory))
                continue;
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (directory.Length == 0)
                continue;
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
