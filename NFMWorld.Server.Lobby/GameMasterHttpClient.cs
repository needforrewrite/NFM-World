using System.Net;
using System.Net.Http.Headers;
using TightPack;
using NFMWorldLibrary.Multiplayer.HttpMessages;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Sends authenticated HTTP requests from the Lobby to a Game Master instance.
/// Uses HMAC-SHA256 request signing — both sides share a secret key.
/// </summary>
public class GameMasterHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _contentHttpClient;
    private readonly string _keyId;
    private readonly byte[] _secretKey;

    /// <summary>
    /// Declared on every request, so a game master built from another revision refuses the call
    /// instead of deserializing the body with a layout it does not share.
    /// </summary>
    private readonly string _protocolVersion = Protocol.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public GameMasterHttpClient(string keyId, string secretKeyBase64)
    {
        _keyId = keyId;
        _secretKey = Convert.FromBase64String(secretKeyBase64);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Asset downloads get their own window: a cold stage on a slow link is not a race
        // creation failure, and must not eat the create-race timeout.
        _contentHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task PrepareContentAsync(
        ResolvedGameMaster master,
        Lobby2RaceServer_PrepareContent request
    )
    {
        var body = TightPackSerializer.Serialize(request);
        var authHeader = HmacAuth.Sign("POST", "/prepare-content", body, _keyId, _secretKey);

        var requestUri = new Uri(master.HttpEndpoint, "/prepare-content");
        Console.WriteLine($"[Lobby→GM] POST {requestUri}");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new ByteArrayContent(body)
        };
        httpRequest.Headers.Add("Authorization", authHeader);
        httpRequest.Headers.Add(Protocol.HeaderName, _protocolVersion);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _contentHttpClient.SendAsync(httpRequest);
        EnsureProtocolAccepted(response);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Lobby2RaceServer_CreateRaceResponse> CreateRaceAsync(
        ResolvedGameMaster master,
        Lobby2RaceServer_CreateRace request
    )
    {
        var body = TightPackSerializer.Serialize(request);
        var authHeader = HmacAuth.Sign("POST", "/create-race", body, _keyId, _secretKey);

        var requestUri = new Uri(master.HttpEndpoint, "/create-race");
        Console.WriteLine($"[Lobby→GM] POST {requestUri}");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new ByteArrayContent(body)
        };
        httpRequest.Headers.Add("Authorization", authHeader);
        httpRequest.Headers.Add(Protocol.HeaderName, _protocolVersion);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(httpRequest);
        EnsureProtocolAccepted(response);
        response.EnsureSuccessStatusCode();

        var responseBytes = await response.Content.ReadAsByteArrayAsync();
        return TightPackSerializer.Deserialize<Lobby2RaceServer_CreateRaceResponse>(responseBytes);
    }

    /// <summary>
    /// Turns a version refusal into a message that names it. A plain status code would surface
    /// as "the race could not start" and point at the wrong server.
    /// </summary>
    private static void EnsureProtocolAccepted(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Conflict)
            return;

        throw new InvalidOperationException(
            $"The Game Master runs a different game version ({response.ReasonPhrase}). Update the server and try again."
        );
    }

    public async Task NotifyRaceEndedAsync(
        ResolvedGameMaster master,
        RaceServer2Lobby_RaceResults results
    )
    {
        var body = TightPackSerializer.Serialize(results);
        var authHeader = HmacAuth.Sign("POST", "/race-ended", body, _keyId, _secretKey);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(master.HttpEndpoint, "/race-ended")
        )
        {
            Content = new ByteArrayContent(body)
        };
        httpRequest.Headers.Add("Authorization", authHeader);
        httpRequest.Headers.Add(Protocol.HeaderName, _protocolVersion);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }
}
