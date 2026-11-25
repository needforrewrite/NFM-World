using System.Text.Json;
using NFMWorldLibrary;
using NFMWorld.Gameplay;
using NFMWorldLibrary.Accounts;
using NFMWorldLibrary.Multiplayer;

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
    private static string Path => ArchiveEndpoint.ClientStorageSuffix is { } environment
        ? $"data/cfg/account-{environment}.json"
        : "data/cfg/account.json";

    public static (string Token, string Username, UserRole Role, DateTimeOffset? DeletionPendingUntil)? Load()
    {
        try
        {
            if (!TheVFS.VFS.FileExists(Path))
                return null;

            var stored = JsonSerializer.Deserialize(TheVFS.VFS.ReadAllText(Path), AccountJsonContext.Default.StoredAccount);
            return stored is { Token.Length: > 0 }
                ? (stored.Token, stored.Username, stored.Role, stored.DeletionPendingUntil)
                : null;
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load the saved account: {ex.Message}");
            return null;
        }
    }

    public static void Save(string token, string username, UserRole role, DateTimeOffset? deletionPendingUntil)
    {
        try
        {
            using var stream = TheVFS.VFS.OpenWrite(Path);
            JsonSerializer.Serialize(
                stream,
                new StoredAccount(token, username, role, deletionPendingUntil),
                AccountJsonContext.Default.StoredAccount
            );
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
            JsonSerializer.Serialize(
                stream,
                new StoredAccount("", "", UserRole.User, null),
                AccountJsonContext.Default.StoredAccount
            );
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not clear the saved account: {ex.Message}");
        }
    }
}
