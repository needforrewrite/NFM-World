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

    /// <summary>The file formats the publish form accepts; the API applies the same pair.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".radpack", ".rad"
    };

    /// <summary>The kinds the publish form offers. Code and replays are not published here.</summary>
    public IReadOnlyList<string> PublishableTypes { get; } =
        ["Car", "Stage", "StagePiece", "Wheel", "Texture"];

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

        if (File is null || string.IsNullOrWhiteSpace(Name) || !AssetKinds.TryParseName(Type, out var kind))
        {
            Error = "Choose a file, a name and a type.";
            return Page();
        }

        if (!AssetKinds.IsPublishable(kind))
        {
            Error = $"{AssetKinds.Name(kind)} cannot be published as an asset.";
            return Page();
        }

        var extension = Path.GetExtension(File.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            Error = "Only .radpack and .rad files are accepted.";
            return Page();
        }

        if (!AssetKinds.IsUploadExtensionAllowed(kind, extension))
        {
            Error = "A legacy .rad file can only be a Car, StagePiece or Wheel.";
            return Page();
        }

        if (File.Length > RadpackLimits.MaxCompressedBytes)
        {
            Error = "That file is too large.";
            return Page();
        }

        await using var source = File.OpenReadStream();
        var (hash, relative, size) = await storage.PutAsync(source, RadpackLimits.MaxCompressedBytes, HttpContext.RequestAborted);

        var validation = extension.Equals(".radpack", StringComparison.OrdinalIgnoreCase)
            ? await validator.RunAsync("radpack", [Path.Combine(storage.Root, relative)], HttpContext.RequestAborted)
            : await validator.RunAsync(
                "rad",
                [Path.Combine(storage.Root, relative), AssetKinds.Name(kind)],
                HttpContext.RequestAborted
            );
        if (!validation.Ok)
        {
            // Content-addressed: another row may already point at these exact bytes, and a failed
            // re-upload must not delete the file it shares.
            await storage.DeleteIfUnreferencedAsync(relative, Db, HttpContext.RequestAborted);
            Error = validation.Error ?? "That file could not be read.";
            return Page();
        }

        if (validation.Output is { } output
            && output.TryGetProperty("kind", out var validated)
            && validated.GetString() is { } validatedKind
            && !string.Equals(validatedKind, AssetKinds.Name(kind), StringComparison.OrdinalIgnoreCase))
        {
            await storage.DeleteIfUnreferencedAsync(relative, Db, HttpContext.RequestAborted);
            Error = $"That file is a {validatedKind}, not a {AssetKinds.Name(kind)}.";
            return Page();
        }

        var existing = await Db.Assets.FirstOrDefaultAsync(a => a.ContentHash == hash);
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

            Message = $"Already present as {existing.Name} ({hash}).";
            return Page();
        }

        Db.Assets.Add(
            new Asset
            {
                OwnerId = Account!.Id,
                Type = (AssetType)kind,
                Name = Name!,
                Description = Description,
                ContentHash = hash,
                StoragePath = relative,
                SizeBytes = size,
                IsPublic = true,
                IsValidated = true
            }
        );
        await Db.SaveChangesAsync();

        Message = $"Uploaded. Content hash: {hash}";
        return Page();
    }
}
