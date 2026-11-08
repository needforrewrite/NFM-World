using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Pages;

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

    public async Task<IActionResult> OnGetAsync()
        => await ResolveAccountAsync() is null ? RedirectToLogin() : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await ResolveAccountAsync() is null)
            return RedirectToLogin();

        if (options.ReadOnly)
        {
            Error = "This archive is not accepting uploads.";
            return Page();
        }

        if (File is null || string.IsNullOrWhiteSpace(Name) || !Enum.TryParse<AssetType>(Type, true, out var type))
        {
            Error = "Choose a file, a name and a type.";
            return Page();
        }

        if (File.Length > RadpackLimits.MaxCompressedBytes)
        {
            Error = "That file is too large.";
            return Page();
        }

        await using var source = File.OpenReadStream();
        var (hash, relative, size) = await storage.PutAsync(source, RadpackLimits.MaxCompressedBytes, HttpContext.RequestAborted);

        var validation = await validator.RunAsync(
            "radpack",
            [Path.Combine(storage.Root, relative)],
            HttpContext.RequestAborted
        );
        if (!validation.Ok)
        {
            if (!await Db.Assets.AnyAsync(a => a.ContentHash == hash))
                storage.Delete(relative);
            Error = validation.Error ?? "That file could not be read as a radpack.";
            return Page();
        }

        if (validation.Output is { } output
            && output.TryGetProperty("type", out var validated)
            && !string.Equals(validated.GetString(), type.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (!await Db.Assets.AnyAsync(a => a.ContentHash == hash))
                storage.Delete(relative);
            Error = $"That file is a {validated.GetString()}, not a {type}.";
            return Page();
        }

        if (!await Db.Assets.AnyAsync(a => a.ContentHash == hash && a.Name == Name))
        {
            Db.Assets.Add(
                new Asset
                {
                    OwnerId = Account!.Id,
                    Type = type,
                    Name = Name!,
                    Description = Description,
                    ContentHash = hash,
                    StoragePath = relative,
                    SizeBytes = size,
                    IsPublic = true
                }
            );
            await Db.SaveChangesAsync();
        }

        Message = "Uploaded.";
        return Page();
    }
}
