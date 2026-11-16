using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Accounts;

/// <summary>One of the signed-in account's own uploads, as the inventory lists it.</summary>
public sealed record OwnedAsset(
    int Id,
    string Name,
    string Type,
    string ContentHash,
    long SizeBytes,
    int DownloadCount,
    string? Description,
    DateTimeOffset CreatedAt,
    bool Eligible,
    bool IsPublic,
    bool IsTakedown
);

public sealed record OwnedAssetPage(IReadOnlyList<OwnedAsset> Items, int? NextCursor);

/// <summary>
/// The owner's half of the archive: listing, renaming, hiding, re-uploading and deleting an
/// upload. Every call carries the signed-in bearer session, because these rows are not public.
/// </summary>
public sealed partial class AccountManager
{
    private const int OwnedPageSize = 100;

    /// <summary>Every upload of the signed-in account, newest first, hidden and taken-down included.</summary>
    public async Task<IReadOnlyList<OwnedAsset>> ListOwnedAssetsAsync(string? type = null)
    {
        if (ActiveAccount is not { } account)
            return [];

        var items = new List<OwnedAsset>();
        int? cursor = null;
        // Bounded: the inventory is a menu, not a data export.
        for (var page = 0; page < 10; page++)
        {
            var url = $"assets/mine?limit={OwnedPageSize}";
            if (!string.IsNullOrWhiteSpace(type))
                url += "&type=" + Uri.EscapeDataString(type);
            if (cursor is { } after)
                url += "&after=" + after;

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, url));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return items;

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in body.GetProperty("items").EnumerateArray())
                items.Add(ReadOwned(item));

            if (!body.TryGetProperty("nextCursor", out var next) || next.ValueKind != JsonValueKind.Number)
                break;
            cursor = next.GetInt32();
        }

        return items;
    }

    /// <summary>Renames, describes or re-lists one upload. Null fields are left alone.</summary>
    public async Task<RequestResult> UpdateAssetAsync(
        int id,
        string? name = null,
        string? description = null,
        bool? isPublic = null
    )
    {
        if (ActiveAccount is not { } account)
            return new RequestResult("Sign in to edit your uploads.", false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri(_baseUri, $"assets/{id}"))
            {
                Content = JsonContent.Create(new { Name = name, Description = description, IsPublic = isPublic })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode
                ? new RequestResult("Changes saved.", true)
                : new RequestResult(await ReadErrorAsync(response), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Replaces one upload's bytes, keeping its id and inventory entry. The archive re-validates
    /// the file and refuses bytes already published under another name.
    /// </summary>
    public async Task<AssetUploadResult> ReplaceAssetAsync(int id, byte[] radpack)
    {
        if (ActiveAccount is not { } account)
            return new AssetUploadResult(false, "Sign in to edit your uploads.", null);

        if (radpack.Length > RadpackLimits.MaxCompressedBytes)
            return new AssetUploadResult(false, "The package is too large to publish.", null);

        try
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(radpack);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", "replacement.radpack");

            using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(_baseUri, $"assets/{id}/content"))
            {
                Content = form
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new AssetUploadResult(false, await ReadErrorAsync(response), null);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var hash = body.TryGetProperty("asset", out var asset)
                       && asset.TryGetProperty("contentHash", out var contentHash)
                ? contentHash.GetString()
                : null;
            return new AssetUploadResult(true, "The file was replaced.", hash);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AssetUploadResult(false, $"Could not reach the archive: {ex.Message}", null);
        }
    }

    /// <summary>Deletes one upload. The archive stops serving it and unranks replays that used it.</summary>
    public async Task<RequestResult> DeleteAssetAsync(int id)
    {
        if (ActiveAccount is not { } account)
            return new RequestResult("Sign in to delete your uploads.", false);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_baseUri, $"assets/{id}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode
                ? new RequestResult("Upload deleted.", true)
                : new RequestResult(await ReadErrorAsync(response), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
    }

    /// <summary>
    /// The account's own upload of this kind with this name, if it has one. Publishing a document
    /// again under the same name updates that row instead of creating a duplicate.
    /// </summary>
    public async Task<OwnedAsset?> FindOwnedAssetAsync(string name, byte kind)
    {
        var type = AssetKinds.Name(kind);
        foreach (var asset in await ListOwnedAssetsAsync(type))
        {
            if (string.Equals(asset.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                return asset;
        }

        return null;
    }

    private static OwnedAsset ReadOwned(JsonElement item) => new(
        item.GetProperty("id").GetInt32(),
        item.GetProperty("name").GetString() ?? "",
        item.GetProperty("type").GetString() ?? "",
        item.GetProperty("contentHash").GetString() ?? "",
        item.GetProperty("sizeBytes").GetInt64(),
        item.GetProperty("downloadCount").GetInt32(),
        item.TryGetProperty("description", out var description) ? description.GetString() : null,
        item.TryGetProperty("createdAt", out var createdAt) && createdAt.TryGetDateTimeOffset(out var date)
            ? date
            : DateTimeOffset.UtcNow,
        item.TryGetProperty("eligible", out var eligible) && eligible.GetBoolean(),
        !item.TryGetProperty("isPublic", out var isPublic) || isPublic.GetBoolean(),
        item.TryGetProperty("isTakedown", out var takedown) && takedown.GetBoolean()
    );
}
