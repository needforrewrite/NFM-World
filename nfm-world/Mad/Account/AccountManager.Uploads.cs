using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Accounts;

public sealed partial class AccountManager
{
    /// <summary>
    /// Submits a finished time trial under the signed-in account. The archive re-simulates the
    /// replay against its own stored stage and car, so the client's number is never the ranking.
    /// </summary>
    public async Task<RequestResult> UploadTimeTrialAsync(
        byte[] recording,
        string stageHash,
        string carHash,
        string stageName,
        string carName
    )
    {
        if (ActiveAccount is not { } account)
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
