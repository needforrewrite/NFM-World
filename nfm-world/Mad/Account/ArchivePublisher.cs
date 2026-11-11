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

        var bytes = StockCatalog.PackageModel(kind, name, rad, description);
        return await UploadAsync(bytes, name, description ?? "", kind);
    }

    public static async Task<AssetUploadResult> PublishStageAsync(
        StageLoader stage,
        string name,
        string? description
    )
    {
        var bytes = StockCatalog.PackageStage(name, stage);
        return await UploadAsync(bytes, name, description ?? "", AssetKind.Stage);
    }

    /// <summary>Writes a radpack beside the local content, so publishing is not required to keep it.</summary>
    public static string? ExportLocalModel(Rad3d rad, AssetKind kind, string name)
    {
        if (!AssetKinds.IsPublishable((byte)kind))
            return null;

        var bytes = StockCatalog.PackageModel(kind, name, rad);
        var path = $"data/models/{name}.radpack";
        using (var stream = VFS.OpenWrite(path))
            stream.Write(bytes);
        return path;
    }

    public static string? ExportLocalStage(StageLoader stage, string name)
    {
        var bytes = StockCatalog.PackageStage(name, stage);
        var path = $"data/stages/{name}.radpack";
        using (var stream = VFS.OpenWrite(path))
            stream.Write(bytes);
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

        return await GameSparker.AccountManager.UploadAssetAsync(bytes, name, description, kind);
    }
}
