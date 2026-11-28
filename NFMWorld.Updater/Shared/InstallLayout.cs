using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NFMWorld.Updates;

internal static class InstallLayout
{
    internal const string Manifest = "nfmworld-install.json", Logo = "data/images/logo.svg", Workspace = ".nfmw-update";
    internal const string Journal = Workspace + "/journal.json";
    internal const int MaxManifest = 4 << 20, MaxEntries = 50_000;
    internal const int MaxJournal = 8 << 20;
    internal static string Apphost(string rid) => rid == "win-x64" ? "NFMWorld.exe" : "NFMWorld";
    internal static string Helper(string rid) => rid == "win-x64" ? "NFMWorld.Updater.exe" : "NFMWorld.Updater";
    internal static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison
        .OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static string Key(string path) => path.Normalize().ToUpperInvariant();

    internal static bool Protected(string path) => path.Split('/')[0].Equals(Workspace, StringComparison.OrdinalIgnoreCase)
                                                   ||
        Under(path, "mods")
                                                   || Under(path, "data/cfg")
                                                   || Under(path, "data/music/custom");

    private static bool Under(string path, string prefix) => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                                                             ||
        path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    internal static void ValidateRelative(string path)
    {
        if (path.Length > 1024)

            throw new InvalidDataException("Package path is too long.");

        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0
                || part.EndsWith('.')
                || part.EndsWith(' ')
                || part.Any(
                    c => char.IsControl(c) || "\\:<>\"|?*".Contains(c)
                ))

                throw new InvalidDataException($"Unsafe package path: {path}");
            var stem = part.Split('.')[0].TrimEnd().ToUpperInvariant();

            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
                ||
                (stem.Length == 4
                 && (stem.StartsWith("COM", StringComparison.Ordinal)
                     || stem.StartsWith(
                         "LPT",
                         StringComparison.Ordinal
                     ))
                 &&
                 "0123456789¹²³".Contains(stem[3])))

                throw new InvalidDataException($"Reserved package path: {path}");
        }
    }

    internal static bool IsWithin(string root, string path)
    {
        var parent = Key(root);
        var candidate = Key(path);

        return candidate == parent
               || candidate.StartsWith(Path.EndsInDirectorySeparator(parent)
            ? parent : parent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    internal static string CanonicalRoot(string input)
    {
        if (!Path.IsPathFullyQualified(input)
            || (OperatingSystem.IsWindows()
                &&
            (input.Length < 3 || !char.IsAsciiLetter(input[0]) || input[1] != ':' || input.AsSpan(2).Contains(':')))
            ||
            input.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "." or ".."))

            throw new InvalidDataException("Expected an absolute local game installation path.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
        var volume = Path.GetPathRoot(full)!;

        if (full == volume || (OperatingSystem.IsWindows() && new DriveInfo(volume).DriveType is DriveType.Network))

            throw new InvalidDataException("Expected an existing local game installation directory.");
        var parts = full[volume.Length..].Split(Path.DirectorySeparatorChar);
        var current = volume;

        for (var i = 0; i < parts.Length; i++)
        {
            if (OperatingSystem.IsWindows())
                ValidateRelative(parts[i]);
            var info = new DirectoryInfo(Path.Combine(current, parts[i]));

            if (!info.Exists)

                throw new InvalidDataException("Game installation directory does not exist.");

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Only resolve OS/user parent aliases before establishing the root, never links below it.

                if (i == parts.Length - 1)

                    throw new InvalidDataException("Game installation is a linked directory.");
                current = info.ResolveLinkTarget(true)?.FullName
                          ?? throw new InvalidDataException("Cannot resolve install parent.");

                if (OperatingSystem.IsWindows()
                    && (current.Length < 3
                        || current[1] != ':'
                        ||
                    new DriveInfo(Path.GetPathRoot(current)!).DriveType is DriveType.Network))

                    throw new InvalidDataException("Game installation must be local.");
            }
            else
                current = info.FullName;
        }

        return current;
    }

    internal static FileAttributes? Attributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    internal static string CheckedPath(string root, string relative, bool directory = false)
    {
        ValidateRelative(relative);
        RejectType(root, Attributes(root) ?? throw new InvalidDataException("Installation directory disappeared."), true);
        var parts = relative.Split('/');
        var current = root;

        for (var i = 0; i < parts.Length; i++)
        {
            current = Path.Combine(current, parts[i]);
            if (Attributes(current) is { } attrs)
                RejectType(relative, attrs, i < parts.Length - 1 || directory);
        }

        if (!IsWithin(root, Path.GetFullPath(current)))

            throw new InvalidDataException("Package path escapes the installation.");

        return current;
    }

    private static void RejectType(string path, FileAttributes attrs, bool directory)
    {
        if ((attrs & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0
            || attrs.HasFlag(FileAttributes.Directory)
            != directory)

            throw new InvalidDataException($"Unexpected file type or link: {path}");
    }

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);

        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal static T ReadJson<T>(
        string path,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        int limit = MaxManifest
    )
    {
        using var stream = File.OpenRead(path);

        if (stream.Length > limit)

            throw new InvalidDataException("Update metadata is too large.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);

        if (stream.ReadByte() >= 0)

            throw new InvalidDataException("Update metadata changed while reading.");

        return JsonSerializer.Deserialize(bytes, type) ?? throw new InvalidDataException("Missing update metadata.");
    }

    internal static InstallManifest ReadManifest(string root, string tag, string rid)
    {
        var manifest = ReadJson(CheckedPath(root, Manifest), UpdateJson.Default.InstallManifest);

        if (manifest is not { Format: 1, Product: "NFMWorld", Files.Count: > 0 and <= MaxEntries }
            ||
            manifest.Tag != tag
            || manifest.Rid != rid
            || ReleaseChannel.ParseTag(tag) is null
            || ReleaseChannel.AssetName(rid) is null)

            throw new InvalidDataException("Installation manifest does not match the game release.");
        var names = new PackagePaths();

        foreach (var (path, hash) in manifest.Files)
        {
            if (Protected(path)
                || path.Equals(Manifest, StringComparison.OrdinalIgnoreCase)
                || !ReleaseChannel.IsHash(hash)
                || hash.Any(c => c is >= 'A' and <= 'F'))

                throw new InvalidDataException($"Invalid package inventory entry: {path}");
            names.Add(path, false);
        }

        foreach (var marker in new[] { Apphost(rid), Helper(rid), Logo })

            if (!manifest.Files
                .ContainsKey(marker))

                throw new InvalidDataException($"Missing game installation marker: {marker}");

        return manifest;
    }

    internal static InstallManifest VerifyInstall(string root, string tag, string rid)
    {
        var manifest = ReadManifest(root, tag, rid);

        foreach (var marker in new[] { Apphost(rid), Helper(rid), Logo })

            if (Hash(CheckedPath(root, marker))
                != manifest
                .Files[marker])

                throw new InvalidDataException($"Game installation marker was modified: {marker}");

        return manifest;
    }

    internal static DirectoryInfo CreateTempOutside(string root, string prefix)
    {
        var temp = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
        if (Directory.Exists(temp) && new DirectoryInfo(temp).ResolveLinkTarget(true) is { } resolved)
            temp = resolved.FullName;

        if (IsWithin(
            root,
            CanonicalRoot(temp)
        ))

            throw new IOException("The temporary directory is inside the game installation.");

        return Directory.CreateTempSubdirectory(prefix);
    }

    internal static string StatePrefix(string root)
    {
        var local = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData,
            System.Environment.SpecialFolderOption.DoNotVerify
        );

        if (string.IsNullOrEmpty(local))

            throw new IOException("Local application data directory is unavailable.");
        var ancestor = local;
        while (!Directory.Exists(ancestor))
            ancestor = Path.GetDirectoryName(ancestor)
                       ?? throw new IOException("Local application data directory is unavailable.");

        if (new DirectoryInfo(ancestor).ResolveLinkTarget(true) is { } resolved)
        {
            local = Path.Combine(resolved.FullName, Path.GetRelativePath(ancestor, local));
            ancestor = resolved.FullName;
        }

        var dir = Path.Combine(CanonicalRoot(ancestor), Path.GetRelativePath(ancestor, local), "NFMWorld", "updates");
        dir = Path.GetFullPath(dir);

        if (IsWithin(root, dir))

            throw new IOException("Updater state directory is inside the game installation.");
        var relative = Path.GetRelativePath(CanonicalRoot(ancestor), dir).Replace(Path.DirectorySeparatorChar, '/');
        _ = CheckedPath(CanonicalRoot(ancestor), relative, directory: true);
        Directory.CreateDirectory(dir);

        if (IsWithin(root, CanonicalRoot(dir)))

            throw new IOException("Updater state directory is inside the game installation.");

        return Path.Combine(dir, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Key(root)))));
    }

    internal static FileStream Lease(string prefix, bool exclusive)
    {
        var path = prefix + ".lock";

        if (Attributes(path) is null)
        {
            try
            {
                using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (IOException) when (Attributes(path) is not null)
            {
            }
        }

        if (Attributes(path) is not { } attrs)

            throw new IOException("Updater lock file is missing.");
        RejectType(path, attrs, false);

        return new FileStream(path, FileMode.Open, FileAccess.Read, exclusive ? FileShare.None : FileShare.Read);
    }

    internal static void VerifyStartup(string root, string tag, string rid)
    {
        var journalPath = CheckedPath(root, Journal);

        if (Attributes(journalPath) is null)

            return;
        var journal = ReadJson(journalPath, UpdateJson.Default.UpdateJournal, MaxJournal);

        if (journal.NewTag != tag || journal.Entries is not { Length: > 0 and <= 2 * MaxEntries + 1 })

            throw new InvalidDataException("An update was interrupted.");
        var manifest = VerifyInstall(root, tag, rid);
        var names = new PackagePaths();

        foreach (var entry in journal.Entries)
        {
            if (Protected(entry.Path))

                throw new InvalidDataException("Invalid update journal.");
            names.Add(entry.Path, false);

            if (entry.Path == Manifest)

                continue;
            var path = CheckedPath(root, entry.Path);

            if (manifest.Files.TryGetValue(entry.Path, out var hash) ? Hash(path) != hash : Attributes(path) is not null)

                throw new InvalidDataException("An update was interrupted.");
        }
    }
}

internal sealed class PackagePaths
{
    private readonly Dictionary<string, (string Path, bool Directory, bool Explicit)> _paths = new(StringComparer.Ordinal);

    internal void Add(string path, bool directory)
    {
        InstallLayout.ValidateRelative(path);
        for (var end = path.IndexOf('/'); end >= 0; end = path.IndexOf('/', end + 1))
            Register(path[..end], true, false);
        Register(path, directory, true);
    }

    private void Register(string path, bool directory, bool explicitEntry)
    {
        var key = InstallLayout.Key(path);

        if (_paths.TryGetValue(key, out var old)
            &&
            (old.Path != path || old.Directory != directory || (old.Explicit && explicitEntry)))

            throw new InvalidDataException($"Conflicting package path: {path}");
        if (explicitEntry || !_paths.ContainsKey(key))
            _paths[key] = (path, directory, explicitEntry);
    }
}
