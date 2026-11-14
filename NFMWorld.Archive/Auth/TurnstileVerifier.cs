using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// The outcome of a challenge check. <see cref="Message"/> is visitor-facing: a refusal the
/// visitor can act on and an outage of ours must not look the same, or a visitor retries a
/// broken deployment until they give up.
/// </summary>
public readonly record struct TurnstileOutcome(bool Accepted, string Message)
{
    public static TurnstileOutcome Pass { get; } = new(true, "");
}

public sealed class TurnstileVerifier(
    HttpClient client,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<TurnstileVerifier> logger
)
{
    private const string SiteverifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private const string TestSiteKey = "1x00000000000000000000AA";
    private const string TestSecret = "1x0000000000000000000000000000000AA";
    private const string ProductionSiteKey = "0x4AAAAAAF8y76GGmumUX1_K";

    /// <summary>The challenge itself was refused: a fresh challenge may pass.</summary>
    public const string Error = "Human verification failed. Please try again.";

    /// <summary>We could not check the challenge. Retrying the form will not help yet.</summary>
    public const string UnavailableError = "Human verification is unavailable right now. Please try again later.";

    public string SiteKey => environment.IsDevelopment() ? TestSiteKey : ProductionSiteKey;

    /// <summary>
    /// Why a deployed archive refuses every challenge, or null when it is configured. Missing
    /// settings look to a visitor exactly like a failed challenge, so startup has to say so.
    /// </summary>
    public static string? FindConfigurationProblem(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return null;
        if (string.IsNullOrWhiteSpace(configuration["TURNSTILE_SECRET"]))
            return "TURNSTILE_SECRET is not set";
        return string.IsNullOrWhiteSpace(configuration["TURNSTILE_HOSTNAMES"])
            ? "TURNSTILE_HOSTNAMES is not set"
            : null;
    }

    public async Task<TurnstileOutcome> VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null)
    {
        var development = environment.IsDevelopment();
        var secret = development ? TestSecret : configuration["TURNSTILE_SECRET"];
        var hostnames = Hostnames();

        if (string.IsNullOrWhiteSpace(token))
            return Reject(action, "no challenge token was submitted");
        if (token.Length > 2048)
            return Reject(action, "the challenge token is longer than 2048 characters");
        if (string.IsNullOrWhiteSpace(secret))
            return Unavailable(action, "TURNSTILE_SECRET is not set");
        if (!development && hostnames.Length == 0)
            return Unavailable(action, "TURNSTILE_HOSTNAMES is not set");

        try
        {
            var fields = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                fields["remoteip"] = remoteIp;
            using var form = new FormUrlEncodedContent(fields);
            using var response = await client.PostAsync(SiteverifyUrl, form, ct);

            // Siteverify answers 400 with the same JSON body it uses for 200, and the body is the
            // only place the reason appears, so it is read before the status is judged.
            var body = await response.Content.ReadAsStringAsync(ct);
            VerificationResult? result = null;
            try
            {
                result = JsonSerializer.Deserialize<VerificationResult>(body);
            }
            catch (JsonException)
            {
            }

            if (result?.Errors is { Length: > 0 } errors && errors.Any(IsOurFault))
                return Unavailable(action, "siteverify rejected our request: " + string.Join(", ", errors));

            if (!response.IsSuccessStatusCode)
                return Unavailable(action, $"siteverify answered HTTP {(int)response.StatusCode}");

            if (result is not { Success: true })
            {
                var codes = result?.Errors is { Length: > 0 } refused ? string.Join(", ", refused) : "no error code";
                return Reject(action, $"siteverify refused the token ({codes}) for remoteip '{remoteIp ?? "<none>"}'");
            }

            // Cloudflare's test credentials omit the action and report a dummy hostname.
            if (development)
                return TurnstileOutcome.Pass;

            if (result.Action != action)
                return Reject(action, $"the token was solved for action '{result.Action ?? "<none>"}'");

            var hostname = result.Hostname;
            if (hostname is null || !hostnames.Contains(hostname, StringComparer.OrdinalIgnoreCase))
            {
                return Reject(
                    action,
                    $"the token was solved on '{hostname ?? "<none>"}', which TURNSTILE_HOSTNAMES "
                    + $"does not list ({string.Join(", ", hostnames)})"
                );
            }

            if (IsLocalhost(hostname))
                return Reject(action, $"the token was solved on the loopback hostname '{hostname}'");

            return TurnstileOutcome.Pass;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return Unavailable(action, "the siteverify request failed: " + ex.Message);
        }
    }

    /// <summary>
    /// True when the code blames our request or the service rather than the visitor's token. A
    /// secret or configuration problem must read as an outage, not as "retry the challenge".
    /// </summary>
    private static bool IsOurFault(string code)
        => code is "missing-input-secret" or "invalid-input-secret" or "bad-request" or "internal-error";

    /// <summary>Every refusal is a Warning carrying the reason; a bare false was undiagnosable.</summary>
    private TurnstileOutcome Reject(string action, string reason)
    {
        logger.LogWarning("Turnstile refused a {Action} attempt: {Reason}", action, reason);
        return new TurnstileOutcome(false, Error);
    }

    /// <summary>Our side failed, so the visitor is told to wait rather than to retry the challenge.</summary>
    private TurnstileOutcome Unavailable(string action, string reason)
    {
        logger.LogError("Turnstile could not check a {Action} attempt: {Reason}", action, reason);
        return new TurnstileOutcome(false, UnavailableError);
    }

    private string[] Hostnames()
        => (configuration["TURNSTILE_HOSTNAMES"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsLocalhost(string hostname)
    {
        hostname = hostname.TrimEnd('.');
        return hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || hostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
               || IPAddress.TryParse(hostname, out var ip) && IPAddress.IsLoopback(ip);
    }

    private sealed record VerificationResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("hostname")] string? Hostname,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("error-codes")] string[]? Errors
    );
}
