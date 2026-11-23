using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class AdministrationModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty] public int UploadLimitMiB { get; set; }
    [TempData] public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await ResolveAccountAsync() is null) return RedirectToLogin();
        if (!IsAdmin) return NotFound();

        UploadLimitMiB = await ArchiveSettings.ReadUploadLimitMiBAsync(Db, HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await ResolveAccountAsync() is null) return RedirectToLogin();
        if (!IsAdmin) return NotFound();

        if (UploadLimitMiB is < 1 or > ArchiveSettings.MaxUploadMiB)
            ModelState.AddModelError(
                nameof(UploadLimitMiB),
                $"Enter a whole number between 1 and {ArchiveSettings.MaxUploadMiB} MiB."
            );
        if (!ModelState.IsValid) return Page();

        await Db.Settings.Where(setting => setting.Id == ArchiveSettings.SingletonId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(setting => setting.UploadLimitMiB, UploadLimitMiB),
                HttpContext.RequestAborted
            );

        Message = "Archive settings saved.";
        return RedirectToPage();
    }
}
