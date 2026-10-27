using System.Collections.Concurrent;

namespace NFMWorld.PhysicsFS;

/// <summary>
/// A read-only mount whose bytes come from the network on demand, one file at a time.
///
/// The game's music is 148 MB against an 11 MB boot payload, so it cannot be fetched up front. This
/// mount answers only the asynchronous read: <see cref="OpenRead"/> throws, because the browser has
/// no synchronous HTTP and a blocking wait on the game thread would hang the page rather than fail.
/// The game's music path is the one caller that uses the async read.
///
/// The fetch itself is a delegate so this project keeps no browser dependency; the browser head
/// supplies one that reads the file over HTTP and the synchronous mounts keep their own behaviour.
/// </summary>
public sealed class FetchFileSystem : IVirtualFileSystem
{
    private readonly Func<string, byte[]?> _fetch;
    private readonly ConcurrentDictionary<string, byte> _known;

    /// <param name="fetch">Returns the file's bytes, or null when the path is not in this mount.</param>
    /// <param name="paths">Every path this mount serves, used for the synchronous existence checks.</param>
    public FetchFileSystem(Func<string, byte[]?> fetch, IEnumerable<string>? paths = null)
    {
        _fetch = fetch;
        _known = new ConcurrentDictionary<string, byte>(
            (paths ?? []).Select(p => new KeyValuePair<string, byte>(Normalize(p), 0)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A path the mount has actually fetched. Registering on first read also lets a mount
    /// built without a path list answer existence for what it has already served.</summary>
    public void Register(string path) => _known.TryAdd(Normalize(path), 0);

    /// <summary>The fetch is synchronous on the browser side (see the caller), so this is an
    /// ordinary read: the bytes are in hand by the time it returns.</summary>
    public Stream? OpenRead(string path)
    {
        var normalized = Normalize(path);
        var bytes = _fetch(normalized);
        if (bytes is null) return null;

        Register(normalized);
        return new MemoryStream(bytes, writable: false);
    }

    public Task<Stream?> OpenReadAsync(string path) => Task.FromResult(OpenRead(path));

    public Stream? OpenWrite(string path) => null;

    public bool FileExists(string path) => _known.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => false;

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => [];

    // Callers pass paths the way a filesystem would take them ("./data/music/x.mp3"), and archive
    // entries are never spelled with a leading dot segment - the same normalisation ZipFS needs.
    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized;
    }
}
