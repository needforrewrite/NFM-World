using System.ComponentModel.DataAnnotations.Schema;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Data;

public sealed class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    /// <summary>Case- and width-folded form of <see cref="Username"/>, the unique index.</summary>
    public required string UsernameNormalized { get; set; }

    /// <summary>Null for accounts that only sign in through Discord.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Staff level; <see cref="UserRole.User"/> for everyone else.</summary>
    public UserRole Role { get; set; }

    [NotMapped] public bool IsAdmin => Role == UserRole.Admin;

    [NotMapped] public bool IsModerator => Role >= UserRole.Moderator;

    /// <summary>
    /// <see cref="DateTimeOffset.MaxValue"/> for a permanent ban, null when never banned: "expired"
    /// and "never banned" must stay distinguishable.
    /// </summary>
    public DateTimeOffset? BannedUntil { get; set; }

    public string? BanReason { get; set; }

    /// <summary>A kick with a cooldown: refused lobby admission, but sign-in and uploads still work.</summary>
    public DateTimeOffset? KickedUntil { get; set; }

    public DateTimeOffset? MutedUntil { get; set; }
    public string? MuteReason { get; set; }

    /// <summary>When the account asked to be erased; the sweeper acts after the grace period.</summary>
    public DateTimeOffset? DeletionRequestedAt { get; set; }

    /// <summary>Set on erasure. The row survives as an anonymous tombstone that owns the uploads.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Bumped by every action a live lobby connection has to notice.</summary>
    public int ModerationRevision { get; set; }

    /// <summary>Bumped to invalidate every existing session, e.g. after a password change.</summary>
    public int SecurityStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }

    public List<Session> Sessions { get; set; } = [];
    public List<ExternalLogin> ExternalLogins { get; set; } = [];
    public List<TimeTrial> TimeTrials { get; set; } = [];
    public List<Rating> Ratings { get; set; } = [];

    public bool IsBanned(DateTimeOffset now) => BannedUntil is { } until && until > now;

    public bool IsMuted(DateTimeOffset now) => MutedUntil is { } until && until > now;

    public bool IsKicked(DateTimeOffset now) => KickedUntil is { } until && until > now;

    /// <summary>An erased account keeps its row as a tombstone; it is never offered as a person.</summary>
    [NotMapped]
    public bool IsDeleted => DeletedAt is not null;
}

/// <summary>
/// One applied moderation action, by web UI or chat command. No foreign key: the log must survive
/// the erasure of either side, and <see cref="TargetName"/> is the name as it was at the time.
/// </summary>
public sealed class ModerationAction
{
    public long Id { get; set; }

    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }

    public int? TargetUserId { get; set; }

    public required string TargetName { get; set; }

    public ModerationActionKind Action { get; set; }

    public string? Reason { get; set; }

    /// <summary>End of a temporary action; null for a permanent or instant one.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A login session. Only the hash of the token is stored: a database leak must not
/// hand out live sessions.
/// </summary>
public sealed class Session
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    /// <summary>Snapshot of the user's stamp when the session was issued; a mismatch revokes it.</summary>
    public int SecurityStamp { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class ExternalLogin
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Provider name, e.g. <c>discord</c>.</summary>
    public required string Provider { get; set; }

    /// <summary>The provider's immutable account id. Never the email: emails can change hands.</summary>
    public required string ProviderUserId { get; set; }
}

/// <summary>
/// A Discord OAuth flow that has started but not finished. Consumed exactly once.
/// </summary>
public sealed class OauthPending
{
    public int Id { get; set; }

    /// <summary>CSRF state parameter; hashed like a session token.</summary>
    public required string StateHash { get; set; }

    /// <summary>PKCE code verifier for the exchange.</summary>
    public required string CodeVerifier { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AuthAudit
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public int? UserId { get; set; }
    public required string Event { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
}

public enum AssetType : byte
{
    Car = 0,
    Stage = 1,
    StagePiece = 2,
    Wheel = 3,
    Texture = 4,
    Replay = 5,
    Gamemode = 6,
    Ai = 7
}

public sealed class Asset
{
    public int Id { get; set; }

    public int? OwnerId { get; set; }
    public User? Owner { get; set; }

    public AssetType Type { get; set; }

    /// <summary>Display name, supplied by the uploader.</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Lower-case hex SHA-256 of the stored bytes. This is what a replay pins.</summary>
    public required string ContentHash { get; set; }

    /// <summary>Path within the storage root, derived from the hash.</summary>
    public required string StoragePath { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>False while an upload is being validated; only public rows are listed.</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// True once the stored bytes have been read of the right kind by the out-of-process
    /// validator. Only validated data assets are advertised as multiplayer-eligible; a legacy
    /// row's claimed type is never trusted on its own.
    /// </summary>
    public bool IsValidated { get; set; }

    /// <summary>Set by moderation; a taken-down asset is never served.</summary>
    public bool IsTakedown { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int DownloadCount { get; set; }
}

public sealed class Clan
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NameNormalized { get; set; }
    public string? Tag { get; set; }
    public int? OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ClanMember> Members { get; set; } = [];
}

public sealed class ClanMember
{
    public int Id { get; set; }
    public int ClanId { get; set; }
    public Clan Clan { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A player's competitive rating, per gamemode. Ratings are never written from a client;
/// only a trusted result report changes them.
/// </summary>
public sealed class Rating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Gamemode id, e.g. <c>nfmm/pvp</c>.</summary>
    public required string Gamemode { get; set; }

    public double Elo { get; set; } = 1500;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Matches { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Match
{
    public int Id { get; set; }

    /// <summary>The lobby's match key. Unique, so a retried report is ignored rather than re-rated.</summary>
    public required string MatchKey { get; set; }

    /// <summary>Id of the trusted server (key id) that reported this result.</summary>
    public required string ResultServerId { get; set; }

    public required string Gamemode { get; set; }
    public required string StageName { get; set; }

    /// <summary>Content hash of the stage the race ran on, so a later edit cannot rewrite history.</summary>
    public string? StageHash { get; set; }

    public TimeSpan Duration { get; set; }
    public DateTimeOffset PlayedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MatchParticipant> Participants { get; set; } = [];
}

public sealed class MatchParticipant
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    /// <summary>Null when the reporter's player id does not map to a registered account.</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Zero-based finish position.</summary>
    public int FinishPosition { get; set; }

    public TimeSpan? FinishTime { get; set; }

    /// <summary>True when the participant was a bot; bots are recorded but never rated.</summary>
    public bool IsBot { get; set; }
}

/// <summary>
/// An accepted time trial. The tick count is the server's simulation result, not the client's
/// claim, so a leaderboard never exposes an unverified time.
/// </summary>
public sealed class TimeTrial
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Content hash of the stage the replay ran against.</summary>
    public required string StageHash { get; set; }

    /// <summary>Content hash of the car the replay ran with.</summary>
    public required string CarHash { get; set; }

    public required string StageName { get; set; }
    public required string CarName { get; set; }

    /// <summary>Ticks the server's simulation took to finish. The ranking key.</summary>
    public int TotalTicks { get; set; }

    /// <summary>Path within the storage root holding the replay bytes.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Replay format version, from the backend that recorded it.</summary>
    public int ReplayVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Marked when the assets it was validated against are later removed, so it stops ranking.</summary>
    public bool IsValid { get; set; } = true;
}
