// LLM maintained.
//
// HLSL -> SPIR-V -> WGSL, the last step being taint - a packaged build of Dawn's Tint.
//
// The SPIR-V is already there - it is the same module the other backends are cross-compiled from -
// so only two things happen here, and both exist because of rules WebGPU has and the other targets
// do not:
//
// 1. Binding numbers are reassigned first. HLSL's register spaces do not survive glslang in a form
//    WebGPU can bind: b0, t0 and s0 all land on set 0 binding 0, and two resources on one binding is
//    a validation error. The one block, then every texture, then every sampler, in that order, is
//    also the layout the backend's bind group is created from.
// 2. taint's output needs two repairs before naga will read it (see PostProcess).
//
// Neither is a workaround for a bad shader; both are properties of the tool chain this tree pins,
// which is why they live here rather than in the `.fx` sources.
//
// taint is only ever loaded by a child process (see WgslTranslator). It aborts on failure rather
// than returning, and under TaskHostFactory the abort takes the shader build with it - macOS did
// exactly that, with no message and no library named.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TaintSharp;

namespace NFMWorld.ShaderCompiler;

/// <summary>Runs taint over one stage's SPIR-V and returns WGSL.</summary>
public static class WgslCompiler
{
    // SPIR-V opcodes and decorations this file reads. Only decorations are rewritten, so the rest of
    // the module is left byte-for-byte alone.
    private const int OpDecorate = 71;
    private const int DecorationBinding = 33;
    private const int DecorationDescriptorSet = 34;

    /// <summary>
    /// How long the translator may take for one stage before it is treated as dead.
    ///
    /// Generous on purpose: this is a liveness bound on a native abort or a wedged process, not a
    /// performance budget. Sixteen stages of a shader tree take a few seconds on the slowest host
    /// measured, so anything near this is a hang.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(120);

    private static readonly object TranslatorGate = new();
    private static WgslTranslatorHost? Translator;
    private static bool TranslatorStarted;
    private static string? TranslatorFailure;

    /// <summary>
    /// Whether WGSL can be produced on this host, i.e. whether <see cref="Compile"/> can run; when
    /// it is false, <see cref="UnavailableReason"/> says why and the caller omits WGSL.
    /// </summary>
    public static bool Available => UnavailableReason is null;

    /// <summary>
    /// Why WGSL cannot be produced on this host, or null when it can.
    ///
    /// Asked of a child process, because the answer on this one is not obtainable: TaintSharp
    /// packages win-x64, linux-x64 and osx-arm64 only, and a translator that is absent, unloadable or
    /// aborting has to leave WGSL off with a warning rather than fail a build - WGSL is consumed only
    /// by the WebGPU backend, which is opt-in on the desktop.
    ///
    /// The answer can change mid-batch: the process that passed the probe can still abort on a
    /// module, and from then on this reports that instead. What it cannot do is go back to null -
    /// a translator that died is not restarted, so a tree that lost it stamps as WGSL-less.
    /// </summary>
    public static string? UnavailableReason
    {
        get
        {
            lock (TranslatorGate)
            {
                if (TranslatorFailure is not null)
                    return TranslatorFailure;

                if (!TranslatorStarted)
                {
                    TranslatorStarted = true;
                    Translator = StartTranslator();
                }

                return Translator is null
                    ? TranslatorFailure ?? "the WGSL translator is not available"
                    : null;
            }
        }
    }

    /// <summary>
    /// Translates one stage, producing both WGSL flavours.
    /// </summary>
    /// <param name="spirv">The stage's SPIR-V, as shaderc emitted it.</param>
    /// <param name="bindings">
    /// The WGSL binding each decorated SPIR-V variable must carry. Every variable that carries a
    /// binding decoration has to appear - a resource left out would keep a number that collides with
    /// another, which WebGPU reports at pipeline creation with nothing to say which shader.
    /// </param>
    /// <param name="entryPoint">The stage's entry point, used in error messages only: taint
    /// translates the whole module, and the module this is handed has that one entry point.</param>
    /// <returns>
    /// <c>Desktop</c> is what naga (wgpu-native) reads; <c>Browser</c> keeps the
    /// <c>diagnostic(off, derivative_uniformity);</c> directive the browser's Tint requires. Only that
    /// line differs, and tint runs once for both, so carrying the pair costs build time and a
    /// compressed member, not a second translation.
    /// </returns>
    public static (string Desktop, string Browser) Compile(
        byte[] spirv,
        IReadOnlyDictionary<int, int> bindings,
        string entryPoint
    )
    {
        if (UnavailableReason is { } reason)
            throw new WgslUnavailableException($"WGSL cannot be produced on this host: {reason}");

        // Per-module failures are the shader's, so they throw; a translator that stopped answering
        // is the host's, and it rethrows as "unavailable" so the rest of the batch skips WGSL
        // instead of failing every shader.
        var reply = Request(WgslProtocol.RequestTranslate, writer =>
            {
                writer.Write(spirv.Length);
                writer.Write(spirv);
                WgslFrames.WriteString(writer, entryPoint);
                writer.Write(bindings.Count);
                foreach (var (variable, slot) in bindings)
                {
                    writer.Write(variable);
                    writer.Write(slot);
                }
            }
        );

        switch (reply?.Status)
        {
            case WgslProtocol.StatusOk:
                return (reply.Value.First, reply.Value.Second);
            case WgslProtocol.StatusFailed:
                throw new InvalidOperationException(
                    $"SPIR-V -> WGSL failed for entry point '{entryPoint}':\n{reply.Value.First.Trim()}"
                );
            case WgslProtocol.StatusUnavailable:
                throw new WgslUnavailableException($"WGSL cannot be produced on this host: {Stop(reply.Value.First)}");
            default:
                // No reply at all: the process died, which is the case this whole file exists for.
                throw new WgslUnavailableException($"WGSL cannot be produced on this host: {Stop(null)}");
        }
    }

    /// <summary>
    /// The probe and one translation, in this process - the child's whole job.
    ///
    /// Reached only through <see cref="WgslTranslatorServer"/>, so an abort here ends a process that
    /// holds nothing but the translator, and the failure comes back to the build as an exit status
    /// plus stderr.
    /// </summary>
    internal static string? LocalUnavailableReason => LocalProbe.Value;

    private static readonly Lazy<string?> LocalProbe = new(ProbeCore, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static (string Desktop, string Browser) Translate(
        byte[] spirv,
        IReadOnlyDictionary<int, int> bindings,
        string entryPoint
    )
    {
        if (LocalUnavailableReason is { } reason)
            throw new InvalidOperationException($"WGSL cannot be produced on this host: {reason}");

        // One option matters: the derivative-uniformity rule is a WGSL restriction that predates the
        // shaders in this tree, and six fragment stages sample under divergent control flow. Without
        // it those stages fail to translate at all; with it the output carries a directive that is
        // stripped for the desktop flavour below.
        using var options = new TaintOptions();
        options.SetOption(TaintOption.AllowNonUniformDerivatives, true);

        var retargeted = RetargetBindings(spirv, bindings);

        using var module = Taint.Translate(retargeted, options);
        var wgsl = module.GetWgsl();
        if (wgsl is null)
        {
            throw new InvalidOperationException(
                $"SPIR-V -> WGSL failed for entry point '{entryPoint}'"
                + (module.GetDiagnostics() is { } diagnostics ? $":\n{diagnostics.Trim()}" : ".")
            );
        }

        return (PostProcess(wgsl, keepDiagnostic: false), PostProcess(wgsl, keepDiagnostic: true));
    }

    /// <summary>
    /// Removes the two constructs desktop naga will not read.
    ///
    /// <c>diagnostic(off, derivative_uniformity);</c> is what
    /// <see cref="TaintOption.AllowNonUniformDerivatives"/> emits instead of failing, and it is a
    /// directive the naga inside the pinned wgpu-native does not know at all - but the Tint inside the
    /// browser's emdawnwebgpu does, and without it every pixel stage with divergent sampling fails to
    /// translate there. So the two flavours differ only in this line, and
    /// <paramref name="keepDiagnostic"/> picks which one is being produced.
    /// The <c>return;</c> after a <c>discard;</c> is tint's demote-to-helper expansion
    /// leaving the rest of the helper body unreachable, which is valid WGSL and rejected by both
    /// compilers as "instruction after return". Dropping it only ever lets a discarded invocation run
    /// code whose stores go nowhere, which is what a discarded invocation is.
    /// </summary>
    private static string PostProcess(string wgsl, bool keepDiagnostic)
    {
        var text = keepDiagnostic
            ? wgsl
            : Regex.Replace(wgsl, @"^[ \t]*diagnostic\([^)]*\);[ \t]*\r?\n", "", RegexOptions.Multiline);
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

    private static WgslTranslatorHost? StartTranslator()
    {
        // One round trip before any shader is handed over, so a host with no translator says so once
        // instead of paying a dead process on the first module and reporting it mid-batch.
        var host = WgslTranslatorHost.Open(
            [WgslProtocol.RequestProbe],
            RequestTimeout,
            out var probe,
            out var error
        );

        if (host is null)
        {
            TranslatorFailure = error ?? "the WGSL translator process could not be started";
            return null;
        }

        if (probe!.Value.Status == WgslProtocol.StatusOk)
            return host;

        TranslatorFailure = probe.Value.First;
        host.Dispose();
        return null;
    }

    private static WgslTranslatorHost.Reply? Request(byte request, Action<BinaryWriter> body)
    {
        var host = Translator;
        if (host is null)
            return null;

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(request);
            body(writer);
        }

        return host.Request(buffer.ToArray(), RequestTimeout);
    }

    /// <summary>
    /// Records that the translator is gone, and how it went, so the rest of the batch skips WGSL.
    ///
    /// A reply the child gave before it gave up is used as-is; null means it never answered, and then
    /// only its exit status and stderr describe the cause - an abort writes nothing else.
    /// </summary>
    private static string Stop(string? said)
    {
        lock (TranslatorGate)
        {
            if (TranslatorFailure is not null)
                return TranslatorFailure;

            TranslatorFailure = said
                                ?? (Translator is { } host
                    ? $"the WGSL translator stopped answering: {host.DescribeFailure()}"
                    : "the WGSL translator process could not be started");

            Translator?.Dispose();
            Translator = null;
            return TranslatorFailure;
        }
    }

    /// <summary>
    /// Ends the translator and forgets it, so a later build in this process starts a fresh one.
    ///
    /// A batch must not restart a translator that died - that is what makes a failure stick - but the
    /// end of a batch is the other case: MSBuild keeps its task host nodes alive between builds, and
    /// the child only exits when its stdin is closed, so without this one translator process per node
    /// would sit idle for as long as the node lives.
    /// </summary>
    public static void Shutdown()
    {
        lock (TranslatorGate)
        {
            Translator?.Dispose();
            Translator = null;
            TranslatorStarted = false;
            TranslatorFailure = null;
        }
    }

    private static string? ProbeCore()
    {
        var name = OperatingSystem.IsWindows()
            ? "taint.dll"
            : OperatingSystem.IsMacOS()
                ? "libtaint.dylib"
                : "libtaint.so";

        var path = FindNative(name);
        if (path is null)
        {
            WgslTranslatorServer.Trace($"probe: {name} not found");
            return $"the taint native library ({name}) was not found beside the shader compiler";
        }

        try
        {
            WgslTranslatorServer.Trace($"probe: loading {path}");
            // The DllImport is a bare "taint", which .NET resolves against the app's own directory
            // rather than this assembly's. The resolver is per assembly and can only be installed
            // once, which is fine: one translator process probes once.
            NativeLibrary.SetDllImportResolver(typeof(Taint).Assembly, (libraryName, _, _) =>
                libraryName == "taint" ? NativeLibrary.Load(path) : IntPtr.Zero
            );
            _ = Taint.LibraryVersion;
            WgslTranslatorServer.Trace("probe: taint loaded");
            return null;
        }
        catch (Exception e) when (IsNativeLoadFailure(e))
        {
            return $"the taint native library could not be loaded: {RootCause(e).Message}";
        }
    }

    /// <summary>
    /// Whether a probe failure is this host's binding rather than the translator being broken.
    ///
    /// <see cref="Taint"/> loads the native from its own static constructor, so a bad file arrives
    /// as <see cref="TypeInitializationException"/> around the real error. Both shapes are
    /// recognised so a binding that cannot load leaves WGSL off with a warning instead of failing a
    /// build. A genuine translation failure is not covered here, because it happens later: the probe
    /// reads one exported symbol and nothing else.
    /// </summary>
    private static bool IsNativeLoadFailure(Exception e)
    {
        for (var cause = e; cause is not null; cause = cause.InnerException)
        {
            if (cause is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException
                or TypeLoadException or FileNotFoundException)
                return true;
        }

        return false;
    }

    private static Exception RootCause(Exception e)
    {
        var cause = e;
        while (cause.InnerException is { } inner)
            cause = inner;
        return cause;
    }

    /// <summary>
    /// The native library, from beside this assembly first.
    ///
    /// Beside the assembly rather than <see cref="AppContext.BaseDirectory"/> first because the shader
    /// build runs as an MSBuild task under <c>TaskHostFactory</c>, where the base directory is the
    /// SDK's own folder rather than the task's.
    /// </summary>
    private static string? FindNative(string fileName)
    {
        var overridePath = Environment.GetEnvironmentVariable("NFMW_TAINT");
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

/// <summary>
/// WGSL is not obtainable on this host, as opposed to a module that cannot be translated.
///
/// Separate from the plain <see cref="InvalidOperationException"/> a bad module throws because the
/// two want opposite handling: a broken shader fails the build, while a host without a working
/// translator only costs the tree its WGSL.
/// </summary>
public sealed class WgslUnavailableException(string message) : InvalidOperationException(message);
