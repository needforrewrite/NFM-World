using System.Diagnostics;
using System.Text.Json;

namespace NFMWorld.Archive.Validation;

public sealed record ValidatorResult(bool Ok, JsonElement? Output, string? Error);

/// <summary>
/// Runs the out-of-process validator. The process boundary is the safety property: the time-trial
/// simulator is synchronous and cannot be cancelled, so the only way to bound it is to kill it.
/// </summary>
public sealed class ValidatorRunner(ArchiveOptions options, ILogger<ValidatorRunner> logger)
{
    /// <summary>
    /// Each run loads the game data and a plugin, so an unbounded number of concurrent uploads
    /// is a trivial way to exhaust the host. One process at a time is the honest limit for a
    /// single-node archive; raise it only with the memory to match.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ValidatorResult> RunAsync(
        string command, IReadOnlyList<string> args, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(30), ct))
            return new ValidatorResult(false, null, "The validator is busy; try again shortly.");

        try
        {
            return await RunCoreAsync(command, args, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ValidatorResult> RunCoreAsync(
        string command, IReadOnlyList<string> args, CancellationToken ct)
    {
        var executable = options.ValidatorPath;
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
            return new ValidatorResult(false, null, "Validator is not configured on this server.");

        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = options.ValidatorWorkingDirectory ?? Path.GetDirectoryName(executable)!
        };

        // The validator parses untrusted bytes, so it must not inherit the archive's secrets:
        // the trusted-server HMAC key and the Discord secret both arrive as environment
        // variables, and anything that reads this process's environment would get them. Only
        // the bare minimum the runtime needs to start a process is passed through.
        startInfo.Environment.Clear();
        foreach (var name in new[] { "PATH", "PATHEXT", "SystemRoot", "windir", "TEMP", "TMP", "HOME" })
            if (Environment.GetEnvironmentVariable(name) is { } value)
                startInfo.Environment[name] = value;

        using var process = new Process();
        process.StartInfo = startInfo;
        process.StartInfo.ArgumentList.Add(command);
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            if (!process.Start())
                return new ValidatorResult(false, null, "Validator could not be started.");

            // Reading both pipes concurrently: a validator that writes more than a pipe buffer
            // would otherwise deadlock before it exits.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(options.ValidationTimeout);
            using var registration = timeout.Token.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            });

            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
                return new ValidatorResult(true, Parse(stdout), null);

            logger.LogInformation("Validator refused {Command} (exit {Code}): {Output}", command, process.ExitCode, stdout);
            return new ValidatorResult(false, Parse(stdout), Parse(stdout)?.TryGetProperty("error", out var e) == true
                ? e.GetString()
                : string.IsNullOrWhiteSpace(stderr) ? "Validation failed." : Truncate(stderr));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Validator timed out on {Command}", command);
            return new ValidatorResult(false, null, "Validation timed out.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Validator failed to run {Command}", command);
            return new ValidatorResult(false, null, "Validation could not be completed.");
        }
    }

    private static JsonElement? Parse(string stdout)
    {
        // The validator prints exactly one JSON object; anything else means it crashed early.
        var line = stdout.Trim().Split('\n').LastOrDefault()?.Trim();
        if (string.IsNullOrEmpty(line))
            return null;
        try { return JsonDocument.Parse(line).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static string Truncate(string value)
        => value.Length <= 256 ? value : value[..256];
}
