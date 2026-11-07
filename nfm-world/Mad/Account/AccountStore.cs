using System.Text.Json;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.Accounts;

/// <summary>
/// Persists the session token between runs.
///
/// It goes through the VFS write root like every other saved game file, which means it lands in
/// the per-user directory on macOS and the current directory elsewhere. It is a bearer token, so
/// it is kept out of anything logged or shown; the username is stored beside it only so the menu
/// can show who is signed in before the first round trip.
/// </summary>
public static class AccountStore
{
    private const string Path = "data/cfg/account.json";

    private sealed record Stored(string Token, string Username);

    public static (string Token, string Username)? Load()
    {
        try
        {
            if (!TheVFS.VFS.FileExists(Path))
                return null;

            var stored = JsonSerializer.Deserialize<Stored>(TheVFS.VFS.ReadAllText(Path));
            return stored is { Token.Length: > 0 } ? (stored.Token, stored.Username) : null;
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load the saved account: {ex.Message}");
            return null;
        }
    }

    public static void Save(string token, string username)
    {
        try
        {
            using var stream = TheVFS.VFS.OpenWrite(Path);
            JsonSerializer.Serialize(stream, new Stored(token, username));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not save the account: {ex.Message}");
        }
    }

    /// <summary>
    /// Forgets the session. The VFS has no delete, so the file is overwritten with a blank token,
    /// which <see cref="Load"/> already treats as signed out.
    /// </summary>
    public static void Clear()
    {
        try
        {
            using var stream = TheVFS.VFS.OpenWrite(Path);
            JsonSerializer.Serialize(stream, new Stored("", ""));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not clear the saved account: {ex.Message}");
        }
    }
}
