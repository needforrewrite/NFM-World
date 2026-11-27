using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using NFMWorld.UI;
using NFMWorld.Updates;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld;

public partial class WorldGame
{
    private static (string Root, string Tag, string Rid, string? State)? _updateInstall;
    private DesktopUpdates? _updates;

    static partial void AcquireUpdateLease(ref IDisposable? lease, ref bool blockStartup)
    {
        var metadata = typeof(WorldGame).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        var tag = metadata.FirstOrDefault(a => a.Key == "NfmwReleaseTag")?.Value;
        var rid = metadata.FirstOrDefault(a => a.Key == "NfmwRuntimeIdentifier")?.Value;
        if (tag is null || ReleaseChannel.ParseTag(tag) is null || rid is null || rid != ReleaseChannel.ProcessRid) return;
        var baseDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        string root;
        try
        {
            root = InstallLayout.CanonicalRoot(baseDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Desktop updates disabled: {ex.Message}");
            _updateInstall = (baseDir, tag, rid, null);
            return;
        }

        var journal = Path.Combine(root, InstallLayout.Journal);
        string? state = null;
        try
        {
            state = InstallLayout.StatePrefix(root);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Desktop updates disabled: {ex.Message}");
        }

        try
        {
            if (state is not null) lease = InstallLayout.Lease(state, exclusive: false);
            InstallLayout.VerifyStartup(root, tag, rid);
            // A launch can load an old apphost just before a different process commits the new package.
            var manifestPath = Path.Combine(root, InstallLayout.Manifest);
            if (InstallLayout.Attributes(manifestPath) is { } attrs && (attrs & (FileAttributes.Directory | FileAttributes
                .ReparsePoint)) == 0)
            {
                InstallManifest? manifest = null;
                try
                {
                    manifest = InstallLayout.ReadJson(manifestPath, UpdateJson.Default.InstallManifest);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not read installed version: {ex.Message}");
                }

                if (manifest is { Format: 1, Product: "NFMWorld" } && manifest.Rid == rid &&
                    ReleaseChannel.ParseTag(manifest.Tag) is not null && manifest.Tag != tag)
                    throw new InvalidDataException(
                        "This apphost does not match the installed game version. Restart from the extracted game folder."
                    );
            }

            _updateInstall = (root, tag, rid, state);
        }
        catch (Exception ex)
        {
            blockStartup = true;
            Console.Error.WriteLine($"Cannot start NFM World: {ex.Message}. If an update was interrupted, see {journal}.");
        }
    }

    partial void InitializeUpdates(string[] args)
    {
        if (_updateInstall is not { } install) return;
        _updates = new DesktopUpdates(this, install, args, System.Environment.CurrentDirectory);
        GameSparker.CheckForDesktopUpdates = _updates.CheckManually;
        GameSparker.PumpDesktopUpdates = _updates.Pump;
    }

    partial void FinishUpdates(bool cleanShutdown, ref bool updaterOwnsRestart)
    {
        GameSparker.CheckForDesktopUpdates = null;
        GameSparker.PumpDesktopUpdates = null;
        updaterOwnsRestart = _updates?.Finish(cleanShutdown) == true;
    }

    private sealed class DesktopUpdates(
        WorldGame game,
        (string Root, string Tag, string Rid, string? State) install,
        string[] args,
        string workingDirectory)
    {
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Signal<string> _progressText = new("Preparing download…");
        private Task<string?>? _previousError;
        private Task<(ReleaseCandidate? Candidate, string? Error)>? _check;
        private bool _started, _manual, _installAccepted;
        private HelperSession? _session;
        private (Func<bool> IsCurrent, Action Close)? _progress;
        private (Func<bool> IsCurrent, Action Close)? _installDialog;

        private static MessageWindow Dialog => GameSparker.MessageWindow;

        internal void CheckManually()
        {
            if (_session is not null) return;
            _manual = true;
            // Start from Pump, so the same menu and overlay gates apply to both checks.
        }

        internal void Pump()
        {
            if (_session is { } session)
            {
                if ((_progress is { } progress && !progress.IsCurrent()) ||
                    (_installDialog is { } confirmation && !confirmation.IsCurrent() && !_installAccepted))
                {
                    Cancel();
                    return;
                }

                while (session.Events.TryDequeue(out var status))
                {
                    if (status == "READY" && _progress is { } ready)
                    {
                        ready.Close();
                        _progress = null;
                        Dialog.ShowYesNo("Install update", $"Restart NFM World to install {session.Tag}?", result =>
                            {
                                _installDialog = null;
                                if (result != MessageWindow.MessageResult.Yes)
                                {
                                    Cancel();
                                    return;
                                }

                                if (!session.CanCommit)
                                {
                                    Cancel();
                                    Dialog.ShowMessage("Update failed", "The updater closed. Check for updates again.");
                                    return;
                                }

                                _installAccepted = true;
                                game.Window.RequestClose();
                            }
                        );
                        _installDialog = Dialog.TrackCurrent();
                    }
                    else if (status.StartsWith("ERROR ", StringComparison.Ordinal))
                    {
                        Cancel();
                        if (!Dialog.IsOpen) Dialog.ShowMessage("Update failed", status[6..]);
                        return;
                    }
                    else if (status == "VERIFY" && _progress is not null) _progressText.Value = "Verifying download…";
                    else if (_progress is not null && TryProgress(status, out var percent)) _progressText.Value =
                        $"Downloading… {percent}%";
                    else
                    {
                        Cancel();
                        if (!Dialog.IsOpen) Dialog.ShowMessage("Update failed", "The updater sent an invalid response.");
                        return;
                    }
                }

                return;
            }

            if (Dialog.IsOpen) return;
            if (!_started)
            {
                _previousError ??= Task.Run(ReadPreviousError);
                if (!_previousError.IsCompleted) return;
                _started = true;
                var error = _previousError.GetAwaiter().GetResult();
                _previousError = null;
                if (error is not null)
                {
                    Dialog.ShowMessage("Update failed", error);
                    return;
                }

                StartCheck();
            }
            else if (_check is null && _manual) StartCheck();

            if (_check is not { IsCompleted: true } result) return;
            _check = null;
            var (candidate, reason) = result.GetAwaiter().GetResult();
            var showResult = _manual;
            _manual = false;
            if (candidate is not null)
            {
                Dialog.ShowYesNo("Update available", $"Download NFM World {candidate.Tag}?", answer =>
                    {
                        if (answer != MessageWindow.MessageResult.Yes) return;
                        _progressText.Value = "Preparing download…";
                        _session = new HelperSession(
                            install.Root,
                            install.Tag,
                            install.Rid,
                            candidate.Tag,
                            args,
                            workingDirectory
                        );
                        _progress = Dialog.ShowProgress("Downloading update", () => _progressText.Value, Cancel);
                    }
                );
            }
            else if (showResult) Dialog.ShowMessage("Check for updates", reason ?? "NFM World is up to date.");
        }

        private void StartCheck() => _check = Task.Run(async () =>
            {
                try
                {
                    if (install.State is null || !IsGameApphost(System.Environment.ProcessPath, install.Root, install.Rid))
                        throw new InvalidDataException("This is not a supported portable installation.");
                    InstallLayout.VerifyInstall(install.Root, install.Tag, install.Rid);
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Update installation check: {ex.Message}");
                    return (null,
                        "This game folder is not a valid portable installation. Extract the stable release into a new folder.");
                }

                try
                {
                    using var client = ReleaseChannel.CreateClient();
                    return (await ReleaseChannel.CheckAsync(client, install.Tag, install.Rid, _lifetime.Token), (string?)null);
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Update check: {ex.Message}");
                    return (null, "Couldn't check for updates. Try again later.");
                }
            }
        );

        private string? ReadPreviousError()
        {
            if (install.State is null) return null;
            var path = install.State + ".error.txt";
            try
            {
                if (InstallLayout.Attributes(path) is not { } attrs || (attrs & (FileAttributes.ReparsePoint | FileAttributes
                    .Directory)) != 0)
                    return null;
                using var reader = new StreamReader(path);
                var buffer = new char[350];
                var length = reader.ReadBlock(buffer);
                reader.Dispose();
                File.Delete(path);
                return length == 0 ? null : new string(buffer, 0, length);
            }
            catch (Exception ex)
            {
                Logging.Warning($"Reading updater status: {ex.Message}");
                return null;
            }
        }

        private static bool TryProgress(string status, out long percent)
        {
            percent = 0;
            if (!status.StartsWith("DOWNLOAD ", StringComparison.Ordinal)) return false;
            var values = status.AsSpan(9);
            var split = values.IndexOf(' ');
            if (split < 1 || !long.TryParse(values[..split], NumberStyles.None, CultureInfo.InvariantCulture, out var received) ||
                !long.TryParse(values[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var total) ||
                total is <= 0 or > ReleaseChannel.MaxArchive || received > total) return false;
            percent = received * 100 / total;
            return true;
        }

        private void Cancel()
        {
            _session?.Cancel();
            _session = null;
            _progress?.Close();
            _progress = null;
            _installDialog?.Close();
            _installDialog = null;
        }

        internal bool Finish(bool clean)
        {
            _lifetime.Cancel();
            if (clean && _installAccepted && _session is { } session)
                return session.Commit();
            _session?.Cancel();
            return false;
        }
    }

    private static bool IsGameApphost(string? path, string root, string rid) => path is not null &&
        string.Equals(Path.GetFileName(path), InstallLayout.Apphost(rid), InstallLayout.PathComparison) &&
        string.Equals(InstallLayout.CanonicalRoot(Path.GetDirectoryName(path)!), root, InstallLayout.PathComparison);

    private sealed class HelperSession
    {
        private readonly object _pipeLock = new();
        private StreamWriter? _pipe;
        private bool _cancelled, _ready;
        internal ConcurrentQueue<string> Events { get; } = new();
        internal string Tag { get; }

        internal bool CanCommit
        {
            get { lock (_pipeLock) return _ready && _pipe is not null && !_cancelled; }
        }

        internal HelperSession(string root, string tag, string rid, string target, string[] args, string workingDirectory)
        {
            Tag = target;
            _ = Task.Run(() => Run(root, tag, rid, args, workingDirectory));
        }

        internal void Cancel()
        {
            lock (_pipeLock) _cancelled = true;
            _ = Task.Run(() => Send("CANCEL"));
        }

        internal bool Commit() => Send("COMMIT");

        private bool Send(string command)
        {
            lock (_pipeLock)
            {
                if (_pipe is not { } pipe || (command == "COMMIT" && (!_ready || _cancelled))) return false;
                _pipe = null;
                try
                {
                    using (pipe)
                    {
                        pipe.WriteLine(command);
                        pipe.Flush();
                    }

                    return true;
                }
                catch (IOException ex)
                {
                    Logging.Warning($"Updater pipe: {ex.Message}");
                    return false;
                }
            }
        }

        private async Task Run(string root, string tag, string rid, string[] args, string workingDirectory)
        {
            DirectoryInfo? temp = null;
            try
            {
                var manifest = InstallLayout.VerifyInstall(root, tag, rid);
                temp = InstallLayout.CreateTempOutside(root, "nfmw-update-");
                var name = InstallLayout.Helper(rid);
                var helper = Path.Combine(temp.FullName, name);
                var source = InstallLayout.CheckedPath(root, name);
                File.Copy(source, helper);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(helper, File.GetUnixFileMode(source) & (UnixFileMode)511);
                if (InstallLayout.Hash(helper) != manifest
                    .Files[name]) throw new InvalidDataException("The updater was modified.");
                var start = new ProcessStartInfo(helper)
                {
                    WorkingDirectory = temp.FullName, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardInputEncoding = new System.Text.UTF8Encoding(false),
                    StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                foreach (var arg in new[]
                {
                    "--release-url", ReleaseChannel.Latest, "--update-path", root, "--current-tag", tag,
                    "--target-tag", Tag, "--rid", rid, "--game-pid", System.Environment.ProcessId.ToString(
                        CultureInfo.InvariantCulture
                    ),
                    "--working-directory", workingDirectory
                }) start.ArgumentList.Add(arg);
                if (OperatingSystem.IsWindows() && System.Environment.GetEnvironmentVariable(CrashDumpChildVariable) == "1")
                {
                    var rawPid = System.Environment.GetEnvironmentVariable(CrashDumpParentVariable);
                    if (!int.TryParse(rawPid, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid == System
                        .Environment.ProcessId)
                        throw new InvalidDataException("Cannot identify the game's crash-dump launcher.");
                    using var parent = Process.GetProcessById(pid);
                    if (!IsGameApphost(
                        parent.MainModule?.FileName,
                        root,
                        rid
                    )) throw new InvalidDataException("The crash-dump launcher does not match this installation.");
                    start.ArgumentList.Add("--launcher-pid");
                    start.ArgumentList.Add(rawPid!);
                }

                start.ArgumentList.Add("--");
                foreach (var arg in args) start.ArgumentList.Add(arg);
                using var process = new Process { StartInfo = start };
                lock (_pipeLock)
                {
                    if (_cancelled) return;
                    if (!process.Start()) throw new IOException("The updater did not start.");
                    _pipe = process.StandardInput;
                }

                var stderr = DrainErrors(process.StandardError);
                try
                {
                    var explicitError = false;
                    while (await process.StandardOutput.ReadLineAsync() is { } line)
                    {
                        lock (_pipeLock)
                        {
                            if (_cancelled) continue;
                            if (line == "READY") _ready = true;
                            else if (line.StartsWith("ERROR ", StringComparison.Ordinal))
                            {
                                _ready = false;
                                explicitError = true;
                            }

                            Events.Enqueue(line);
                        }
                    }

                    if (!explicitError) Events.Enqueue("ERROR The updater closed before installation.");
                }
                finally
                {
                    // If the status pipe fails, close stdin so the helper cannot remain staged indefinitely.
                    lock (_pipeLock)
                    {
                        _ready = false;
                        _pipe?.Dispose();
                        _pipe = null;
                    }

                    await process.WaitForExitAsync();
                    await stderr;
                }
            }
            catch (Exception ex)
            {
                Logging.Warning($"Updater: {ex}");
                Events.Enqueue($"ERROR {ex.Message}");
            }
            finally
            {
                if (temp is not null)
                    try
                    {
                        if (temp.Exists) temp.Delete(recursive: true);
                    }
                    catch (Exception ex)
                    {
                        Logging.Warning($"Updater temporary files: {ex.Message}");
                    }
            }
        }

        private static async Task DrainErrors(StreamReader stream)
        {
            while (await stream.ReadLineAsync() is { } line) Logging.Warning($"Updater: {line}");
        }
    }
}
