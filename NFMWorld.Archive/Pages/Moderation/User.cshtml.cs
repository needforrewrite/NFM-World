using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Moderation;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Pages.Moderation;

/// <summary>
/// The whole surface for one account: role, name, password, ban/mute/kick, content and deletion.
/// Each control is its own form so the antiforgery filter applies per handler.
/// </summary>
public sealed class UserModel(
    ArchiveDbContext db,
    ModerationService moderation
)
    : ArchivePageModel(db)
{
    [BindProperty] public int TargetId { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? Duration { get; set; }
    [BindProperty] public string? Argument { get; set; }
    [BindProperty] public string? ConfirmName { get; set; }

    public Data.User? Target { get; private set; }
    public string? Status { get; private set; }
    public IReadOnlyList<Asset> Assets { get; private set; } = [];
    public IReadOnlyList<ModerationAction> Actions { get; private set; } = [];

    private async Task<IActionResult?> LoadAsync(int id)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();
        if (!IsModerator)
            return NotFound();

        Target = await Db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (Target is null)
            return NotFound();

        Assets = await Db.Assets.Where(a => a.OwnerId == id).OrderByDescending(a => a.Id).Take(50).ToListAsync();
        Actions = await Db.ModerationActions
            .Where(a => a.TargetUserId == id)
            .OrderByDescending(a => a.Id)
            .Take(50)
            .ToListAsync();
        ViewData["Title"] = "Moderate " + Target.Username;
        return null;
    }

    public Task<IActionResult> OnGetAsync(int id) => PageAfterLoad(id);

    private async Task<IActionResult> PageAfterLoad(int id)
    {
        var redirect = await LoadAsync(id);
        return redirect ?? (IActionResult)Page();
    }

    private async Task<IActionResult> ApplyAsync(
        int id,
        ModerationActionKind action,
        TimeSpan? duration = null,
        string? argument = null
    )
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        var outcome = await moderation.ApplyAsync(
            Account!,
            new ModerationRequest(action, id, Reason, duration, argument),
            HttpContext,
            HttpContext.RequestAborted
        );
        Status = outcome.Ok ? $"{action} applied." : outcome.Error;
        return Page();
    }

    private TimeSpan? ParseDuration()
    {
        if (string.IsNullOrWhiteSpace(Duration))
            return null;
        return ModerationDurations.TryParse(Duration.Trim(), out var value) ? value : null;
    }

    public Task<IActionResult> OnPostBanAsync(int id) => ApplyAsync(id, ModerationActionKind.Ban);
    public Task<IActionResult> OnPostTempBanAsync(int id) => ApplyAsync(id, ModerationActionKind.TempBan, ParseDuration());
    public Task<IActionResult> OnPostUnbanAsync(int id) => ApplyAsync(id, ModerationActionKind.Unban);
    public Task<IActionResult> OnPostMuteAsync(int id) => ApplyAsync(id, ModerationActionKind.Mute);
    public Task<IActionResult> OnPostTempMuteAsync(int id) => ApplyAsync(id, ModerationActionKind.TempMute, ParseDuration());
    public Task<IActionResult> OnPostUnmuteAsync(int id) => ApplyAsync(id, ModerationActionKind.Unmute);
    public Task<IActionResult> OnPostKickAsync(int id) => ApplyAsync(id, ModerationActionKind.Kick);
    public Task<IActionResult> OnPostTempKickAsync(int id) => ApplyAsync(id, ModerationActionKind.TempKick, ParseDuration());
    public Task<IActionResult> OnPostUnlistAsync(int id) => ApplyAsync(id, ModerationActionKind.Unlist);
    public Task<IActionResult> OnPostRelistAsync(int id) => ApplyAsync(id, ModerationActionKind.Relist);
    public Task<IActionResult> OnPostRenameAsync(int id) => ApplyAsync(id, ModerationActionKind.Rename, argument: Argument);
    public Task<IActionResult> OnPostSetPasswordAsync(int id) => ApplyAsync(
        id,
        ModerationActionKind.SetPassword,
        argument: Argument
    );

    public Task<IActionResult> OnPostSetRoleAsync(int id) => ApplyAsync(id, ModerationActionKind.SetRole, argument: Argument);
    public Task<IActionResult> OnPostVerifyUploaderAsync(int id) => ApplyAsync(id, ModerationActionKind.VerifyUploader);
    public Task<IActionResult> OnPostRevokeUploaderVerificationAsync(int id) => ApplyAsync(
        id,
        ModerationActionKind.RevokeUploaderVerification
    );

    public Task<IActionResult> OnPostDeleteRequestAsync(int id) => ApplyAsync(id, ModerationActionKind.DeleteRequest);
    public Task<IActionResult> OnPostDeleteCancelAsync(int id) => ApplyAsync(id, ModerationActionKind.DeleteCancel);

    public async Task<IActionResult> OnPostDeleteEraseAsync(int id)
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        if (ConfirmName != Target!.Username)
        {
            Status = "Type the account's name to confirm erasure.";
            return Page();
        }

        return await ApplyAsync(id, ModerationActionKind.DeleteErase);
    }
}
