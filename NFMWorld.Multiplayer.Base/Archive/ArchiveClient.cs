using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Multiplayer.Archive;

public sealed record ArchiveAccount(
    int AccountId,
    string Username,
    UserRole Role,
    int ModerationRevision,
    bool Banned,
    DateTimeOffset? BannedUntil,
    string? BanReason,
    DateTimeOffset? KickedUntil,
    bool Muted,
    DateTimeOffset? MutedUntil,
    string? MuteReason
);

/// <summary>
/// What the archive said about a token. <see cref="Unavailable"/> is not a rejection: the lobby
/// must not destroy a player's session because it could not reach the archive.
/// </summary>
public enum TokenVerification
{
    Valid,
    Rejected,
    Unavailable,
}

public sealed record ResultPlayer(int? AccountId, int FinishPosition, long? FinishTimeTicks, bool IsBot);

public sealed record MatchReport(
    string MatchKey,
    string Gamemode,
    string StageName,
    string? StageHash,
    long DurationTicks,
    IReadOnlyList<ResultPlayer> Players
);

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
    public bool CanModerate => _keyId is not null && _secret is not null;

    /// <summary>
    /// Resolves an account token to an account. A token the archive refuses is
    /// <see cref="TokenVerification.Rejected"/>; a token it could not be asked about - a network
    /// failure, a timeout, an unusable answer - is <see cref="TokenVerification.Unavailable"/>,
    /// which refuses lobby access without invalidating the player's stored session.
    /// </summary>
    public async Task<(TokenVerification Status, ArchiveAccount? Account)> ValidateTokenAsync(
        string token,
        CancellationToken ct
    )
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, "auth/verify"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (TokenVerification.Rejected, null);

            if (!response.IsSuccessStatusCode)
                return (TokenVerification.Unavailable, null);

            var account = await response.Content.ReadFromJsonAsync<ArchiveAccount>(ct);
            return account is { AccountId: > 0 }
                ? (TokenVerification.Valid, account)
                : (TokenVerification.Unavailable, null);
        }
        catch (Exception ex)
        {
            // An unusable answer is an outage, not a rejection: a proxy or captive portal hands
            // back HTML with a 200, and a bad charset surfaces as an invalid operation. Anything
            // that is not a definitive refusal must leave the player's session alone.
            Logging.Warning($"[Archive] Token verification failed: {ex.Message}");
            return (TokenVerification.Unavailable, null);
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

    /// <summary>Applies a moderation action, signed with the server key. Returns ok/error.</summary>
    public async Task<(bool Ok, string? Error)> ModerateAsync(ModerationEvent request, CancellationToken ct)
    {
        if (_keyId is null || _secret is null)
            throw new InvalidOperationException("This archive client has no server key; it cannot moderate.");

        var body = JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web);
        var nonce = Guid.NewGuid().ToString("N");
        var authHeader = HmacAuth.SignWithNonce("POST", "/moderation/events", body, _keyId, _secret, nonce);

        using var http = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "moderation/events"))
        {
            Content = new ByteArrayContent(body)
        };
        http.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        try
        {
            using var response = await _http.SendAsync(http, ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"The archive refused the action ({(int)response.StatusCode}).");

            var result = await response.Content.ReadFromJsonAsync<ModerationResult>(ct);
            return result is null ? (false, "The archive returned no answer.") : (result.Ok, result.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logging.Warning($"[Archive] Moderation request failed: {ex.Message}");
            return (false, "The archive could not be reached.");
        }
    }

    /// <summary>Re-reads one account's moderation state, signed with the server key.</summary>
    public async Task<ArchiveAccount?> RevalidateAsync(int accountId, CancellationToken ct)
    {
        if (_keyId is null || _secret is null)
            return null;

        var path = $"auth/account/{accountId}";
        var authHeader = HmacAuth.Sign("GET", "/" + path, ReadOnlySpan<byte>.Empty, _keyId, _secret);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, path));
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<ArchiveAccount>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logging.Warning($"[Archive] Account revalidation failed: {ex.Message}");
            return null;
        }
    }
}

public sealed record ModerationEvent(
    int ActorAccountId,
    ModerationActionKind Action,
    int TargetAccountId,
    string TargetUsername,
    string? Reason,
    long? DurationSeconds,
    string? Argument
);

internal sealed record ModerationResult(bool Ok, string? Error);
