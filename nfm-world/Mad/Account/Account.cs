using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Accounts;

public record Account(string Token, string Username, UserRole Role = UserRole.User, DateTimeOffset? DeletionPendingUntil = null)
{
    public bool IsStaff => Role >= UserRole.Moderator;
}
