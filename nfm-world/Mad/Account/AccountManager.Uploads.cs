using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Accounts;

public sealed partial class AccountManager
{
    /// <summary>Uploads one canonical radpack and returns the archive's content identity.</summary>
    public async Task<AssetUploadResult> UploadAssetAsync(
        byte[] radpack,
        string name,
        string description,
        AssetKind kind,
        bool isPublic
    )
    {
        if (ActiveAccount is not { } account)
            return new AssetUploadResult(false, "Sign in from the main menu to publish.", null);

        if (!AssetKinds.IsPublishable((byte)kind))
            return new AssetUploadResult(false, $"{AssetKinds.Name((byte)kind)} cannot be published.", null);

        if (radpack.Length > RadpackLimits.MaxCompressedBytes)
            return new AssetUploadResult(false, "The package is too large to publish.", null);

        async Task<HttpResponseMessage> SendUploadAsync(string route)
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(radpack);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", "publication.radpack");
            form.Add(new StringContent(name), "name");
            form.Add(new StringContent(description), "description");
            form.Add(new StringContent(AssetKinds.Name((byte)kind)), "type");
            form.Add(new StringContent(isPublic.ToString()), "isPublic");

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, route))
            {
                Content = form
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            return await _http.SendAsync(request);
        }

        try
        {
            using var publish = await SendUploadAsync("assets/publish");
            var routeUnavailable = IsPublicationRouteUnavailable(publish);
            if (routeUnavailable && !isPublic)
                return new AssetUploadResult(false, "This Archive needs an update before it can publish hidden uploads.", null);

            using var legacy = routeUnavailable ? await SendUploadAsync("assets/upload") : null;
            var response = legacy ?? publish;
            if (!response.IsSuccessStatusCode)
                return new AssetUploadResult(false, await ReadErrorAsync(response, assetRequest: true), null);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var hash = body.TryGetProperty("asset", out var asset)
                       && asset.TryGetProperty("contentHash", out var contentHash)
                ? contentHash.GetString()
                : null;

            if (body.TryGetProperty("status", out var status) && status.GetString() == "already present")
                return new AssetUploadResult(
                    false,
                    "These bytes are already in the Archive. Select your existing upload to update it.",
                    hash
                );

            var message = IsAwaitingAssetVerification(asset)
                ? $"Uploaded \"{name}\". Awaiting verification."
                : $"Published \"{name}\".";
            return new AssetUploadResult(true, message, hash);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AssetUploadResult(false, $"Could not reach the archive: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Submits a finished time trial under the signed-in account. The archive re-simulates the
    /// replay against its own stored stage and car, so the client's number is never the ranking.
    /// </summary>
    public Task<RequestResult> UploadTimeTrialAsync(
        byte[] recording,
        string stageHash,
        string carHash,
        string stageName,
        string carName
    )
        => UploadTimeTrialAsync(recording, stageHash, carHash, stageName, carName, ActiveAccount);

    internal async Task<RequestResult> UploadTimeTrialAsync(
        byte[] recording,
        string stageHash,
        string carHash,
        string stageName,
        string carName,
        Account? account
    )
    {
        if (account is null)
            return new RequestResult("Sign in to submit a time trial.", false);

        if (recording.Length > RadpackLimits.MaxTimeTrialBytes)
            return new RequestResult("The recording is too large to submit.", false);

        try
        {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(recording);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", "run.timetrial");
            form.Add(new StringContent(stageHash), "stageHash");
            form.Add(new StringContent(carHash), "carHash");
            form.Add(new StringContent(stageName), "stageName");
            form.Add(new StringContent(carName), "carName");

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "tt/upload"))
            {
                Content = form
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new RequestResult(await ReadErrorAsync(response), false);

            return new RequestResult("Submitted.", true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
    }
}
