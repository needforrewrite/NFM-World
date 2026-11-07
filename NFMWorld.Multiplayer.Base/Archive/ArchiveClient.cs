using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NFMWorldLibrary.Multiplayer.Archive;

public sealed record ArchiveAccount(int AccountId, string Username);

public sealed record ResultPlayer(int? AccountId, int FinishPosition, long? FinishTimeTicks, bool IsBot);

public sealed record MatchReport(
    string MatchKey,
    string Gamemode,
    string StageName,
    string? StageHash,
    long DurationTicks,
    IReadOnlyList<ResultPlayer> Players);

/// <summary>
/// Client for the archive server.
///
/// Two trust levels share one HTTP client: <c>/auth/verify</c> is called with a player's own
/// session token, while <c>/results/*</c> is signed with the lobby's server key. The server key
/// must never be handed to a game master, which is why the lobby, not the race server, reports
/// results.
/// </summary>
public sealed class ArchiveClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly string? _keyId;
    private readonly byte[]? _secret;

    public ArchiveClient(string baseUrl, string? keyId = null, string? secretBase64 = null, HttpClient? http = null)
    {
        _baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _keyId = keyId;
        if (!string.IsNullOrEmpty(secretBase64))
            _secret = Convert.FromBase64String(secretBase64);
    }

    public bool CanReportResults => _keyId is not null && _secret is not null;

    /// <summary>
    /// Resolves an account token to an account, or null when the archive rejects it. A network
    /// failure is reported as null too: refusing a player whose token cannot be checked is safer
    /// than admitting them on trust.
    /// </summary>
    public async Task<ArchiveAccount?> VerifyTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, "auth/verify"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var account = await response.Content.ReadFromJsonAsync<ArchiveAccount>(ct);
            return account is { AccountId: > 0 } ? account : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Logging.Warning($"[Archive] Token verification failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Reports a finished match. Idempotent on the match key.</summary>
    public async Task<bool> ReportMatchAsync(MatchReport report, CancellationToken ct)
    {
        if (_keyId is null || _secret is null)
            throw new InvalidOperationException("This archive client has no server key; it cannot report results.");

        var body = JsonSerializer.SerializeToUtf8Bytes(report, JsonSerializerOptions.Web);
        var nonce = Guid.NewGuid().ToString("N");
        var authHeader = HmacAuth.SignWithNonce("POST", "/results/match", body, _keyId, _secret, nonce);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "results/match"))
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                Logging.Warning($"[Archive] Result report rejected: {(int)response.StatusCode} {response.ReasonPhrase}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logging.Warning($"[Archive] Result report failed: {ex.Message}");
            return false;
        }
    }
}
