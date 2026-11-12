namespace NFMWorld.PhysicsFS
{
    internal sealed class DirectoryFS : IVirtualFileSystem
    {
        private readonly string _root;

        public DirectoryFS(string root)
        {
            _root = Path.GetFullPath(root);

            if (!Directory.Exists(_root))
                throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        public Stream? OpenRead(string path)
        {
            var fullPath = Resolve(path);
            return fullPath is not null && File.Exists(fullPath)
                ? File.OpenRead(fullPath)
                : null;
        }

        public Stream? OpenWrite(string path)
        {
            var fullPath = Resolve(path);
            if (fullPath is null) return null;

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            return File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        public bool FileExists(string path)
        {
            var fullPath = Resolve(path);
            return fullPath is not null && File.Exists(fullPath);
        }

        public bool DirectoryExists(string path)
        {
            var fullPath = Resolve(path);
            return fullPath is not null && Directory.Exists(fullPath);
        }

        public IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern,
            SearchOption searchOption
        )
        {
            var fullPath = Resolve(path);
            if (fullPath is null || !Directory.Exists(fullPath))
                yield break;

            foreach (var file in Directory.EnumerateFiles(fullPath, searchPattern, searchOption))
                yield return Path.GetRelativePath(_root, file).Replace("\\", "/");
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            var fullPath = Resolve(path);
            if (fullPath is null || !Directory.Exists(fullPath))
                yield break;

            foreach (var directory in Directory.EnumerateDirectories(fullPath))
                yield return Path.GetRelativePath(_root, directory).Replace("\\", "/");
        }

        /// <summary>
        /// Resolves a virtual path and validates it stays within the mount root.
        /// Returns null if the path escapes the root (path traversal attempt).
        /// </summary>
        private string? Resolve(string path)
        {
            var full = Path.GetFullPath(Path.Combine(_root, path));
            return full.StartsWith(_root, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }
    }
}
