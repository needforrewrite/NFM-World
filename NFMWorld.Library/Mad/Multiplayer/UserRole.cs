using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Account staff level. Ordered: a moderator may do everything a user may, an admin everything a
/// moderator may. Persisted as the <c>Users.Role</c> integer, so append only — never renumber.
/// </summary>
[PluginVisible]
public enum UserRole : byte
{
    User = 0,
    Moderator = 1,
    Admin = 2
}
