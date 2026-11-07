using System.Buffers;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// Verifies HTTP requests signed by a trusted game server (the lobby). Shares
/// <see cref="HmacAuth"/>'s wire format with the lobby↔game-master channel, and additionally
/// requires a nonce so a captured request cannot be replayed: a results endpoint that could be
/// replayed would let anyone re-report a win as many times as they liked.
/// </summary>
public sealed class TrustedServerAuth(ArchiveOptions options, NonceCache nonces)
{
    private readonly IReadOnlyDictionary<string, byte[]> _keys = HmacAuth.ParseKnownKeys(options.TrustedServerKeys);

    /// <summary>
    /// Returns null when the request is authentic, or the reason it was refused. On success
    /// <paramref name="keyId"/> names the server that signed it.
    /// </summary>
    public string? Verify(string method, string path, ReadOnlySpan<byte> body, string? authHeader, out string? keyId)
    {
        keyId = null;
        if (_keys.Count == 0)
            return "No trusted server keys are configured.";

        var error = HmacAuth.Verify(method, path, body, authHeader, _keys, out keyId, out var nonce);
        if (error is not null)
            return error;

        if (!nonces.TryUse(keyId!, nonce))
            return "Nonce is missing or was already used.";

        return null;
    }

    /// <summary>Reads the body once and caps it, since it is untrusted until verified.</summary>
    public static async Task<byte[]> ReadBodyAsync(HttpRequest request, int maxBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new InvalidDataException($"Body exceeds {maxBytes} bytes");
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
