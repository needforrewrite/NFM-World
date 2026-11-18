using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Moderation;

/// <summary>
/// Erases accounts whose deletion request has outlived the grace period. Waits before its first
/// pass so a test that seeds an expired request is never raced by a startup erase.
/// </summary>
public sealed class DeletionSweeper(IServiceScopeFactory scopeFactory, ArchiveOptions options, ILogger<DeletionSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First pass after a full interval; the actual sweep reads DateTimeOffset.UtcNow itself.
        await Task.Delay(Interval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var outer = scopeFactory.CreateScope();
                var count = await SweepOnceAsync(outer.ServiceProvider, DateTimeOffset.UtcNow, stoppingToken);
                if (count > 0)
                    logger.LogInformation("Erased {Count} expired deletion requests", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deletion sweep failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// One pass. Scoped so the test can call it directly with its own service provider.
    /// </summary>
    public static async Task<int> SweepOnceAsync(
        IServiceProvider services,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var factory = services.GetRequiredService<IServiceScopeFactory>();
        var options = services.GetRequiredService<ArchiveOptions>();
        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
        var eraseLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AccountErasure");

        var cutoff = now - options.DeletionGracePeriod;
        var candidates = await db.Users
            .Where(u => u.DeletedAt == null && u.DeletionRequestedAt != null)
            .Select(u => new { u.Id, u.DeletionRequestedAt })
            .ToListAsync(ct);
        var dueIds = candidates
            .Where(u => u.DeletionRequestedAt is { } requested && requested <= cutoff)
            .Select(u => u.Id)
            .ToArray();
        var due = await db.Users.Where(u => dueIds.Contains(u.Id)).ToListAsync(ct);

        var erased = 0;
        foreach (var user in due)
            erased += await AccountErasure.EraseAsync(db, storage, user, eraseLogger, ct);

        return erased;
    }
}
