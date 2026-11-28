using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Cache;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Moderation;

/// <summary>The numeric action over the wire; <c>DurationSeconds</c> becomes the TimeSpan.</summary>
public sealed record ModerationActionRequest(
    ModerationActionKind Action,
    int TargetUserId,
    string? Reason,
    [property: JsonPropertyName("durationSeconds")] long? DurationSeconds,
    string? Argument
)
{
    /// <summary>Null and no error only when the duration is absent or expressible.</summary>
    public TimeSpan? Duration
    {
        get
        {
            if (DurationSeconds is not { } seconds)

                return null;
            if (seconds <= 0 || seconds > (long)TimeSpan.FromDays(3650).TotalSeconds)

                return null;

            return TimeSpan.FromSeconds(seconds);
        }
    }

    public bool DurationInvalid => DurationSeconds is { } seconds && Duration is null;
}

public sealed record ModerationRequest(
    ModerationActionKind Action,
    int TargetUserId,
    string? Reason,
    TimeSpan? Duration,
    string? Argument
);

public sealed record ModerationOutcome(bool Ok, string? Error, User? Target);

/// <summary>
/// The one place an action is applied, so the web UI and the lobby's signed event endpoint cannot
/// drift: same authorisation, same audit row, same column changes.
/// </summary>
public sealed class ModerationService(ArchiveDbContext db, FileStorage storage, ILogger<ModerationService> logger)
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(3650);

    public async Task<ModerationOutcome> ApplyAsync(
        User actor,
        ModerationRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;

        if (actor.IsBanned(now))

            return new ModerationOutcome(false, "Your account is banned.", null);

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId, ct);

        if (target is null)

            return new ModerationOutcome(false, "No such account.", null);

        if (request.Duration is { } duration && duration <= TimeSpan.Zero)

            return new ModerationOutcome(false, "The duration must be positive.", target);
        if (request.Duration is { } tooLong && tooLong > MaxDuration)

            return new ModerationOutcome(false, "The duration is too long.", target);

        // Self-service deletion is the caller acting on themselves. Everything else needs staff
        // standing, and DeleteErase additionally the admin gate in the switch below.
        var selfDeletion = request.Action is ModerationActionKind.DeleteRequest or ModerationActionKind.DeleteCancel
                           && actor.Id == target.Id;
        var uploaderVerification = request.Action is ModerationActionKind.VerifyUploader or ModerationActionKind
            .RevokeUploaderVerification;

        if (uploaderVerification && !actor.IsAdmin)

            return new ModerationOutcome(false, "This needs an administrator.", target);
        if (!selfDeletion && !uploaderVerification && !Roles.CanModerate(actor, target))

            return new ModerationOutcome(false, "You may not moderate that account.", target);

        switch (request.Action)
        {
            // Admin-only actions — except erasing your own account, which is the website's
            // self-service flow and needs no staff standing.
            case ModerationActionKind.Rename:
            case ModerationActionKind.SetPassword:
            case ModerationActionKind.SetRole:
            case ModerationActionKind.DeleteErase:

                if (actor.Id != target.Id && !Roles.CanAdminister(actor, target))

                    return new ModerationOutcome(false, "This needs an administrator.", target);

                break;

            case ModerationActionKind.Unlist:
            case ModerationActionKind.Relist:
            case ModerationActionKind.Kick:
            case ModerationActionKind.TempKick:
            case ModerationActionKind.Ban:
            case ModerationActionKind.TempBan:
            case ModerationActionKind.Unban:
            case ModerationActionKind.Mute:
            case ModerationActionKind.TempMute:
            case ModerationActionKind.Unmute:

                break;

            case ModerationActionKind.DeleteRequest:
            case ModerationActionKind.DeleteCancel:
            case ModerationActionKind.VerifyUploader:
            case ModerationActionKind.RevokeUploaderVerification:

                break;

            default:

                return new ModerationOutcome(false, "Unsupported action.", target);
        }

        switch (request.Action)
        {
            case ModerationActionKind.Kick:
                Bump(target);

                break;

            case ModerationActionKind.TempKick:

                if (request.Duration is not { } kickLength)

                    return new ModerationOutcome(false, "A temporary kick needs a duration.", target);
                target.KickedUntil = now + kickLength;
                Bump(target);

                break;

            case ModerationActionKind.Ban:
                target.BannedUntil = DateTimeOffset.MaxValue;
                target.BanReason = request.Reason;
                Bump(target);

                break;

            case ModerationActionKind.TempBan:

                if (request.Duration is not { } banLength)

                    return new ModerationOutcome(false, "A temporary ban needs a duration.", target);
                target.BannedUntil = now + banLength;
                target.BanReason = request.Reason;
                Bump(target);

                break;

            case ModerationActionKind.Unban:
                target.BannedUntil = null;
                target.BanReason = null;
                Bump(target);

                break;

            case ModerationActionKind.Mute:
                target.MutedUntil = DateTimeOffset.MaxValue;
                target.MuteReason = request.Reason;
                Bump(target);

                break;

            case ModerationActionKind.TempMute:

                if (request.Duration is not { } muteLength)

                    return new ModerationOutcome(false, "A temporary mute needs a duration.", target);
                target.MutedUntil = now + muteLength;
                target.MuteReason = request.Reason;
                Bump(target);

                break;

            case ModerationActionKind.Unmute:
                target.MutedUntil = null;
                target.MuteReason = null;
                Bump(target);

                break;

            case ModerationActionKind.Unlist:
            case ModerationActionKind.Relist:
            {
                var isTakedown = request.Action == ModerationActionKind.Unlist;
                // ExecuteUpdate bypasses the tracked rows, so the hashes are read here: the edge
                // copy of each upload has to go, not just the origin's visibility bit.
                var hashes = await db.Assets.Where(a => a.OwnerId == target.Id)
                    .Select(a => a.ContentHash)
                    .ToListAsync(ct);
                await db.Assets.Where(a => a.OwnerId == target.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsTakedown, isTakedown), ct);
                await http.PurgeAssetsAsync(hashes);

                break;
            }

            case ModerationActionKind.Rename:

                if (await RenameAsync(target, request.Argument, ct) is { } renameError)

                    return new ModerationOutcome(false, renameError, target);

                break;

            case ModerationActionKind.SetPassword:

                if (await SetPasswordAsync(target, request.Argument, ct) is { } passwordError)

                    return new ModerationOutcome(false, passwordError, target);

                break;

            case ModerationActionKind.SetRole:

                if (!Enum.TryParse<UserRole>(request.Argument, ignoreCase: true, out var role))

                    return new ModerationOutcome(
                        false,
                        "The role must be one of: " + string.Join(", ", Enum.GetNames<UserRole>()) + ".",
                        target
                    );
                target.Role = role;
                Bump(target);

                break;

            case ModerationActionKind.VerifyUploader:
            case ModerationActionKind.RevokeUploaderVerification:

                if (target.IsDeleted)

                    return new ModerationOutcome(false, "That account is already erased.", target);
                target.IsVerifiedUploader = request.Action == ModerationActionKind.VerifyUploader;

                break;

            case ModerationActionKind.DeleteRequest:

                if (target.IsDeleted)

                    return new ModerationOutcome(false, "That account is already erased.", target);
                target.DeletionRequestedAt = now;

                break;

            case ModerationActionKind.DeleteCancel:
                target.DeletionRequestedAt = null;

                break;

            case ModerationActionKind.DeleteErase:
                // The log row keeps the pre-erase name: EraseAsync overwrites the user's name with
                // the tombstone one, and the audit trail must still say who was erased.
                await WriteAuditAsync(actor, target, request, http, expiresAt: null, ct);
                await AccountErasure.EraseAsync(db, storage, target, logger, ct);

                return new ModerationOutcome(true, null, target);

            default:

                return new ModerationOutcome(false, "Unsupported action.", target);
        }

        await WriteAuditAsync(
            actor,
            target,
            request,
            http,
            request.Duration is { } length ? now + length : null,
            ct
        );
        await db.SaveChangesAsync(ct);

        return new ModerationOutcome(true, null, target);
    }

    private async Task WriteAuditAsync(
        User actor,
        User target,
        ModerationRequest request,
        HttpContext http,
        DateTimeOffset? expiresAt,
        CancellationToken ct
    )
    {
        db.ModerationActions.Add(
            new ModerationAction
            {
                ActorUserId = actor.Id,
                ActorName = actor.Username,
                TargetUserId = target.Id,
                TargetName = target.Username,
                Action = request.Action,
                Reason = request.Reason,
                ExpiresAt = expiresAt
            }
        );

        db.AuthAudit.Add(
            new AuthAudit
            {
                UserId = actor.Id,
                Event = "moderation",
                Detail = request.Action.ToString(),
                IpAddress = http.Connection.RemoteIpAddress?.ToString()
            }
        );
    }

    private static void Bump(User target) => target.ModerationRevision++;

    private async Task<string?> RenameAsync(User target, string? argument, CancellationToken ct)
    {
        if (!UsernamePolicy.TryValidate(argument, out var nameError))

            return nameError;

        var normalized = UsernamePolicy.Normalize(argument!);
        var taken = await db.Users.AnyAsync(
            u => u.UsernameNormalized == normalized && u.Id != target.Id,
            ct
        );

        if (taken)

            return "That username is taken.";

        target.Username = argument!;
        target.UsernameNormalized = normalized;

        // Deliberately no SecurityStamp bump: renaming must not sign the account out.

        return null;
    }

    private async Task<string?> SetPasswordAsync(User target, string? argument, CancellationToken ct)
    {
        if (!PasswordPolicy.TryValidate(argument, out var passwordError))

            return passwordError;

        target.PasswordHash = PasswordHasher.Hash(argument!);
        target.SecurityStamp++;
        await db.Sessions.Where(s => s.UserId == target.Id).ExecuteDeleteAsync(ct);
        Bump(target);

        return null;
    }
}
