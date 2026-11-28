using System.Text.Json;

namespace NFMWorld.Updates;

internal static class InstallTransaction
{
    private sealed record Change(string Path, string? Before, string? After);

    internal static void Apply(
        string root,
        string stage,
        InstallManifest old,
        InstallManifest next,
        Action<string> log,
        Action<string>? beforeWrite = null
    )
    {
        var changes = new List<Change>();
        // Refuse case/normalization-only renames across versions, including parent directories.
        var names = new PackagePaths();
        foreach (var path in old.Files.Keys.Concat(next.Files.Keys).Distinct(StringComparer.Ordinal))
            names.Add(path, false);

        foreach (var (name, incoming) in next.Files)
        {
            var path = InstallLayout.CheckedPath(root, name);
            old.Files.TryGetValue(name, out var previous);
            var actual = ExistingHash(path);

            if (actual is not null && (previous is null || (actual != previous && actual != incoming)))

                throw new InvalidDataException($"Update would overwrite a local file: {name}");
            if (actual != incoming)
                changes.Add(new(name, actual, incoming));
        }

        foreach (var (name, previous) in old.Files)
        {
            if (next.Files.ContainsKey(name))

                continue;
            if (ExistingHash(InstallLayout.CheckedPath(root, name)) == previous)
                changes.Add(new(name, previous, null));
        }

        var app = InstallLayout.Apphost(old.Rid);

        if (changes.FindIndex(c => c.Path == app) is var index && index >= 0)
        {
            var change = changes[index];
            changes.RemoveAt(index);
            changes.Add(change);
        }

        changes.Add(new(
            InstallLayout.Manifest,
            InstallLayout.Hash(InstallLayout.CheckedPath(root, InstallLayout.Manifest)),
            InstallLayout.Hash(InstallLayout.CheckedPath(stage, InstallLayout.Manifest))
        )
        );

        var workspace = InstallLayout.CheckedPath(root, InstallLayout.Workspace, true);

        if (InstallLayout
            .Attributes(workspace) is not null)

            throw new IOException($"Move the existing update workspace aside: {workspace}");
        Directory.CreateDirectory(workspace);
        var journal = InstallLayout.CheckedPath(root, InstallLayout.Journal);
        var applied = new List<Change>();
        var createdDirs = new List<string>();
        var keepWorkspace = false;

        try
        {
            foreach (var change in changes)
            {
                var live = VerifyLive(change, change.Before);
                if (change.Before is not null)
                    Copy(live, WorkspaceFile("backup", change.Path), change.Before);
                if (change.After is not null)
                    Copy(
                        InstallLayout.CheckedPath(stage, change.Path),
                        WorkspaceFile("new", change.Path),
                        change.After
                    );
            }

            var entries = changes.Select(c => new JournalEntry(c.Path, c.Before is not null)).ToArray();
            var recovery = JsonSerializer.SerializeToUtf8Bytes(
                new UpdateJournal(old.Tag, next.Tag, entries),
                UpdateJson.Default.UpdateJournal
            );

            if (recovery.Length > InstallLayout.MaxJournal)

                throw new InvalidDataException("Update journal is too large.");

            using (var file = new FileStream(journal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(recovery);
                file.Flush(flushToDisk: true);
            }

            log(
                $"Recovery if interrupted: use {journal}. Restore files from backup, remove only entries with hadOriginal=false, then move the workspace aside."
            );

            foreach (var change in changes)
            {
                beforeWrite?.Invoke(change.Path);
                var live = VerifyLive(change, change.Before);

                if (change.After is null)
                    File.Delete(live);
                else
                {
                    EnsureParents(change.Path);
                    live = VerifyLive(change, change.Before);
                    File.Move(WorkspaceFile("new", change.Path), live, overwrite: change.Before is not null);
                }

                applied.Add(change);
            }
        }
        catch
        {
            try
            {
                for (var i = applied.Count - 1; i >= 0; i--)
                {
                    var change = applied[i];
                    var live = VerifyLive(change, change.After);

                    if (change.Before is null)
                        File.Delete(live);
                    else
                    {
                        var backup = WorkspaceFile("backup", change.Path);
                        var restore = WorkspaceFile("restore", change.Path);
                        Copy(backup, restore, change.Before);
                        File.Move(restore, VerifyLive(change, change.After), overwrite: change.After is not null);
                    }
                }

                for (var i = createdDirs.Count - 1; i >= 0; i--)
                {
                    var dir = InstallLayout.CheckedPath(root, createdDirs[i], true);
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
            }
            catch (Exception ex)
            {
                keepWorkspace = true;
                log(ex.ToString());

                throw new IOException($"Update rollback failed. Recover from {journal} before starting the game.", ex);
            }

            throw;
        }
        finally
        {
            if (!keepWorkspace)
            {
                try
                {
                    File.Delete(InstallLayout.CheckedPath(root, InstallLayout.Journal));
                    Directory.Delete(InstallLayout.CheckedPath(root, InstallLayout.Workspace, true), recursive: true);
                }
                catch (Exception ex)
                {
                    log($"Could not clean {workspace}: {ex}");
                }
            }
        }

        string WorkspaceFile(string folder, string path) => InstallLayout.CheckedPath(
            root,
            $"{InstallLayout.Workspace}/{folder}/{path}"
        );

        string VerifyLive(Change change, string? expected)
        {
            var path = InstallLayout.CheckedPath(root, change.Path);

            if (ExistingHash(path) != expected)

                throw new IOException($"File changed during update: {change.Path}");

            return path;
        }

        void EnsureParents(string path)
        {
            for (var i = path.IndexOf('/'); i >= 0; i = path.IndexOf('/', i + 1))
            {
                var relative = path[..i];
                var directory = InstallLayout.CheckedPath(root, relative, true);

                if (InstallLayout.Attributes(directory) is not null)

                    continue;
                Directory.CreateDirectory(directory);
                createdDirs.Add(relative);
            }
        }
    }

    private static string? ExistingHash(string path) => InstallLayout.Attributes(path) is null ? null : InstallLayout.Hash(path);

    private static void Copy(string source, string target, string expected)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using var input = File.OpenRead(source);
            input.CopyTo(file);
            file.Flush(flushToDisk: true);
        }

        if (InstallLayout.Hash(target) != expected)

            throw new IOException($"File changed while staging: {source}");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(target, File.GetUnixFileMode(source) & (UnixFileMode)511);
    }
}
