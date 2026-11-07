using System.Text;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// Username rules, applied identically at registration and login normalisation.
/// </summary>
public static class UsernamePolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    /// <summary>Rejects or accepts a proposed username, with the reason when rejected.</summary>
    public static bool TryValidate(string? username, out string error)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            error = "Username is required.";
            return false;
        }

        if (username.Length < MinLength || username.Length > MaxLength)
        {
            error = $"Username must be {MinLength}-{MaxLength} characters.";
            return false;
        }

        foreach (var c in username)
        {
            if (char.IsControl(c))
            {
                error = "Username contains invalid characters.";
                return false;
            }

            // Anything that renders as a space, including non-breaking and zero-width forms,
            // is refused so two accounts cannot look identical.
            if (char.IsWhiteSpace(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
            {
                error = "Username cannot contain spaces.";
                return false;
            }
        }

        error = "";
        return true;
    }

    /// <summary>
    /// Folded form used for uniqueness and lookup: case-insensitive and width-insensitive, so
    /// "Ａlice" and "alice" cannot both exist.
    /// </summary>
    public static string Normalize(string username)
    {
        var normalized = username.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }
}

public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>
    /// Deliberately modest: length plus a few requirements, no forced composition trivia. The
    /// point is to stop trivially weak passwords without driving users to reuse a pattern.
    /// </summary>
    public static bool TryValidate(string? password, out string error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            error = $"Password must be at least {MinLength} characters.";
            return false;
        }

        if (password.Length > 256)
        {
            error = "Password is too long.";
            return false;
        }

        if (password.Any(char.IsWhiteSpace))
        {
            error = "Password cannot contain spaces.";
            return false;
        }

        error = "";
        return true;
    }
}
