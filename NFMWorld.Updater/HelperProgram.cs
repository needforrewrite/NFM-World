using System.Diagnostics;

namespace NFMWorld.Updates;

internal static class HelperProgram
{
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var root = InstallLayout.CanonicalRoot(options.Root);
            var old = InstallLayout.VerifyInstall(root, options.Current, options.Rid);
            InstallLayout.VerifyStartup(root, options.Current, options.Rid);
            var executable = InstallLayout.CheckedPath(root, InstallLayout.Apphost(options.Rid));
            var helper = Environment.ProcessPath ?? throw new IOException("Cannot locate the updater executable.");
            var temp = Path.GetDirectoryName(helper)!;
            if (InstallLayout.IsWithin(root, InstallLayout.CanonicalRoot(temp)) ||
                InstallLayout.Hash(helper) != old.Files[InstallLayout.Helper(options.Rid)])
                throw new InvalidDataException("Run the verified updater from outside the game installation.");
            using var game = Attach(options.GamePid, executable);
            using var launcher = options.LauncherPid is int pid ? Attach(pid, executable) : null;
            using var input = new StreamReader(Console.OpenStandardInput());
            using var client = ReleaseChannel.CreateClient();
            using var downloadDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var stage = InstallLayout.CreateTempOutside(root, "nfmw-stage-");
            try
            {
                var archive = Path.Combine(stage.FullName, "download");
                var payload = Path.Combine(stage.FullName, "payload");
                var next = await AwaitCommit(input, Status, async ct =>
                    {
                        var candidate = await ReleaseChannel.CheckAsync(client, options.Current, options.Rid, ct);
                        if (candidate?.Tag != options
                            .Target) throw new InvalidDataException("The release changed. Check for updates again.");
                        await PackageStaging.Download(client, candidate, archive, Status, ct);
                        return await PackageStaging.Extract(archive, payload, options.Target, options.Rid, ct);
                    }, game.WaitForExitAsync(downloadDeadline.Token), downloadDeadline.Token
                );
                if (next is null) return 0;
                return await CommitAndRestart(root, payload, options, next, game, launcher);
            }
            finally
            {
                try
                {
                    stage.Delete(recursive: true);
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }

                // A running Windows executable cannot remove its own copy.
                if (!OperatingSystem.IsWindows() && Path.GetFileName(temp).StartsWith("nfmw-update-", StringComparison.Ordinal))
                {
                    try
                    {
                        File.Delete(helper);
                        Directory.Delete(temp);
                    }
                    catch (IOException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Status("ERROR " + SingleLine(ex is OperationCanceledException ? "The update timed out." : ex.Message));
            return 1;
        }
    }

    internal static async Task<T?> AwaitCommit<T>(
        TextReader input,
        Action<string> status,
        Func<CancellationToken, Task<T>> prepare,
        Task gameExited,
        CancellationToken deadline
    ) where T : class
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline);
        var command = input.ReadLineAsync(cancel.Token).AsTask();
        var stage = prepare(cancel.Token);
        var timeout = Task.Delay(Timeout.InfiniteTimeSpan, cancel.Token);
        try
        {
            if (await Task.WhenAny(command, gameExited, timeout, stage) != stage || command.IsCompleted || gameExited.IsCompleted)
            {
                deadline.ThrowIfCancellationRequested();
                if (command.IsCompletedSuccessfully && command.Result is not (null or "CANCEL"))
                    throw new InvalidDataException("Unexpected updater command before verification.");
                return null;
            }

            var prepared = await stage;
            status("READY");
            // COMMIT is written before process exit; prefer the command over the exit notification.
            if (await Task.WhenAny(command, gameExited, timeout) != command && !command.IsCompleted)
            {
                deadline.ThrowIfCancellationRequested();
                if (!gameExited.IsCompleted) return null;
                // The parent has exited; the pipe must deliver its final line or EOF.
                await command.WaitAsync(TimeSpan.FromSeconds(2), cancel.Token);
            }

            return await command switch
            {
                "COMMIT" => prepared,
                null or "CANCEL" => null,
                _ => throw new InvalidDataException("Unexpected updater command.")
            };
        }
        finally
        {
            cancel.Cancel();
            if (!stage.IsCompletedSuccessfully)
                try
                {
                    await stage;
                }
                catch (Exception)
                { /* Already propagated, or preparation was cancelled. */
                }
        }
    }

    private static async Task<int> CommitAndRestart(
        string root,
        string payload,
        Options options,
        InstallManifest next,
        Process game,
        Process? launcher
    )
    {
        string? prefix = null;
        var restartTag = options.Current;
        var failed = false;
        try
        {
            prefix = InstallLayout.StatePrefix(root);
            using var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await game.WaitForExitAsync(exitDeadline.Token);
            if (launcher is not null) await launcher.WaitForExitAsync(exitDeadline.Token);
            // Unix cannot report the exit code of a process that is not our child.
            if (OperatingSystem.IsWindows() && (game.ExitCode != 0 || launcher is { ExitCode: not 0 }))
                throw new IOException("The game did not exit cleanly.");
            using var lease = InstallLayout.Lease(prefix, exclusive: true);
            if (InstallLayout.CanonicalRoot(root) != root) throw new IOException("The installation moved during the update.");
            InstallLayout.VerifyStartup(root, options.Current, options.Rid);
            var old = InstallLayout.VerifyInstall(root, options.Current, options.Rid);
            InstallTransaction.Apply(root, payload, old, next, Log);
            restartTag = options.Target;
        }
        catch (Exception ex)
        {
            failed = true;
            Report(ex);
        }

        try
        {
            if (prefix is null || !game.HasExited || (launcher is not null && !launcher.HasExited)) return 1;
            using (InstallLayout.Lease(prefix, exclusive: true))
            {
                InstallLayout.VerifyStartup(root, restartTag, options.Rid);
                InstallLayout.VerifyInstall(root, restartTag, options.Rid);
            }

            var start = new ProcessStartInfo(InstallLayout.CheckedPath(root, InstallLayout.Apphost(options.Rid)))
            {
                WorkingDirectory = options.WorkingDirectory, UseShellExecute = false, StartDetached = true
            };
            start.Environment.Remove("NFMW_CRASH_DUMP_CHILD");
            start.Environment.Remove("NFMW_CRASH_DUMP_PARENT_PID");
            foreach (var arg in options.GameArgs) start.ArgumentList.Add(arg);
            using var restarted = Process.Start(start)
                                  ?? throw new IOException("Could not reopen the game. Please start it manually.");
        }
        catch (Exception ex)
        {
            Report(ex);
            return 1;
        }

        return failed ? 1 : 0;

        void Log(string text)
        {
            if (prefix is null || InstallLayout.Attributes(prefix + ".log") is { } attrs && (attrs & (FileAttributes.ReparsePoint
                                                                                                      | FileAttributes.Directory))
                != 0)
                throw new IOException("Updater log is unavailable.");
            File.AppendAllText(prefix + ".log", $"{DateTimeOffset.UtcNow:u} {text}{Environment.NewLine}");
        }

        void Report(Exception ex)
        {
            Console.Error.WriteLine(ex);
            try
            {
                Log(ex.ToString());
                var path = prefix + ".error.txt";
                if (InstallLayout.Attributes(path) is not { } attr || (attr & (FileAttributes.ReparsePoint | FileAttributes
                    .Directory)) == 0)
                    File.WriteAllText(
                        path,
                        SingleLine(ex is OperationCanceledException ? "Timed out waiting for the game to exit." : ex.Message)
                    );
            }
            catch (Exception logError)
            {
                Console.Error.WriteLine(logError);
            }
        }
    }

    internal static string SingleLine(string text) => string.Concat(text.Take(350).Select(c => char.IsControl(c) ? ' ' : c));

    private static void Status(string text)
    {
        Console.Out.WriteLine(text);
        Console.Out.Flush();
    }

    private static Process Attach(int pid, string executable)
    {
        var process = Process.GetProcessById(pid);
        try
        {
            _ = process.SafeHandle;
            if (process.MainModule?.FileName is not { } path ||
                !string.Equals(
                    InstallLayout.CanonicalRoot(Path.GetDirectoryName(path)!),
                    Path.GetDirectoryName(executable),
                    InstallLayout.PathComparison
                ) ||
                !string.Equals(Path.GetFileName(path), Path.GetFileName(executable), InstallLayout.PathComparison) || process.HasExited)
                throw new InvalidDataException("The game process is not running from this installation.");
            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private sealed record Options(
        string Root,
        string Current,
        string Target,
        string Rid,
        int GamePid,
        int? LauncherPid,
        string WorkingDirectory,
        string[] GameArgs)
    {
        internal static Options Parse(string[] args)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var i = 0;
            for (; i < args.Length && args[i] != "--"; i += 2)
            {
                var flag = args[i];
                if (i + 1 >= args.Length || flag is not ("--release-url" or "--update-path" or "--current-tag" or "--target-tag"
                    or "--rid" or "--game-pid" or "--launcher-pid" or "--working-directory") || !fields.TryAdd(flag, args[i + 1]))
                    throw new InvalidDataException("Invalid updater arguments.");
            }

            if (i == args.Length || Required("--release-url") != ReleaseChannel.Latest ||
                ReleaseChannel.ParseTag(Required("--current-tag")) is not { } current ||
                ReleaseChannel.ParseTag(Required("--target-tag")) is not { } target || target <= current ||
                Required("--rid") != ReleaseChannel.ProcessRid || !Path.IsPathFullyQualified(Required("--working-directory")))
                throw new InvalidDataException("Invalid updater arguments.");
            int game = Pid("--game-pid");
            int? launcher = fields.ContainsKey("--launcher-pid") ? Pid("--launcher-pid") : null;
            if (launcher is not null && (!OperatingSystem.IsWindows() || launcher
                                         == game)) throw new InvalidDataException("Invalid launcher PID.");
            return new(
                Required("--update-path"),
                Required("--current-tag"),
                Required("--target-tag"),
                Required("--rid"),
                game,
                launcher,
                Required("--working-directory"),
                args[(i + 1)..]
            );

            string Required(string key) => fields.TryGetValue(key, out var value) && value.Length
                                           > 0 ? value : throw new InvalidDataException($"Missing {key}.");

            int Pid(string key) => int.TryParse(Required(key), System.Globalization.NumberStyles.None, null, out var pid) && pid
                                   > 0 && pid != Environment.ProcessId
                ? pid : throw new InvalidDataException($"Invalid {key}.");
        }
    }
}
