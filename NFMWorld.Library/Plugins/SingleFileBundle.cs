using System.IO.Compression;
using System.Reflection;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The apphost of a <c>PublishSingleFile=true</c> build, which holds every assembly in the app's
/// closure instead of next to the executable.
/// </summary>
/// <remarks>
/// Nothing that reads an assembly by path can see a bundled one - Mono.Cecil most importantly, so
/// the plugin sandbox has no way to resolve the host assemblies a plugin references. This reads the
/// host's own layout (dotnet/runtime's <c>Microsoft.NET.HostModel</c>): the 8 bytes before a 32-byte
/// marker hold the header's offset, and at that offset are the version, the entry count, the bundle
/// id, then one record per file. Header and marker only: an entry's bytes are read on demand.
///
/// The format has been stable since .NET 5 and only ever gained trailing fields, which is why the
/// version is checked rather than trusted.
/// </remarks>
internal sealed class SingleFileBundle
{
    private const byte AssemblyEntry = 1;

    // bundle_marker_t's signature, which the host searches for to find the header.
    private static readonly byte[] Marker =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    ];

    private static readonly Lazy<SingleFileBundle?> Current = new(TryOpen);

    /// <summary>
    /// The bundle this process was started from, or null when it is not a single-file build.
    /// </summary>
    public static SingleFileBundle? ForCurrentProcess => Current.Value;

    private readonly string _path;
    private readonly Dictionary<string, BundleEntry> _assemblies;

    private readonly record struct BundleEntry(string RelativePath, long Offset, long Size, long CompressedSize);

    private SingleFileBundle(string path, BundleEntry[] entries)
    {
        _path = path;
        _assemblies = new Dictionary<string, BundleEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            // A bundle keeps assemblies under their own name, sometimes in a subdirectory.
            var name = entry.RelativePath[(entry.RelativePath.LastIndexOf('/') + 1)..];
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                _assemblies.TryAdd(name[..^4], entry);
        }
    }

    /// <summary>
    /// Reads the assembly of the given simple name out of the bundle, or returns false when it is
    /// not bundled.
    /// </summary>
    public bool TryGetAssembly(string simpleName, out byte[] bytes)
    {
        bytes = [];
        if (!_assemblies.TryGetValue(simpleName, out var entry))
            return false;

        using var file = OpenImage();
        file.Position = entry.Offset;

        if (entry.CompressedSize == 0)
        {
            bytes = new byte[entry.Size];
            file.ReadExactly(bytes);
            return true;
        }

        // EnableCompressionInSingleFile deflates each entry on its own, with its length in the image.
        var compressed = new byte[entry.CompressedSize];
        file.ReadExactly(compressed);
        using var deflate = new DeflateStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
        bytes = new byte[entry.Size];
        deflate.ReadExactly(bytes);
        return true;
    }

    private FileStream OpenImage() =>
        new(
            _path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            FileOptions.RandomAccess
        );

    private static SingleFileBundle? TryOpen()
    {
        // The host reports an empty location for the entry assembly of a single-file app, and only
        // then is there anything to read. Reading it is the question, so the analyzer's warning is
        // the answer rather than a defect.
#pragma warning disable IL3000
        if (Assembly.GetEntryAssembly()?.Location is { Length: > 0 })
            return null;
#pragma warning restore IL3000

        if (Environment.ProcessPath is not { } path)
            return null;

        try
        {
            using var file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.RandomAccess
            );
            var length = file.Length;
            if (!TryLocateHeader(file, length, out var headerOffset))
                return null;

            var entries = ReadEntries(file, headerOffset);
            return entries is null ? null : new SingleFileBundle(path, entries);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the marker, then the header offset stored in the 8 bytes before it. The marker is
    /// matched by value, so a candidate only counts once the header it points at looks like one.
    /// </summary>
    private static bool TryLocateHeader(FileStream file, long length, out long headerOffset)
    {
        const int ChunkSize = 1 << 20;

        var chunk = new byte[ChunkSize + Marker.Length];
        long consumed = 0; // file offset just past the last read
        var carried = 0; // leading bytes of `chunk` already scanned

        while (true)
        {
            var read = file.Read(chunk, carried, ChunkSize);
            if (read == 0)
                break;

            var total = carried + read;
            var span = chunk.AsSpan(0, total);
            var chunkStart = consumed - carried;
            for (var search = 0; search < total;)
            {
                var index = span[search..].IndexOf(Marker);
                if (index < 0)
                    break;

                var at = search + index;
                if (IsHeader(file, length, chunkStart + at, out headerOffset))
                    return true;

                search = at + 1;
            }

            // Keep the tail, so a marker straddling two reads is still seen.
            carried = Math.Min(total, Marker.Length - 1);
            chunk.AsSpan(total - carried, carried).CopyTo(chunk);
            consumed += read;
        }

        headerOffset = 0;
        return false;
    }

    private static bool IsHeader(FileStream file, long length, long markerOffset, out long headerOffset)
    {
        headerOffset = 0;
        if (markerOffset < 8)
            return false;

        Span<byte> field = stackalloc byte[8];
        file.Position = markerOffset - 8;
        file.ReadExactly(field);
        var offset = BitConverter.ToInt64(field);

        // The header is written after the apphost and the bundled files, never before the marker.
        if (offset <= markerOffset || offset + 12 > length)
            return false;

        Span<byte> header = stackalloc byte[12];
        file.Position = offset;
        file.ReadExactly(header);
        var major = BitConverter.ToUInt32(header);
        var count = BitConverter.ToInt32(header[8..]);
        if (major is < 1 or > 8 || count is <= 0 or > 1_000_000)
            return false;

        headerOffset = offset;
        return true;
    }

    private static BundleEntry[]? ReadEntries(FileStream file, long headerOffset)
    {
        file.Position = headerOffset;
        using var reader = new BinaryReader(file, System.Text.Encoding.UTF8, leaveOpen: true);

        var major = reader.ReadUInt32();
        reader.ReadUInt32(); // minor
        var count = reader.ReadInt32();
        reader.ReadString(); // bundle id

        if (major >= 2)
        {
            reader.ReadInt64(); // deps.json offset
            reader.ReadInt64(); // deps.json size
            reader.ReadInt64(); // runtimeconfig offset
            reader.ReadInt64(); // runtimeconfig size
        }

        if (major >= 6)
            reader.ReadUInt64(); // flags

        var entries = new List<BundleEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = reader.ReadInt64();
            var size = reader.ReadInt64();
            var compressedSize = major >= 6 ? reader.ReadInt64() : 0;
            var type = reader.ReadByte();
            var relativePath = reader.ReadString();

            // Only assemblies are ever read back; everything else is content or native code.
            if (type == AssemblyEntry)
                entries.Add(new BundleEntry(relativePath, offset, size, compressedSize));
        }

        return [.. entries];
    }
}
