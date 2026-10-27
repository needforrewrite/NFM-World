using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NFMWorld.PhysicsFS
{
    internal sealed class ZipFS : IVirtualFileSystem
    {
        private readonly ZipArchive _archive;
        private readonly Stream? _backingStream; // kept alive for in-memory zips
        private readonly HashSet<string> _entries;
        private readonly HashSet<string> _directories;

        public ZipFS(string zipPath)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"Zip file not found: {zipPath}");

            _archive = ZipFile.OpenRead(zipPath);
            (_entries, _directories) = BuildIndex();
        }

        public ZipFS(Memory<byte> zipBytes)
        {
            _backingStream = new MemoryStream(zipBytes.ToArray());
            _archive = new ZipArchive(_backingStream, ZipArchiveMode.Read, leaveOpen: true);
            (_entries, _directories) = BuildIndex();
        }

        private (HashSet<string> entries, HashSet<string> dirs) BuildIndex()
        {
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _archive.Entries)
            {
                var fullName = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (fullName.Length == 0) continue;

                if (string.IsNullOrEmpty(entry.Name))
                {
                    // Directory entry (e.g. "fonts/")
                    dirs.Add(fullName);
                }
                else
                {
                    // File entry
                    entries.Add(fullName);
                }

                // Walk up and register all ancestor directories
                var parent = GetParentDirectory(fullName);
                while (parent is not null)
                {
                    dirs.Add(parent);
                    parent = GetParentDirectory(parent);
                }
            }

            return (entries, dirs);

            static string? GetParentDirectory(string path)
            {
                var idx = path.LastIndexOf('/');
                return idx > 0 ? path[..idx] : null;
            }
        }

        // ── IVirtualFileSystem ────────────────────────────────────

        public Stream? OpenRead(string path)
        {
            var normalized = Normalize(path);
            var entry = _archive.GetEntry(normalized);
            if (entry is null) return null;

            // Copy to a MemoryStream so callers get an independent, seekable stream.
            // The ZipArchive entry stream must be disposed promptly.
            using var src = entry.Open();
            var ms = new MemoryStream((int)entry.Length);
            src.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        public Stream? OpenWrite(string path) => null; // Read-only

        public bool FileExists(string path)
        {
            return _entries.Contains(Normalize(path));
        }

        public bool DirectoryExists(string path)
        {
            var normalized = Normalize(path).TrimEnd('/');
            return normalized.Length == 0 || _directories.Contains(normalized);
        }

        public IEnumerable<string> EnumerateFiles(
            string path, string searchPattern, SearchOption searchOption)
        {
            var prefix = Normalize(path).TrimEnd('/');
            if (prefix.Length > 0) prefix += "/";

            var regex = WildcardToRegex(searchPattern);

            foreach (var entry in _entries)
            {
                if (!entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relative = entry[prefix.Length..];

                // TopDirectoryOnly: skip entries nested in subdirectories
                if (searchOption == SearchOption.TopDirectoryOnly && relative.Contains('/'))
                    continue;

                // Match the filename portion against the search pattern
                var fileName = searchOption == SearchOption.AllDirectories
                    ? Path.GetFileName(entry)   // final segment
                    : relative;                 // already just the filename

                if (regex.IsMatch(fileName))
                    yield return entry;
            }
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            var prefix = Normalize(path).TrimEnd('/');
            if (prefix.Length > 0) prefix += "/";

            // The index holds every ancestor, so only the entries one level below the prefix are
            // this directory's own children.
            foreach (var dir in _directories)
            {
                if (!dir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relative = dir[prefix.Length..];
                if (relative.Length == 0 || relative.Contains('/'))
                    continue;

                yield return dir;
            }
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string Normalize(string path)
        {
            var normalized = path.Replace('\\', '/').TrimStart('/');

            // Callers pass paths the way a filesystem would take them ("./data/fonts/x.ttf"), and
            // an archive has no entry spelled with a leading dot segment.
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized[2..];

            return normalized;
        }

        private static Regex WildcardToRegex(string pattern)
        {
            // Escape regex metacharacters, then translate * → .*  and ? → .
            var escaped = Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");
            return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}