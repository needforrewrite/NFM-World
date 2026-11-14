using System.Buffers;
using System.Security.Cryptography;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary;

namespace NFMWorldLibrary.Content;

/// <summary>A canonical package for one piece of shipped content.</summary>
public sealed record StockPackage(string Id, AssetKind Kind, byte[] Bytes, string Hash, long Size);

/// <summary>
/// The build's own content, packaged deterministically so an archive can identify it by hash.
/// </summary>
/// <remarks>
/// Stock identity cannot use the file the game happens to load: a mod may shadow a stock name, and
/// a user's own file may use it outright. The catalog is built from an explicit shipped root
/// instead of the VFS, with fixed metadata and stable serialization, so the same build produces
/// the same hashes every run and an upload of stock bytes is recognised as stock.
///
/// Stage packages are only produced for stages that shipped as a radpack, which already embed
/// their resolved piece geometry. A stage shipped as text needs the game's piece lookup to resolve
/// its geometry, which is not available to the archive process; those are left to the build to
/// package rather than guessed at here.
/// </remarks>
public static class StockCatalog
{
    public static IReadOnlyList<StockPackage> Build(string shippedRoot, bool includeTextStages = false)
    {
        var packages = new List<StockPackage>();
        if (!Directory.Exists(shippedRoot))
            return packages;

        AddModelDirectory(packages, shippedRoot, "data/models/nfmm/cars", AssetKind.Car);
        AddModelDirectory(packages, shippedRoot, "data/models/world/cars", AssetKind.Car);
        AddModelDirectory(packages, shippedRoot, "data/models/elo/cars", AssetKind.Car);
        AddModelDirectory(packages, shippedRoot, "data/models/football/cars", AssetKind.Car);

        AddModelDirectory(packages, shippedRoot, "data/models/nfmm/stage", AssetKind.StagePiece);
        AddModelDirectory(packages, shippedRoot, "data/models/world/stage", AssetKind.StagePiece);
        AddModelDirectory(packages, shippedRoot, "data/models/football/stage", AssetKind.StagePiece);

        AddRadpackStages(packages, shippedRoot);
        if (includeTextStages)
            AddTextStages(packages, shippedRoot);
        return packages;
    }

    /// <summary>
    /// The canonical bytes for a model, so content loaded from anywhere can be compared with the
    /// stock catalog by hash rather than by name.
    /// </summary>
    public static byte[] PackageModel(AssetKind kind, string id, Rad3d rad, string? description = null)
    {
        var type = AssetKinds.ToRadpackType(kind)
                   ?? throw new ArgumentException($"{AssetKinds.Name((byte)kind)} is not a model kind.", nameof(kind));

        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackRad3d
            {
                Metadata = new RadpackMetadata
                {
                    Name = id,
                    Description = description,
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = type
                },
                Rad = rad
            },
            writer
        );
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>The canonical bytes for a stage, so a recording pins the exact course it ran.</summary>
    public static byte[] PackageStage(string id, StageLoader stage, bool includeMusic = false)
    {
        var music = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (includeMusic)
        {
            foreach (var path in new[] { stage.musicPath, stage.remasteredMusicPath }.Where(path => !string.IsNullOrEmpty(path))
                .Distinct())
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Split('/').Contains(".."))
                    throw new InvalidDataException($"Music '{path}' must be a relative path under data/music.");
                if (stage.EmbeddedMusic?.TryGetValue(path, out var embedded) == true)
                {
                    music.Add(path, embedded);
                    continue;
                }

                using var source = VFS.OpenRead($"data/music/{path}");
                using var bytes = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bytes.Length + read > RadpackLimits.MaxCompressedBytes)
                        throw new InvalidDataException($"Music '{path}' exceeds the package size limit.");
                    bytes.Write(buffer, 0, read);
                }

                music.Add(path, bytes.ToArray());
            }
        }

        var metadata = new RadpackMetadata { Name = id, CreationDate = DateTimeOffset.UnixEpoch, Type = RadpackType.Track };
        RadpackStage package = music.Count == 0
            ? new RadpackTrack { Metadata = metadata, Stage = stage }
            : new RadpackTrackWithMusic { Metadata = metadata, Stage = stage, Music = music };
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(package, writer);
        return writer.WrittenSpan.ToArray();
    }

    public static string HashOf(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void AddModelDirectory(List<StockPackage> packages, string root, string relative, AssetKind kind)
    {
        var directory = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
            return;

        var prefix = relative["data/models/".Length..].Split('/')[0];
        foreach (var file in Directory.EnumerateFiles(directory).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = $"{prefix}/{name}";

            try
            {
                var rad = Path.GetExtension(file).Equals(".radpack", StringComparison.OrdinalIgnoreCase)
                    ? ReadPackage(file, AssetKinds.ToRadpackType(kind)!.Value) is RadpackRad3d packed
                        ? packed.Rad
                        : null
                    : RadParser.ParseRad(File.ReadAllText(file), id);

                if (rad is null)
                    continue;

                // Identity is the canonical package, not the loose file, so a stock file edited in
                // place stops matching the catalog instead of silently taking its hash.
                var bytes = PackageModel(kind, id, rad);
                packages.Add(new StockPackage(id, kind, bytes, HashOf(bytes), bytes.Length));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                Logging.Warning($"[Stock] Could not package '{file}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Packages stages that ship as text, using the game's loaded piece set. Only the running game
    /// has that set, so a seeder that never loads the game skips these.
    /// </summary>
    private static void AddTextStages(List<StockPackage> packages, string root)
    {
        if (BackendGameSparker.stage_parts.Count == 0)
            return;

        var directory = Path.Combine(root, "data", "stages");
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.txt", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            var id = Path.GetRelativePath(directory, file).Replace('\\', '/');
            id = id[..id.LastIndexOf('.')];
            try
            {
                var stage = StageLoader.FromLines(id, File.ReadLines(file));
                var bytes = PackageStage(id, stage);
                packages.Add(new StockPackage(id, AssetKind.Stage, bytes, HashOf(bytes), bytes.Length));
            }
            catch (Exception ex) when (ex is IOException or StageLoadException)
            {
                Logging.Warning($"[Stock] Could not package stage '{file}': {ex.Message}");
            }
        }
    }

    private static RadpackAsset ReadPackage(string file, RadpackType kind)
    {
        var fs = new NFMWorld.PhysicsFS.VirtualFS();
        fs.MountDirectory(Path.GetDirectoryName(file)!);
        return RadpackReader.Read(
            fs,
            Path.GetFileName(file),
            kind,
            RadpackLimits.MaxCompressedBytes,
            RadpackLimits.MaxDecompressedBytes
        );
    }

    private static void AddRadpackStages(List<StockPackage> packages, string root)
    {
        var directory = Path.Combine(root, "data", "stages");
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.radpack", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                var id = Path.GetRelativePath(directory, file).Replace('\\', '/');
                id = id[..id.LastIndexOf('.')];
                if (ReadPackage(file, RadpackType.Track) is not RadpackStage track)
                    continue;

                var writer = new ArrayBufferWriter<byte>();
                RadpackSerializer.Serialize(
                    new RadpackTrack
                    {
                        Metadata = new RadpackMetadata
                        {
                            Name = id,
                            CreationDate = DateTimeOffset.UnixEpoch,
                            Type = RadpackType.Track
                        },
                        Stage = track.Stage
                    },
                    writer
                );
                var bytes = writer.WrittenSpan.ToArray();
                packages.Add(new StockPackage(id, AssetKind.Stage, bytes, HashOf(bytes), bytes.Length));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                Logging.Warning($"[Stock] Could not package stage '{file}': {ex.Message}");
            }
        }
    }
}
