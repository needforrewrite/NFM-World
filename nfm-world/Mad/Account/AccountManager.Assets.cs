using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Accounts;
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
    bool IsTakedown,
    string? ReviewStatus = null,
    IReadOnlyList<string>? Tags = null
)
{
    public string? ReviewLabel => ReviewStatus switch
    {
        "Pending" => "Pending verification",
        "Rejected" => "Rejected",
        "Verified" => "Verified",
        _ => null
    };
}

public sealed record OwnedAssetPage(IReadOnlyList<OwnedAsset> Items, int? NextCursor);

/// <summary>
/// The owner's half of the archive: listing, renaming, hiding, re-uploading and deleting an
/// upload. Every call carries the signed-in bearer session, because these rows are not public.
/// </summary>
public sealed partial class AccountManager
{
    private const int OwnedPageSize = 100;

    public async Task<int?> GetAssetUploadLimitAsync()
    {
        try
        {
            using var response = await _http.GetAsync(new Uri(_baseUri, "assets/limits"));
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.JsonElement);
            return body.ValueKind == JsonValueKind.Object && body.TryGetProperty("maxUploadBytes", out var limit)
                   && limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out var bytes)
                   && bytes is > 0 and <= RadpackLimits.MaxCompressedBytes
                ? bytes
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

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
                throw new HttpRequestException(await ReadErrorAsync(response, assetRequest: true), null, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.JsonElement);
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
                Content = JsonContent.Create(
                    new AssetUpdateRequest(name, description, isPublic),
                    AccountJsonContext.Default.AssetUpdateRequest
                )
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode
                ? new RequestResult("Changes saved.", true)
                : new RequestResult(await ReadErrorAsync(response), false);
        }
        catch (Exception ex)
        {
            // Called fire-and-forget from the menu; nothing may escape as an unobserved fault.
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Replaces one upload's bytes, keeping its id and inventory entry. The archive re-validates
    /// the file and refuses bytes already published under another name.
    /// </summary>
    public async Task<AssetUploadResult> ReplaceAssetAsync(
        int id,
        byte[] radpack,
        string name,
        string description,
        bool isPublic
    )
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
            form.Add(new StringContent(name), "name");
            form.Add(new StringContent(description), "description");
            form.Add(new StringContent(isPublic.ToString()), "isPublic");

            using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(_baseUri, $"assets/{id}/publication"))
            {
                Content = form
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            using var response = await _http.SendAsync(request);
            if (IsPublicationRouteUnavailable(response))
                return new AssetUploadResult(
                    false,
                    "The upload is unavailable or this Archive does not support editor updates yet.",
                    null
                );
            if (!response.IsSuccessStatusCode)
                return new AssetUploadResult(false, await ReadErrorAsync(response, assetRequest: true), null);

            var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.JsonElement);
            var hash = body.TryGetProperty("asset", out var asset)
                       && asset.TryGetProperty("contentHash", out var contentHash)
                ? contentHash.GetString()
                : null;
            var message = IsAwaitingAssetVerification(asset)
                ? $"Updated \"{name}\". Awaiting verification."
                : $"Updated \"{name}\".";
            return new AssetUploadResult(true, message, hash);
        }
        catch (Exception ex)
        {
            return new AssetUploadResult(false, $"Could not reach the archive: {ex.Message}", null);
        }
    }

    private static bool IsPublicationRouteUnavailable(HttpResponseMessage response)
        => response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed;

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
        catch (Exception ex)
        {
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
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
        item.TryGetProperty("isTakedown", out var takedown) && takedown.GetBoolean(),
        item.TryGetProperty("reviewStatus", out var reviewStatus) ? reviewStatus.GetString() : null,
        item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
            ? tags.EnumerateArray().Where(tag => tag.ValueKind == JsonValueKind.String)
                .Select(tag => tag.GetString()!).ToArray()
            : []
    );

    private static bool IsAwaitingAssetVerification(JsonElement asset)
        => asset.ValueKind == JsonValueKind.Object && asset.TryGetProperty("reviewStatus", out var status)
           && status.GetString() == "Pending";
}
