// LLM maintained.
//
// The WGSL translator is a child process, not part of the build's own.
//
// taint is native code, and native code fails by aborting the process it lives in. MSBuild reports
// that as MSB4217 "Task host node exited prematurely" and names no library, which is what the macOS
// runner did while the same dylib loads and translates on Windows and Linux; nothing managed runs on
// the way out of an abort, so no amount of narrowing the in-process probe can make it safe. The build
// therefore only ever talks to the translator over a pipe. The child can die and the shader build
// still finishes, with the child's exit status and stderr as the reason the bundles carry no WGSL.
//
// One child serves the whole task, started on the first use and kept for the batch. Closing its stdin
// ends it, so a killed build leaves no translator behind either.

using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace NFMWorld.ShaderCompiler;

internal static class WgslProtocol
{
    /// <summary>Asks whether this host can translate at all.</summary>
    public const byte RequestProbe = 0;

    /// <summary>Asks for one stage to be translated.</summary>
    public const byte RequestTranslate = 1;

    public const byte StatusOk = 0;

    /// <summary>This module cannot be translated; the translator itself is fine.</summary>
    public const byte StatusFailed = 1;

    /// <summary>No translator on this host; WGSL is off for the whole batch.</summary>
    public const byte StatusUnavailable = 2;

    public const int MaxFrameBytes = 64 * 1024 * 1024;
}

/// <summary>Length-prefixed frames over a pipe, in both directions.</summary>
internal static class WgslFrames
{
    public static void Write(Stream stream, byte[] body)
    {
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
        stream.Write(header);
        stream.Write(body);
        stream.Flush();
    }

    /// <summary>One frame, or null when the stream ended or handed back nonsense.</summary>
    public static byte[]? Read(Stream stream)
    {
        Span<byte> header = stackalloc byte[4];
        if (!ReadExactly(stream, header))
            return null;

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 0 || length > WgslProtocol.MaxFrameBytes)
            return null;

        var body = new byte[length];
        return ReadExactly(stream, body) ? body : null;
    }

    public static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length < 0 || length > WgslProtocol.MaxFrameBytes)
            throw new InvalidDataException($"A string of {length} byte(s) is out of range.");

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static bool ReadExactly(Stream stream, Span<byte> buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = stream.Read(buffer[read..]);
            if (count <= 0)
                return false;

            read += count;
        }

        return true;
    }
}

/// <summary>
/// The child half: one reply per request on stdin until the parent closes it.
///
/// Every translation is caught, because an exception escaping here ends the process and reports
/// whatever shader was in flight as "the translator exited". A module that cannot be translated is a
/// property of that module, so it is answered rather than thrown.
/// </summary>
internal static class WgslTranslatorServer
{
    public const string Argument = "--wgsl-server";

    public static int Run()
    {
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();

        while (WgslFrames.Read(input) is { } request)
        {
            var (status, first, second) = Handle(request);

            using var body = new MemoryStream();
            using (var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(status);
                WgslFrames.WriteString(writer, first);
                WgslFrames.WriteString(writer, second);
            }

            WgslFrames.Write(output, body.ToArray());
        }

        return 0;
    }

    private static (byte Status, string First, string Second) Handle(byte[] request)
    {
        using var reader = new BinaryReader(new MemoryStream(request));

        if (reader.ReadByte() == WgslProtocol.RequestProbe)
            return WgslCompiler.LocalUnavailableReason is { } reason
                ? (WgslProtocol.StatusUnavailable, reason, "")
                : (WgslProtocol.StatusOk, "", "");

        var spirv = reader.ReadBytes(reader.ReadInt32());
        var entryPoint = WgslFrames.ReadString(reader);
        var bindingCount = reader.ReadInt32();
        if (bindingCount < 0 || bindingCount > 4096)
            return (WgslProtocol.StatusFailed, $"{bindingCount} binding(s) is out of range.", "");

        var bindings = new Dictionary<int, int>(bindingCount);
        for (var i = 0; i < bindingCount; i++)
            bindings[reader.ReadInt32()] = reader.ReadInt32();

        // Checked per request, not once: a probe that failed leaves WGSL off for the whole batch, and
        // reporting that as "this shader is broken" would blame every shader in the tree.
        if (WgslCompiler.LocalUnavailableReason is { } noTranslator)
            return (WgslProtocol.StatusUnavailable, noTranslator, "");

        try
        {
            var (desktop, browser) = WgslCompiler.Translate(spirv, bindings, entryPoint);
            return (WgslProtocol.StatusOk, desktop, browser);
        }
        catch (Exception e)
        {
            return (WgslProtocol.StatusFailed, e.ToString(), "");
        }
    }
}

/// <summary>
/// The parent half: one long-lived translator process, and its failure as a message.
///
/// A request that cannot be answered - the child exited, aborted, or stopped reading - kills it and
/// returns null, which the caller turns into "WGSL is unavailable on this host" for the rest of the
/// task. It is never restarted: a translator that died on one module would die on the next, and each
/// attempt costs a process launch plus a timeout.
/// </summary>
internal sealed class WgslTranslatorHost : IDisposable
{
    private readonly Process _process;
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly StringBuilder _stderr = new();
    private readonly object _gate = new();
    private bool _closed;
    private string? _failure;

    private WgslTranslatorHost(Process process)
    {
        _process = process;
        _input = process.StandardInput.BaseStream;
        _output = process.StandardOutput.BaseStream;
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { } line)
                return;

            lock (_stderr)
                _stderr.AppendLine(line);
        };
        process.BeginErrorReadLine();
    }

    /// <summary>
    /// Starts the translator that sits beside this assembly, as its own process.
    ///
    /// Which executable is host-specific and not something this side can assume: the apphost exists
    /// wherever the SDK produced one beside the assembly, and the same assembly run through the MSBuild
    /// host's own <c>dotnet</c> covers the rest - including a copied apphost that lost its executable
    /// bit. Both are tried, and both failures are reported.
    /// </summary>
    public static WgslTranslatorHost? Start(out string error)
    {
        var directory = Path.GetDirectoryName(typeof(WgslCompiler).Assembly.Location);
        if (string.IsNullOrEmpty(directory))
        {
            error = "the shader compiler assembly has no directory to look for the WGSL translator in";
            return null;
        }

        var failures = new List<string>();
        foreach (var info in StartInfos(directory))
        {
            try
            {
                var process = Process.Start(info);
                if (process is null)
                {
                    failures.Add($"{info.FileName} did not start");
                    continue;
                }

                error = "";
                return new WgslTranslatorHost(process);
            }
            catch (Exception e) when (e is Win32Exception or IOException or PlatformNotSupportedException)
            {
                failures.Add($"{info.FileName}: {e.Message}");
            }
        }

        error = $"the WGSL translator process could not be started ({string.Join("; ", failures)})";
        return null;
    }

    /// <summary>
    /// Sends one request, or null when the translator did not answer.
    ///
    /// The read runs on a worker so a translator that hangs cannot hang the build; the caller's
    /// timeout turns it into a dead translator like any other.
    /// </summary>
    public Reply? Request(byte[] body, TimeSpan timeout)
    {
        lock (_gate)
        {
            if (_closed)
                return null;

            try
            {
                WgslFrames.Write(_input, body);

                var pending = Task.Run(() => WgslFrames.Read(_output));
                if (!pending.Wait(timeout) || pending.Result is not { } frame)
                {
                    // The read is still parked on a stream this side is about to close; its failure
                    // is this method's already, and letting it reach the finalizer would report it
                    // as an unobserved task exception instead.
                    pending.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    Dispose();
                    return null;
                }

                using var reader = new BinaryReader(new MemoryStream(frame));
                return new Reply(reader.ReadByte(), WgslFrames.ReadString(reader), WgslFrames.ReadString(reader));
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidDataException)
            {
                Dispose();
                return null;
            }
        }
    }

    /// <summary>
    /// What the translator did instead of answering, for the warning the build reports.
    ///
    /// Its stderr is the only place a native abort says anything at all, so it is carried verbatim
    /// rather than summarised. Recorded when the process is reaped, because this is read after that.
    /// </summary>
    public string DescribeFailure()
    {
        lock (_stderr)
            return _failure ??= Capture();
    }

    private string Capture()
    {
        string exit;
        try
        {
            exit = _process.HasExited ? $"exited with code {_process.ExitCode}" : "did not answer";
        }
        catch (InvalidOperationException)
        {
            exit = "did not answer";
        }

        var stderr = _stderr.ToString().Trim();
        return string.IsNullOrEmpty(stderr) ? exit : $"{exit}: {stderr}";
    }

    /// <summary>
    /// Ends the translator and releases the pipes.
    ///
    /// Closing stdin is what it waits on, so this is also the clean shutdown; a process that is already
    /// gone makes every step here a no-op.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
                return;

            _closed = true;
            try
            {
                if (!_process.HasExited)
                {
                    _process.StandardInput.BaseStream.Close();
                    if (!_process.WaitForExit(2000))
                        _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException
                                          or IOException)
            {
                // Already exited, or this platform will not let a build task reap it; either way
                // nothing is waiting on it any more.
            }

            // Read before the Process goes: an exit code and stderr are all a native abort leaves
            // behind, and the caller reports them after this returns.
            _failure ??= Capture();
        }

        _process.Dispose();
    }

    private static IEnumerable<ProcessStartInfo> StartInfos(string directory)
    {
        // The apphost first: it is the process the SDK produced beside this assembly, it needs no
        // muxer resolution, and it is what the task's own output directory holds. A copy that lost its
        // executable bit (a Unix checkout of the task output, an archive extracted without modes) is
        // the reason the framework-dependent form is tried as well, and its failure is reported.
        var apphost = Path.Combine(
            directory,
            OperatingSystem.IsWindows() ? "NFMWorld.ShaderCompiler.exe" : "NFMWorld.ShaderCompiler"
        );
        if (File.Exists(apphost))
            yield return Configure(new ProcessStartInfo(apphost), null, directory);

        var dll = Path.Combine(directory, "NFMWorld.ShaderCompiler.dll");
        if (File.Exists(dll))
        {
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            yield return Configure(new ProcessStartInfo(string.IsNullOrEmpty(host) ? "dotnet" : host), dll, directory);
        }
    }

    private static ProcessStartInfo Configure(ProcessStartInfo info, string? dll, string directory)
    {
        if (dll is not null)
            info.ArgumentList.Add(dll);
        info.ArgumentList.Add(WgslTranslatorServer.Argument);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.WorkingDirectory = directory;
        return info;
    }

    public readonly record struct Reply(byte Status, string First, string Second);
}
