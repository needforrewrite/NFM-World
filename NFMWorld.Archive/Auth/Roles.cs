using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Auth;

/// <summary>
/// Who may act on whom. Deliberately does not consult <see cref="User.IsBanned"/>: a banned staff
/// member keeps their role, and the moderation entry points refuse a banned actor themselves.
/// </summary>
public static class Roles
{
    /// <summary>Moderator and up, on someone else and never on a peer or a superior.</summary>
    public static bool CanModerate(User actor, User target)
        => actor.Id != target.Id && target.Role < actor.Role && actor.IsModerator;

    /// <summary>
    /// <see cref="CanModerate"/> restricted to admins, which is what stops an admin from demoting
    /// another admin or themselves.
    /// </summary>
    public static bool CanAdminister(User actor, User target) => actor.IsAdmin && CanModerate(actor, target);
}
