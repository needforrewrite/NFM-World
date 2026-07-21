using System.Net.Http.Headers;
using MemoryPack;
using NFMWorldLibrary.Multiplayer.HttpMessages;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Sends authenticated HTTP requests from the Lobby to a Game Master instance.
/// Uses HMAC-SHA256 request signing — both sides share a secret key.
/// </summary>
public class GameMasterHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _keyId;
    private readonly byte[] _secretKey;

    public GameMasterHttpClient(string keyId, string secretKeyBase64)
    {
        _keyId = keyId;
        _secretKey = Convert.FromBase64String(secretKeyBase64);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<Lobby2RaceServer_CreateRaceResponse> CreateRaceAsync(
        ResolvedGameMaster master,
        Lobby2RaceServer_CreateRace request)
    {
        var body = MemoryPackSerializer.Serialize(request);
        var authHeader = HmacAuth.Sign("POST", "/create-race", body, _keyId, _secretKey);

        var requestUri = new Uri(master.HttpEndpoint, "/create-race");
        Console.WriteLine($"[Lobby→GM] POST {requestUri}");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new ByteArrayContent(body)
        };
        httpRequest.Headers.Add("Authorization", authHeader);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseBytes = await response.Content.ReadAsByteArrayAsync();
        return MemoryPackSerializer.Deserialize<Lobby2RaceServer_CreateRaceResponse>(responseBytes);
    }

    public async Task NotifyRaceEndedAsync(
        ResolvedGameMaster master,
        RaceServer2Lobby_RaceResults results)
    {
        var body = MemoryPackSerializer.Serialize(results);
        var authHeader = HmacAuth.Sign("POST", "/race-ended", body, _keyId, _secretKey);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(master.HttpEndpoint, "/race-ended"))
        {
            Content = new ByteArrayContent(body)
        };
        httpRequest.Headers.Add("Authorization", authHeader);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
    }
}
