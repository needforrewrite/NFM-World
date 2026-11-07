using System.Security.Cryptography;
using System.Text;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA512 password hashing.
///
/// The old archive stored <c>SHA512(salt + password)</c>, which is a single fast hash: every
/// leaked hash is recoverable at GPU speed. PBKDF2 is what the BCL ships natively and needs no
/// third-party package; the iteration count is stored in the hash so it can be raised later
/// without invalidating existing accounts.
///
/// Format: <c>pbkdf2-sha512$iterations$base64salt$base64hash</c>.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "pbkdf2-sha512";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashBytes);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifies a password against a stored hash, in constant time.</summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True when the stored hash is from the old archive's scheme and cannot be verified here.</summary>
    public static bool IsLegacyHash(string stored) => !stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

    /// <summary>Cost of a hash that is reused when no user matched, so a miss costs the same as a hit.</summary>
    public const string DummyHash =
        "pbkdf2-sha512$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
}
