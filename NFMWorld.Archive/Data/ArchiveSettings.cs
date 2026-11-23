using Microsoft.EntityFrameworkCore;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Data;

public sealed class ArchiveSettings
{
    public const int BytesPerMiB = 1024 * 1024;
    public const int MaxUploadMiB = RadpackLimits.MaxCompressedBytes / BytesPerMiB;
    public const int DefaultUploadMiB = 4;
    public const int SingletonId = 1;

    public int Id { get; set; }
    public int UploadLimitMiB { get; set; }

    public static Task<int> ReadUploadLimitMiBAsync(ArchiveDbContext db, CancellationToken ct)
        => db.Settings.Where(setting => setting.Id == SingletonId).Select(setting => setting.UploadLimitMiB)
            .SingleAsync(ct);

    public static async Task<int> ReadUploadLimitBytesAsync(ArchiveDbContext db, CancellationToken ct)
        => (await ReadUploadLimitMiBAsync(db, ct)) * BytesPerMiB;
}
