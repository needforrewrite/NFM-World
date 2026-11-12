using System.Diagnostics;
using System.Net.Http.Headers;

namespace NFMWorld.Sentry;

/// <summary>
/// Handles HTTP transport of serialized envelopes to the Sentry server.
/// </summary>
public class SentryHttpTransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SentryOptions _options;

    public SentryHttpTransport(SentryOptions options)
    {
        _options = options;
        _httpClient = new HttpClient
        {
            Timeout = options.RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("NFMWorld.Sentry", "1.0.0")
        );
    }

    /// <summary>
    /// Send a serialized envelope to the Sentry server.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public async Task<string?> SendEnvelopeAsync(
        byte[] envelopeBytes,
        DsnInfo dsn,
        CancellationToken ct = default
    )
    {
        try
        {
            using var content = new ByteArrayContent(envelopeBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-sentry-envelope");

            // Sentry auth header
            var authHeader = $"Sentry sentry_version=7, sentry_client=NFMWorld.Sentry/1.0.0, sentry_key={dsn.PublicKey}";
            content.Headers.Add("X-Sentry-Auth", authHeader);

            var response = await _httpClient.PostAsync(dsn.EnvelopeUri, content, ct)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (_options.Debug)
                    Debug.WriteLine($"[Sentry] Envelope sent successfully ({envelopeBytes.Length} bytes)");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var error = $"Sentry returned {(int)response.StatusCode} {response.StatusCode}: {body}";
            if (_options.Debug)
                Debug.WriteLine($"[Sentry] {error}");
            return error;
        }
        catch (OperationCanceledException)
        {
            if (_options.Debug)
                Debug.WriteLine("[Sentry] Envelope upload cancelled");
            return "Upload cancelled";
        }
        catch (Exception ex)
        {
            if (_options.Debug)
                Debug.WriteLine($"[Sentry] Envelope upload failed: {ex.Message}");
            return ex.Message;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
