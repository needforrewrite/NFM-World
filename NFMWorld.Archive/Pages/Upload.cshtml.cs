using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Pages;

[RequestSizeLimit(AssetUpload.MaxRequestBytes)]
public sealed class UploadModel(
    ArchiveDbContext db,
    FileStorage storage,
    ValidatorRunner validator,
    ArchiveOptions options
)
    : ArchivePageModel(db)
{
    [BindProperty] public IFormFile? File { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? Type { get; set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public int MaxUploadBytes { get; private set; }

    /// <summary>The kinds the publish form offers. Code and replays are not published here.</summary>
    public IReadOnlyList<string> PublishableTypes { get; } =
        ["Car", "Stage", "StagePiece", "Wheel", "Texture"];

    public async Task<IActionResult> OnGetAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        MaxUploadBytes = await ArchiveSettings.ReadUploadLimitBytesAsync(Db, HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        MaxUploadBytes = await ArchiveSettings.ReadUploadLimitBytesAsync(Db, HttpContext.RequestAborted);
        if (options.ReadOnly)
        {
            Error = "This archive is not accepting uploads.";
            return Page();
        }

        if (File is null || !AssetKinds.TryParseName(Type, out var kind))
        {
            Error = "Choose a file, a name and a type.";
            return Page();
        }

        // The same helpers the JSON API uses: the form had its own copy of these rules and
        // silently lost the extension check, storing a .txt with no validation at all.
        if (AssetUpload.NormalizeName(Name) is not { } name)
        {
            Error = $"Choose a name of at most {AssetUpload.MaxNameLength} characters.";
            return Page();
        }

        var description = AssetUpload.NormalizeDescription(Description);
        if (AssetUpload.DescriptionTooLong(description))
        {
            Error = $"The description must be at most {AssetUpload.MaxDescriptionLength} characters.";
            return Page();
        }

        if (File.Length > MaxUploadBytes)
        {
            Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            Error = AssetUpload.SizeLimitMessage(MaxUploadBytes);
            return Page();
        }

        if (AssetUpload.CheckClaim(kind, File.FileName) is { } claimError)
        {
            Error = claimError;
            return Page();
        }

        await using var source = File.OpenReadStream();
        var stored = await AssetUpload.StoreAndValidateAsync(
            storage,
            validator,
            Db,
            source,
            File.FileName,
            kind,
            MaxUploadBytes,
            HttpContext.RequestAborted
        );
        if (stored.Error is { } validationError)
        {
            Error = validationError;
            return Page();
        }

        var existing = await Db.Assets.FirstOrDefaultAsync(a => a.ContentHash == stored.Hash);
        if (existing is not null)
        {
            if ((byte)existing.Type != kind)
            {
                Error = $"Those bytes are already catalogued as a {AssetKinds.Name((byte)existing.Type)}.";
                return Page();
            }

            if (!existing.IsValidated)
            {
                existing.IsValidated = true;
                await Db.SaveChangesAsync();
            }

            Message = $"Already present as {existing.Name} ({stored.Hash}).";
            return Page();
        }

        Db.Assets.Add(
            new Asset
            {
                OwnerId = Account!.Id,
                Type = (AssetType)kind,
                Name = name,
                Description = description,
                ContentHash = stored.Hash,
                StoragePath = stored.RelativePath,
                SizeBytes = stored.SizeBytes,
                IsPublic = true,
                IsValidated = true
            }
        );
        await Db.SaveChangesAsync();

        Message = $"Uploaded. Content hash: {stored.Hash}";
        return Page();
    }
}
