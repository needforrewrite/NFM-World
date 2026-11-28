using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace NFMWorld.Updates;

internal static class PackageStaging
{
    internal static async Task Download(
        HttpClient client,
        ReleaseCandidate candidate,
        string file,
        Action<string> status,
        CancellationToken ct
    )
    {
        var url = candidate.Url;

        for (var redirects = 0;; redirects++)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (redirects == 5
                    || response.Headers.Location is not { } location
                    ||
                    !Uri.TryCreate(url, location, out var next)
                    || next.Scheme != Uri.UriSchemeHttps
                    ||
                    !next.IsDefaultPort
                    || next.UserInfo.Length != 0
                    ||
                    (next.AbsoluteUri != candidate.Url.AbsoluteUri && next.Host is not ("release-assets.githubusercontent.com" or "objects.githubusercontent.com")))

                    throw new InvalidDataException("Unexpected download redirect.");
                url = next;

                continue;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long length && length != candidate.Size)

                throw new InvalidDataException("Download size does not match the release.");
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            var timer = Stopwatch.StartNew();
            long received = 0;
            status(FormattableString.Invariant($"DOWNLOAD 0 {candidate.Size}"));

            while (await input.ReadAsync(buffer, ct) is var count && count != 0)
            {
                received += count;

                if (received > candidate.Size)

                    throw new InvalidDataException("Download exceeds its expected size.");
                hash.AppendData(buffer.AsSpan(0, count));
                await output.WriteAsync(buffer.AsMemory(0, count), ct);

                if (timer.ElapsedMilliseconds >= 250)
                {
                    status(FormattableString.Invariant($"DOWNLOAD {received} {candidate.Size}"));
                    timer.Restart();
                }
            }

            status("VERIFY");

            if (received != candidate.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != candidate.Hash)

                throw new InvalidDataException("Download failed SHA-256 verification or was incomplete.");

            return;
        }
    }

    internal static async Task<InstallManifest> Extract(string archive, string root, string tag, string rid, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var paths = new PackagePaths();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        var count = 0;
        long total = 0;
        await using var input = File.OpenRead(archive);

        if (rid == "win-x64")
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);

            foreach (var entry in zip.Entries)
            {
                var dir = entry.FullName.EndsWith('/');
                var mode = (entry.ExternalAttributes >> 16) & 0xf000;

                if ((entry.ExternalAttributes & (int)(FileAttributes.ReparsePoint | FileAttributes.Device)) != 0
                    ||
                    (mode != 0 && mode != (dir ? 0x4000 : 0x8000))
                    ||
                    (!dir && (entry.ExternalAttributes & (int)FileAttributes.Directory) != 0))

                    throw new InvalidDataException($"Unsupported ZIP member: {entry.FullName}");
                using var data = entry.Open();
                await ExtractEntry(entry.FullName, dir, entry.Length, data, null);
            }
        }
        else
        {
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);

            while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))

                    throw new InvalidDataException($"Unsupported TAR member: {entry.Name}");
                await ExtractEntry(
                    entry.Name,
                    entry.EntryType == TarEntryType.Directory,
                    entry.Length,
                    entry.DataStream,
                    entry.Mode
                );
            }
        }

        var manifest = InstallLayout.ReadManifest(root, tag, rid);

        if (hashes.Count != manifest.Files.Count
            || manifest.Files.Any(
                p => !hashes.TryGetValue(p.Key, out var hash) || hash != p.Value
            ))

            throw new InvalidDataException("Archive contents do not match the installation inventory.");

        if (rid != "win-x64" && !OperatingSystem.IsWindows())

            foreach (var file in new[] { InstallLayout.Apphost(rid), InstallLayout.Helper(rid) })

                if ((File.GetUnixFileMode(InstallLayout.CheckedPath(root, file)) & UnixFileMode.UserExecute) == 0)

                    throw new InvalidDataException($"Archive apphost is not executable: {file}");

        return manifest;

        async Task ExtractEntry(string path, bool directory, long declared, Stream? data, UnixFileMode? mode)
        {
            ct.ThrowIfCancellationRequested();
            if (directory && path.EndsWith('/'))
                path = path[..^1];

            if (++count > InstallLayout.MaxEntries
                || declared < 0
                || declared > (4L << 30) - total
                ||
                (path == InstallLayout.Manifest && (directory || declared > InstallLayout.MaxManifest))
                || InstallLayout.Protected(path))

                throw new InvalidDataException($"Archive limits or protected path: {path}");
            paths.Add(path, directory);
            var destination = InstallLayout.CheckedPath(root, path, directory);

            if (directory)
            {
                if (declared != 0)

                    throw new InvalidDataException("Directory entry contains data.");
                Directory.CreateDirectory(destination);

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            await using (var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                65536,
                true
            ))
            {
                long actual = 0;

                while (data is not null && await data.ReadAsync(buffer, ct) is var bytes && bytes != 0)
                {
                    actual += bytes;

                    if (actual > declared)

                        throw new InvalidDataException("Archive member exceeds its declared size.");
                    hasher.AppendData(buffer.AsSpan(0, bytes));
                    await output.WriteAsync(buffer.AsMemory(0, bytes), ct);
                }

                if (actual != declared)

                    throw new InvalidDataException("Truncated archive member.");
                total += actual;
            }

            var digest = Convert.ToHexStringLower(hasher.GetHashAndReset());
            if (path != InstallLayout.Manifest)
                hashes.Add(path, digest);
            if (mode is { } unixMode && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(
                    destination,
                    unixMode & (UnixFileMode)511
                );
        }
    }
}
