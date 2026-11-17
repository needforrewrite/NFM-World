using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorld.Gameplay;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Accounts;

public sealed record AssetUploadResult(bool Success, string Message, string? ContentHash);

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
    public static async Task<AssetUploadResult> PublishModelAsync(
        Rad3d rad,
        AssetKind kind,
        string name,
        string? description
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
            bytes = StockCatalog.PackageModel(kind, name, rad, description);
        }
        catch (Exception ex)
        {
            return new AssetUploadResult(false, $"The model could not be packaged: {ex.Message}", null);
        }

        return await UploadAsync(bytes, name, description ?? "", kind);
    }

    public static async Task<AssetUploadResult> PublishStageAsync(
        StageLoader stage,
        string name,
        string? description
    )
    {
        byte[] bytes;
        try
        {
            bytes = StockCatalog.PackageStage(name, stage, includeMusic: true);
        }
        catch (Exception ex)
        {
            return new AssetUploadResult(false, $"The stage could not be packaged: {ex.Message}", null);
        }

        return await UploadAsync(bytes, name, description ?? "", AssetKind.Stage);
    }

    /// <summary>Writes a radpack beside the local content, so publishing is not required to keep it.</summary>
    public static string? ExportLocalModel(Rad3d rad, AssetKind kind, string name)
    {
        if (!AssetKinds.IsPublishable((byte)kind))
            return null;

        var path = $"data/models/{name}.radpack";
        try
        {
            using var stream = VFS.OpenWrite(path);
            stream.Write(StockCatalog.PackageModel(kind, name, rad));
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
        var path = $"data/stages/{name}.radpack";
        try
        {
            var bytes = StockCatalog.PackageStage(name, stage, includeMusic: true);
            using var stream = VFS.OpenWrite(path);
            stream.Write(bytes);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not export the stage: {ex.Message}");
            return null;
        }

        return path;
    }

    private static async Task<AssetUploadResult> UploadAsync(
        byte[] bytes,
        string name,
        string description,
        AssetKind kind
    )
    {
        if (bytes.Length > RadpackLimits.MaxCompressedBytes)
            return new AssetUploadResult(false, "The package is too large to publish.", null);

        if (!GameSparker.AccountManager.LoggedIn)
            return new AssetUploadResult(
                false,
                "Sign in from the main menu to publish. Your local document is unchanged.",
                null
            );

        // Re-publishing a document under a name the account already published replaces that
        // upload's bytes instead of creating a second row: the author meant to update it, and a
        // duplicate would leave the stale copy listed next to the new one.
        if (await GameSparker.AccountManager.FindOwnedAssetAsync(name, (byte)kind) is { } existing)
        {
            var replaced = await GameSparker.AccountManager.ReplaceAssetAsync(existing.Id, bytes);
            if (!replaced.Success)
                return replaced;

            var renamed = await GameSparker.AccountManager.UpdateAssetAsync(
                existing.Id,
                name,
                description,
                isPublic: true
            );
            return renamed.Success
                ? new AssetUploadResult(true, $"Updated \"{name}\".", replaced.ContentHash)
                : new AssetUploadResult(
                    false,
                    renamed.Message ?? "The upload was replaced but not renamed.",
                    replaced.ContentHash
                );
        }

        return await GameSparker.AccountManager.UploadAssetAsync(bytes, name, description, kind);
    }
}
