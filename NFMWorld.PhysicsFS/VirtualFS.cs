namespace NFMWorld.PhysicsFS;

public class VirtualFS
{
    private readonly List<IVirtualFileSystem> _readMounts = [];
    private string? _writeRoot;

    // ── Mounting ──────────────────────────────────────────────

    public void MountDirectory(string root)
    {
        _readMounts.Add(new DirectoryFS(root));
    }

    public void MountZip(string zipFile)
    {
        _readMounts.Add(new ZipFS(zipFile));
    }

    public void MountZip(Memory<byte> zipBytes)
    {
        _readMounts.Add(new ZipFS(zipBytes));
    }

    public void Mount(IVirtualFileSystem fs)
    {
        _readMounts.Add(fs);
    }

    public void MountWriteDestination(string root)
    {
        _writeRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(_writeRoot);
    }

    // ── Query ─────────────────────────────────────────────────

    public IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern = "*.*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Read mounts (first match wins)
        foreach (var mount in _readMounts)
        {
            foreach (var file in mount.EnumerateFiles(path, searchPattern, searchOption))
            {
                if (seen.Add(file))
                    yield return file;
            }
        }

        // Write root (lowest priority)
        if (_writeRoot is not null)
        {
            var fullPath = Path.Combine(_writeRoot, path);
            if (Directory.Exists(fullPath))
            {
                foreach (var file in Directory.EnumerateFiles(fullPath, searchPattern, searchOption))
                {
                    var relative = Path.GetRelativePath(_writeRoot, file).Replace("\\", "/");
                    if (seen.Add(relative))
                        yield return relative;
                }
            }
        }
    }

    /// <summary>Immediate subdirectories of <paramref name="path"/>, as paths relative to the
    /// mount root, matching <see cref="EnumerateFiles"/>'s convention.</summary>
    public IEnumerable<string> EnumerateDirectories(string path)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mount in _readMounts)
        {
            foreach (var dir in mount.EnumerateDirectories(path))
            {
                if (seen.Add(dir))
                    yield return dir;
            }
        }

        if (_writeRoot is not null)
        {
            var fullPath = Path.Combine(_writeRoot, path);
            if (Directory.Exists(fullPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(fullPath))
                {
                    var relative = Path.GetRelativePath(_writeRoot, dir).Replace("\\", "/");
                    if (seen.Add(relative))
                        yield return relative;
                }
            }
        }
    }

    public bool FileExists(string path)
    {
        foreach (var mount in _readMounts)
        {
            if (mount.FileExists(path))
                return true;
        }

        if (_writeRoot is not null && File.Exists(Path.Combine(_writeRoot, path)))
            return true;

        return false;
    }

    public bool DirectoryExists(string path)
    {
        foreach (var mount in _readMounts)
        {
            if (mount.DirectoryExists(path))
                return true;
        }

        if (_writeRoot is not null && Directory.Exists(Path.Combine(_writeRoot, path)))
            return true;

        return false;
    }

    public void CreateDirectory(string path)
    {
        if (_writeRoot is null)
            throw new InvalidOperationException(
                "No write destination mounted. Call MountWriteDestination first.");

        Directory.CreateDirectory(Path.Combine(_writeRoot, path));
    }

    // ── I/O ───────────────────────────────────────────────────

    public Stream OpenRead(string path)
    {
        foreach (var mount in _readMounts)
        {
            var stream = mount.OpenRead(path);
            if (stream is not null)
                return stream;
        }

        // Also check write root (so you can read back what you wrote)
        if (_writeRoot is not null)
        {
            var fullPath = Path.Combine(_writeRoot, path);
            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);
        }

        throw new FileNotFoundException(
            $"File not found in any mounted filesystem: {path}");
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="OpenRead"/>, for the mount that fetches its bytes over
    /// the network. Mounts that already hold their data answer from the synchronous read; a mount
    /// that cannot serve one throws instead of blocking, so the caller must use this path for the
    /// files that live in it.
    /// </summary>
    public async System.Threading.Tasks.Task<Stream> OpenReadAsync(string path)
    {
        foreach (var mount in _readMounts)
        {
            var stream = await mount.OpenReadAsync(path).ConfigureAwait(false);
            if (stream is not null)
                return stream;
        }

        if (_writeRoot is not null)
        {
            var fullPath = Path.Combine(_writeRoot, path);
            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);
        }

        throw new FileNotFoundException($"File not found in any mounted filesystem: {path}");
    }

    public Stream OpenWrite(string path)
    {
        if (_writeRoot is null)
            throw new InvalidOperationException(
                "No write destination mounted. Call MountWriteDestination first.");

        var fullPath = Path.Combine(_writeRoot, path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        return File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public byte[] ReadAllBytes(string path)
    {
        using var stream = OpenRead(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public string ReadAllText(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public IEnumerable<string> ReadAllLines(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            yield return reader.ReadLine()!;
        }
    }
}