using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive;

/// <summary>
/// Content-addressed file storage. Every file is named by its SHA-256, so identical uploads
/// collapse to one file and there is nothing in a path that came from user input: no traversal,
/// no encoding surprises, no length tricks.
/// </summary>
public sealed class FileStorage(string root)
{
    private readonly string _root = Path.GetFullPath(root);

    public string Root => _root;

    /// <summary>Copies a submitted stream into storage and returns its hash and relative path.</summary>
    public async Task<(string Hash, string RelativePath, long Size)> PutAsync(
        Stream source,
        long maxBytes,
        CancellationToken ct
    )
    {
        var incoming = Path.Combine(_root, "incoming");
        Directory.CreateDirectory(incoming);
        var temp = Path.Combine(incoming, Guid.NewGuid().ToString("N"));

        string hash;
        long size;
        try
        {
            await using (var file = File.Create(temp))
            {
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                size = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > maxBytes)
                        throw new InvalidDataException($"Upload exceeds {maxBytes} bytes");
                    hasher.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }

                hash = Convert.ToHexStringLower(hasher.GetHashAndReset());
            }
        }
        catch
        {
            File.Delete(temp);
            throw;
        }

        var relative = Path.Combine(hash[..2], hash);
        var destination = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // Two uploads of the same bytes race here; whoever loses the rename discards its copy.
        try
        {
            File.Move(temp, destination);
        }
        catch (IOException) when (File.Exists(destination))
        {
            File.Delete(temp);
        }

        return (hash, relative.Replace('\\', '/'), size);
    }

    /// <summary>Copies stored bytes in, for content the server produced itself.</summary>
    public async Task<(string Hash, string RelativePath, long Size)> PutAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken ct
    )
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return await PutAsync(stream, data.Length, ct);
    }

    public Stream? OpenRead(string relativePath)
    {
        var full = Resolve(relativePath);
        return File.Exists(full) ? File.OpenRead(full) : null;
    }

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full))
            File.Delete(full);
    }

    /// <summary>
    /// Deletes only when no row still references the path. Storage is content-addressed, so
    /// identical uploads share one file; a blind delete on a rejected or replaced upload would
    /// take the file out from under every other row pointing at it.
    /// </summary>
    public async Task DeleteIfUnreferencedAsync(
        string relativePath,
        ArchiveDbContext db,
        CancellationToken ct
    )
    {
        var referenced = await db.TimeTrials.AnyAsync(t => t.StoragePath == relativePath, ct)
                         || await db.Assets.AnyAsync(a => a.StoragePath == relativePath, ct);
        if (!referenced)
            Delete(relativePath);
    }

    /// <summary>
    /// Resolves a relative path inside the root, refusing anything that escapes it. Paths stored
    /// in the database are server-generated, but this is the one place a bad row could become an
    /// arbitrary-file read.
    /// </summary>
    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Storage path escapes the root: {relativePath}");
        return full;
    }
}
