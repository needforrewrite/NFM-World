using System.Security.Cryptography;
using System.Text;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// Opaque bearer tokens. The plaintext exists only in the response that issues it and in the
/// client's storage; the server keeps a SHA-256 of it. A database dump therefore leaks no usable
/// session, which is the opposite of the old archive's scheme where the server had to hold the
/// token itself to validate the client's per-request salt.
/// </summary>
public static class Tokens
{
    public static string Create() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Hex SHA-256, the only form ever persisted.</summary>
    public static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Splits a session token into its public selector and secret so lookup is an index hit rather
    /// than a scan over every session. The stored value is <c>selector</c> plus the hash of the
    /// secret half.
    /// </summary>
    public static (string Selector, string VerifierHash) Split(string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
            return (token, Hash(token));

        return (token[..dot], Hash(token[(dot + 1)..]));
    }

    public static string CreateSessionToken()
    {
        var selector = Base64UrlEncode(RandomNumberGenerator.GetBytes(8));
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return $"{selector}.{secret}";
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
