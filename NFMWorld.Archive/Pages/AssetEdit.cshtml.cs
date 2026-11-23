using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Cache;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Pages;

/// <summary>
/// The owner's edit surface for one upload: metadata, visibility, a replacement file and deletion.
///
/// Every rule here is the JSON API's rule (see <see cref="AssetUpload"/>), because the game edits
/// the same rows through <c>PATCH /assets/{id}</c> and <c>PUT /assets/{id}/content</c>; a rule that
/// lives on only one of the two surfaces is a rule that can be bypassed from the other.
/// </summary>
[RequestSizeLimit(AssetUpload.MaxRequestBytes)]
public sealed class AssetEditModel(
    ArchiveDbContext db,
    FileStorage storage,
    ValidatorRunner validator,
    ArchiveOptions options
)
    : ArchivePageModel(db)
{
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public bool IsPublic { get; set; }
    [BindProperty] public bool IsTakedown { get; set; }
    [BindProperty] public IFormFile? Replacement { get; set; }

    public Asset? Asset { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public int MaxUploadBytes { get; private set; }

    /// <summary>True for the owner and for an administrator; anyone else never sees the page.</summary>
    public bool IsOwner { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        Name = Asset!.Name;
        Description = Asset.Description;
        IsPublic = Asset.IsPublic;
        IsTakedown = Asset.IsTakedown;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        if (options.ReadOnly)
            return Refuse("This archive is not accepting changes.");

        if (AssetUpload.NormalizeName(Name) is not { } name)
            return Refuse($"Choose a name of at most {AssetUpload.MaxNameLength} characters.");

        var description = AssetUpload.NormalizeDescription(Description);
        if (AssetUpload.DescriptionTooLong(description))
            return Refuse($"The description must be at most {AssetUpload.MaxDescriptionLength} characters.");

        // A non-owner moderator may only take a row down or restore it; an admin may change
        // everything, which is the "full CRUD" the role is meant to have.
        if (!IsOwner && !Account!.IsAdmin)
        {
            Asset!.IsTakedown = IsTakedown;
            await Db.SaveChangesAsync();
            await HttpContext.PurgeAssetsAsync([Asset.ContentHash]);
            return Redirect($"/asset/{Asset.Id}");
        }

        Asset!.Name = name;
        Asset.Description = description;
        Asset.IsPublic = IsPublic;

        // Takedown is moderation, not an owner's choice: a hidden-by-moderation asset must not
        // come back just because its author resubmitted the form.
        if (Account!.IsAdmin)
            Asset.IsTakedown = IsTakedown;
        await Db.SaveChangesAsync();
        // The download's Content-Disposition carries the name, and the edge holds the old copy.
        await HttpContext.PurgeAssetsAsync([Asset.ContentHash]);
        return Redirect($"/asset/{Asset.Id}");
    }

    public async Task<IActionResult> OnPostReplaceAsync(int id)
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        if (options.ReadOnly)
            return Refuse("This archive is not accepting changes.");

        if (!IsOwner)
            return Refuse("Only the author can replace an upload's file.");

        if (Replacement is null)
            return Refuse("Choose a file.");

        if (Replacement.Length > MaxUploadBytes)
        {
            Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return Refuse(AssetUpload.SizeLimitMessage(MaxUploadBytes));
        }

        var kind = (byte)Asset!.Type;
        if (AssetUpload.CheckClaim(kind, Replacement.FileName) is { } claimError)
            return Refuse(claimError);

        await using var source = Replacement.OpenReadStream();
        var stored = await AssetUpload.StoreAndValidateReplacementAsync(
            storage,
            validator,
            Db,
            source,
            kind,
            MaxUploadBytes,
            HttpContext.RequestAborted
        );
        if (stored.Error is { } validationError)
            return Refuse(validationError);

        if (string.Equals(stored.Hash, Asset.ContentHash, StringComparison.Ordinal))
        {
            Message = "Those are the bytes already published; nothing changed.";
            return Page();
        }

        var other = await Db.Assets.FirstOrDefaultAsync(
            a => a.ContentHash == stored.Hash && a.Id != Asset.Id,
            HttpContext.RequestAborted
        );
        if (other is not null)
            return Refuse($"Those bytes are already published as \"{other.Name}\".");

        var previousPath = Asset.StoragePath;
        var previousHash = Asset.ContentHash;
        Asset.ContentHash = stored.Hash;
        Asset.StoragePath = stored.RelativePath;
        Asset.SizeBytes = stored.SizeBytes;
        Asset.IsValidated = true;
        await Db.SaveChangesAsync();

        await storage.DeleteIfUnreferencedAsync(previousPath, Db, HttpContext.RequestAborted);
        await TimeTrialInvalidation.InvalidateForAssetAsync(Db, previousHash, HttpContext.RequestAborted);
        await HttpContext.PurgeAssetsAsync([previousHash]);

        Message = "The file was replaced. Time trials recorded against the old file no longer rank.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var redirect = await LoadAsync(id);
        if (redirect is not null)
            return redirect;

        if (options.ReadOnly)
            return Refuse("This archive is not accepting changes.");

        var path = Asset!.StoragePath;
        var hash = Asset.ContentHash;
        Db.Assets.Remove(Asset);
        await Db.SaveChangesAsync();

        await storage.DeleteIfUnreferencedAsync(path, Db, HttpContext.RequestAborted);
        await TimeTrialInvalidation.InvalidateForAssetAsync(Db, hash, HttpContext.RequestAborted);
        await HttpContext.PurgeAssetsAsync([hash]);

        return Redirect("/");
    }

    /// <summary>Loads the row and proves the viewer may edit it; null when the page can render.</summary>
    private async Task<IActionResult?> LoadAsync(int id)
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        // Deliberately not the public visibility rule: an owner must be able to reach a row they
        // have hidden, and a moderator a row they have taken down.
        Asset = await Db.Assets.Include(a => a.Owner).FirstOrDefaultAsync(a => a.Id == id);
        if (Asset is null)
            return NotFound();

        IsOwner = Asset.OwnerId == Account!.Id;
        if (!IsOwner && !Account.IsModerator)
            return NotFound();

        MaxUploadBytes = await ArchiveSettings.ReadUploadLimitBytesAsync(Db, HttpContext.RequestAborted);
        ViewData["Title"] = "Edit " + Asset.Name;
        return null;
    }

    private IActionResult Refuse(string message)
    {
        Error = message;
        Name = Asset!.Name;
        Description = Asset.Description;
        IsPublic = Asset.IsPublic;
        IsTakedown = Asset.IsTakedown;
        return Page();
    }

    public static string FormatSize(long bytes) => AssetDetailModel.FormatSize(bytes);
}
