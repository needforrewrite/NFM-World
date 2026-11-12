using System.Diagnostics.CodeAnalysis;

namespace NFMWorld.Sentry;

/// <summary>
/// Parsed Sentry DSN information. Format: {protocol}://{public_key}@{host}/{project_id}
/// </summary>
public readonly struct DsnInfo
{
    public string Protocol { get; }
    public string PublicKey { get; }
    public string Host { get; }
    public string ProjectId { get; }
    public Uri EnvelopeUri { get; }

    private DsnInfo(string protocol, string publicKey, string host, string projectId)
    {
        Protocol = protocol;
        PublicKey = publicKey;
        Host = host;
        ProjectId = projectId;
        EnvelopeUri = new Uri($"{protocol}://{host}/api/{projectId}/envelope/");
    }

    /// <summary>
    /// Parse a Sentry DSN string.
    /// </summary>
    public static bool TryParse(string? dsn, [NotNullWhen(true)] out DsnInfo result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(dsn))
            return false;

        // Expected format: {protocol}://{public_key}@{host}/{project_id}
        // e.g. https://abc123@example.com/42
        var uri = new Uri(dsn);

        var protocol = uri.Scheme;
        if (string.IsNullOrEmpty(protocol))
            return false;

        var host = uri.Host;
        if (string.IsNullOrEmpty(host))
            return false;

        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        host = $"{host}{port}";

        var publicKey = uri.UserInfo;
        if (string.IsNullOrEmpty(publicKey))
            return false;

        // Project ID is the last path segment
        var path = uri.AbsolutePath.Trim('/');
        var lastSlash = path.LastIndexOf('/');
        var projectId = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

        if (string.IsNullOrEmpty(projectId))
            return false;

        result = new DsnInfo(protocol, publicKey, host, projectId);
        return true;
    }

    /// <summary>
    /// Parse a Sentry DSN string, throwing if invalid.
    /// </summary>
    public static DsnInfo Parse(string dsn)
    {
        if (!TryParse(dsn, out var result))
            throw new ArgumentException($"Invalid Sentry DSN: '{dsn}'");
        return result;
    }
}
