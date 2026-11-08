using System.Security.Cryptography;
using System.Text;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// HMAC-SHA256 request signing for service-to-service authentication.
/// 
/// Wire format:
///   Authorization: HMAC-SHA256 keyId={id},ts={unixSec},sig={hex}
/// 
/// Signature covers: {method}\n{path}\n{timestamp}\n{hexBodyHash}
/// Rejects requests outside ±maxClockSkewSec of server time.
/// 
/// Key rotation: add new keyId→secret pairs on the server, update the client's
/// keyId, remove old key from server once no in-flight requests remain.
/// </summary>
public static class HmacAuth
{
    private const string Scheme = "HMAC-SHA256";

    /// <summary>Builds the Authorization header value for an outgoing request.</summary>
    public static string Sign(
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string keyId,
        ReadOnlySpan<byte> secretKey
    )
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bodyHash = SHA256.HashData(body);
        var hexBodyHash = Convert.ToHexStringLower(bodyHash);
        var stringToSign = $"{method}\n{path}\n{ts}\n{hexBodyHash}";

        var signature = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes(stringToSign)
        );

        return $"{Scheme} keyId={keyId},ts={ts},sig={Convert.ToHexStringLower(signature)}";
    }

    /// <summary>
    /// Signs with a nonce folded into the timestamp field as <c>ts.nonce</c>, so a captured
    /// request stays inside the clock window but is only accepted once. Use this for endpoints
    /// where a replay would change state (match results, bans); the plain overload is fine for
    /// read-only work like creating a race.
    /// </summary>
    public static string SignWithNonce(
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string keyId,
        ReadOnlySpan<byte> secretKey,
        string nonce
    )
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bodyHash = SHA256.HashData(body);
        var hexBodyHash = Convert.ToHexStringLower(bodyHash);
        var stringToSign = $"{method}\n{path}\n{ts}.{nonce}\n{hexBodyHash}";

        var signature = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(stringToSign));

        return $"{Scheme} keyId={keyId},ts={ts}.{nonce},sig={Convert.ToHexStringLower(signature)}";
    }

    /// <summary>
    /// Verifies an incoming request's Authorization header.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public static string? Verify(
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string? authHeader,
        IReadOnlyDictionary<string, byte[]> knownKeys,
        int maxClockSkewSec = 30
    )
        => Verify(method, path, body, authHeader, knownKeys, out _, out _, maxClockSkewSec);

    /// <summary>
    /// As above, also reporting the verified key id and the nonce (empty when the caller signed
    /// without one) so the caller can check replays against its own nonce cache.
    /// </summary>
    public static string? Verify(
        string method,
        string path,
        ReadOnlySpan<byte> body,
        string? authHeader,
        IReadOnlyDictionary<string, byte[]> knownKeys,
        out string? keyIdOut,
        out string nonce,
        int maxClockSkewSec = 30
    )
    {
        keyIdOut = null;
        nonce = "";
        if (string.IsNullOrEmpty(authHeader))
            return "Missing Authorization header";

        if (!authHeader.StartsWith(Scheme + " ", StringComparison.Ordinal))
            return $"Expected {Scheme} scheme";

        // Parse keyId, ts, sig from: HMAC-SHA256 keyId=X,ts=Y,sig=Z
        var parts = authHeader[(Scheme.Length + 1)..];
        string? keyId = null;
        string? tsField = null;
        string? sig = null;

        foreach (var part in parts.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("keyId="))
                keyId = trimmed[6..];
            else if (trimmed.StartsWith("ts="))
                tsField = trimmed[3..];
            else if (trimmed.StartsWith("sig="))
                sig = trimmed[4..];
        }

        if (keyId is null || tsField is null || sig is null)
            return "Malformed Authorization header: missing keyId, ts, or sig";

        // ts may carry a nonce as "ts.nonce"; the signature covers the field verbatim.
        var dot = tsField.IndexOf('.');
        var tsText = dot < 0 ? tsField : tsField[..dot];
        var nonceText = dot < 0 ? "" : tsField[(dot + 1)..];
        if (!long.TryParse(tsText, out var ts) || ts == 0)
            return "Malformed Authorization header: bad timestamp";

        // Replay check
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > maxClockSkewSec)
            return $"Timestamp outside ±{maxClockSkewSec}s window";

        // Look up key
        if (!knownKeys.TryGetValue(keyId, out var secretKey))
            return $"Unknown keyId: {keyId}";

        // Reconstruct signature
        var bodyHash = SHA256.HashData(body);
        var hexBodyHash = Convert.ToHexStringLower(bodyHash);
        var stringToSign = $"{method}\n{path}\n{tsField}\n{hexBodyHash}";

        var expectedSig = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes(stringToSign)
        );
        var expectedHex = Convert.ToHexStringLower(expectedSig);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHex),
                Encoding.UTF8.GetBytes(sig)
            ))
            return "Signature mismatch";

        keyIdOut = keyId;
        nonce = nonceText;
        return null; // Success
    }

    /// <summary>Parses known HMAC keys from a key=base64,... string (env var format).</summary>
    public static Dictionary<string, byte[]> ParseKnownKeys(string keysConfig)
    {
        var keys = new Dictionary<string, byte[]>();
        foreach (var pair in keysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;

            var keyId = pair[..eq].Trim();
            var secretB64 = pair[(eq + 1)..].Trim();

            if (keyId.Length > 0 && secretB64.Length > 0)
                keys[keyId] = Convert.FromBase64String(secretB64);
        }

        return keys;
    }
}
