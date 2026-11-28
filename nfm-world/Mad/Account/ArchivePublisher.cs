using System.Net.Http;
using NFMWorld.Audio;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Accounts;

public sealed record AssetUploadResult(bool Success, string Message, string? ContentHash);

internal sealed record ArchivePublication(string Name, string Description, bool IsPublic, int? AssetId);

/// <summary>
/// Publishes editor documents as canonical radpacks and uploads them under the signed-in account.
/// </summary>
/// <remarks>
/// The bytes come from the live document the caller snapshotted, never from rereading a source
/// file, so unsaved edits are what gets published. A failure leaves the local document and its
/// dirty state alone; nothing here writes the working file.
/// </remarks>
public static class ArchivePublisher
{
    internal static async Task<AssetUploadResult> PublishModelAsync(
        Rad3d rad,
        AssetKind kind,
        ArchivePublication publication
    )
    {
        if (kind is not (AssetKind.Car or AssetKind.StagePiece or AssetKind.Wheel))

            return new AssetUploadResult(false, $"{AssetKinds.Name((byte)kind)} cannot be published.", null);

        // A car goes into other players' races, so it is checked here before it is packaged and
        // again by the archive before it is stored. The garage already refuses to offer a model
        // that has no four-wheel contact set; publishing is the point where the rest of the
        // values have to hold up too.

        if (kind == AssetKind.Car && CarModelValidator.Validate(rad) is { } invalid)

            return new AssetUploadResult(false, $"This car cannot be published: {invalid}", null);

        byte[] bytes;

        try
        {
            // The packaged FileName is the model's identity in a stage file. Parsing from a tab
            // bakes the author's local path, which no other machine can resolve.
            rad.FileName = publication.Name;
            bytes = StockCatalog.PackageModel(kind, publication.Name, rad, publication.Description);
        }
        catch (Exception ex)
        {
            return new AssetUploadResult(false, $"The model could not be packaged: {ex.Message}", null);
        }

        return await UploadAsync(bytes, kind, publication);
    }

    internal static async Task<AssetUploadResult> PublishStageAsync(
        StageLoader stage,
        ArchivePublication publication
    )
    {
        try
        {
            var limit = await GameSparker.AccountManager.GetAssetUploadLimitAsync()
                        ?? RadpackLimits.MaxCompressedBytes;
            var music = StockCatalog.ReadStageMusic(stage);
            var original = music.ToDictionary(pair => pair.Key, pair => new StageMusic(pair.Key, pair.Value));
            var bytes = StockCatalog.PackageStage(publication.Name, stage, original);

            if (bytes.Length <= limit)

                return await UploadAsync(bytes, AssetKind.Stage, publication);

            foreach (var bitrate in new[] { 64_000, 32_000 })
            {
                var converted = new Dictionary<string, StageMusic>();

                foreach (var (path, data) in music)
                {
                    try
                    {
#if WEB_AUDIO
                        using var decoded = Path.GetExtension(path)
                            .ToLowerInvariant() is ".opus" or ".ogg" or ".oga" or ".aif" or ".aiff" or ".aifc"
                            ? await WebAudioBackend.DecodeTrackForUploadAsync(data)
                            : AudioDecoder.DecodeTrack(new MemoryStream(data, writable: false), Path.GetExtension(path));
#else
                        using var source = new MemoryStream(data, writable: false);
                        using var decoded = AudioDecoder.DecodeTrack(source, Path.GetExtension(path));
#endif
                        var encoded = OpusTrackEncoder.Encode(decoded, bitrate);
                        var outputPath = path + ".opus";
                        while (music.ContainsKey(outputPath) || converted.Values.Any(item => item.Path == outputPath))
                            outputPath += ".opus";
                        converted[path] = encoded.Length < data.Length
                            ? new StageMusic(outputPath, encoded)
                            : original[path];
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Logging.Warning($"Could not compress soundtrack '{path}': {ex.Message}");
                        converted[path] = original[path];
                    }
                }

                bytes = StockCatalog.PackageStage(publication.Name, stage, converted);

                if (bytes.Length <= limit)

                    return await UploadAsync(bytes, AssetKind.Stage, publication);
            }

            return new AssetUploadResult(
                false,
                "The stage exceeds the Archive upload limit even after compressing its soundtrack.",
                null
            );
        }
        catch (Exception ex)
        {
            return new AssetUploadResult(false, $"The stage could not be packaged: {ex.Message}", null);
        }
    }

    /// <summary>Writes a model pack to the local radpack directory.</summary>
    public static string? ExportLocalModel(Rad3d rad, AssetKind kind, string name)
    {
        if (!AssetKinds.IsPublishable((byte)kind))

            return null;

        if (GetExportPath(name) is not { } path)

            return null;

        try
        {
            rad.FileName = name;
            VFS.WriteAllBytesAtomic(path, StockCatalog.PackageModel(kind, name, rad));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not export the model: {ex.Message}");

            return null;
        }

        return path;
    }

    public static string? ExportLocalStage(StageLoader stage, string name)
    {
        if (GetExportPath(name) is not { } path)

            return null;

        try
        {
            var bytes = StockCatalog.PackageStage(name, stage, includeMusic: true);
            VFS.WriteAllBytesAtomic(path, bytes);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not export the stage: {ex.Message}");

            return null;
        }

        return path;
    }

    private static string? GetExportPath(string name)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            ? null
            : $"data/radpacks/{fileName}.radpack";
    }

    private static Task<AssetUploadResult> UploadAsync(byte[] bytes, AssetKind kind, ArchivePublication publication)
        => publication.AssetId is { } id
            ? GameSparker.AccountManager.ReplaceAssetAsync(
                id,
                bytes,
                publication.Name,
                publication.Description,
                publication.IsPublic
            )
            : GameSparker.AccountManager.UploadAssetAsync(
                bytes,
                publication.Name,
                publication.Description,
                kind,
                publication.IsPublic
            );
}
