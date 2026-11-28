using Microsoft.AspNetCore.Mvc;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Moderation;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Pages.Account;

/// <summary>
/// Self-service account deletion: request, then cancel while the grace period runs. The erase
/// itself runs on a timer once the grace period has passed, or immediately on the moderation page.
/// </summary>
public sealed class DeletionModel(
    ArchiveDbContext db,
    ModerationService moderation,
    ArchiveOptions options
)
    : ArchivePageModel(db)
{
    public DateTimeOffset? RequestedAt { get; private set; }
    public DateTimeOffset? EraseAt { get; private set; }
    public string? Status { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        RequestedAt = Account!.DeletionRequestedAt;
        EraseAt = RequestedAt is { } requested ? requested + options.DeletionGracePeriod : null;

        return Page();
    }

    public async Task<IActionResult> OnPostRequestAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        var outcome = await moderation.ApplyAsync(
            Account!,
            new ModerationRequest(ModerationActionKind.DeleteRequest, Account!.Id, null, null, null),
            HttpContext,
            HttpContext.RequestAborted
        );

        Status = outcome.Ok
            ? "Deletion requested. You can cancel it until the erase date."
            : outcome.Error;

        return await OnGetAsync();
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        if (await ResolveAccountAsync() is null)

            return RedirectToLogin();

        var outcome = await moderation.ApplyAsync(
            Account!,
            new ModerationRequest(ModerationActionKind.DeleteCancel, Account!.Id, null, null, null),
            HttpContext,
            HttpContext.RequestAborted
        );

        Status = outcome.Ok ? "Deletion cancelled." : outcome.Error;

        return await OnGetAsync();
    }
}
