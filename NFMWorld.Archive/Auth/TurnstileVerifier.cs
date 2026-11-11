using System.Net;
using System.Text.Json;

namespace NFMWorld.Archive.Auth;

public sealed class TurnstileVerifier(HttpClient client, IConfiguration configuration, IWebHostEnvironment environment)
{
    public const string SiteKey = "0x4AAAAAAF8y76GGmumUX1_K";
    public const string Error = "Human verification failed. Please try again.";

    public async Task<bool> VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null)
    {
        var secret = configuration["TURNSTILE_SECRET"];
        var hostnames = (configuration["TURNSTILE_HOSTNAMES"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(token)
            || token.Length > 2048
            || string.IsNullOrWhiteSpace(secret)
            || hostnames.Length == 0)
            return false;

        try
        {
            var fields = new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                fields["remoteip"] = remoteIp;
            using var form = new FormUrlEncodedContent(fields);
            using var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", form, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<VerificationResult>(ct);
            return result is { Success: true, Hostname: not null }
                   && result.Action == action
                   && hostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase)
                   && (environment.IsDevelopment() || !IsLocalhost(result.Hostname));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return false;
        }
    }

    private static bool IsLocalhost(string hostname)
    {
        hostname = hostname.TrimEnd('.');
        return hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || hostname.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
               || IPAddress.TryParse(hostname, out var ip) && IPAddress.IsLoopback(ip);
    }

    private sealed record VerificationResult(bool Success, string? Hostname, string? Action);
}
