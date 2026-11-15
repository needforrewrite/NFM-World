using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Files;

namespace NFMWorld.Archive.Pages;

public sealed class LeaderboardsModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? StageHash { get; set; }
    [BindProperty(SupportsGet = true)] public string? CarHash { get; set; }

    public IReadOnlyList<TimeTrial> Entries { get; private set; } = [];

    /// <summary>Replays recorded under older physics: listed for watching, never ranked.</summary>
    public IReadOnlyList<TimeTrial> OutdatedEntries { get; private set; } = [];

    public IReadOnlyList<(string StageHash, string StageName, string CarHash, string CarName)> Boards { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();

        // Distinct stage/car pairs that have at least one entry: the boards a visitor can pick.
        // The names come along with the group rather than a second join, which would pair every
        // row with every other row of the same board.
        Boards = (await Db.TimeTrials.Where(t => t.IsValid)
            .GroupBy(t => new { t.StageHash, t.CarHash })
            .Select(g => new
                    {
                        g.Key.StageHash,
                        g.Key.CarHash,
                        StageName = g.Min(t => t.StageName),
                        CarName = g.Min(t => t.CarName)
                    }
                )
            .OrderBy(b => b.StageName)
            .Take(200)
            .ToListAsync())
            .Select(b => (b.StageHash, b.StageName, b.CarHash, b.CarName))
            .ToList();

        if (string.IsNullOrEmpty(StageHash) && Boards.Count > 0)
        {
            StageHash = Boards[0].StageHash;
            CarHash = Boards[0].CarHash;
        }

        if (!string.IsNullOrEmpty(StageHash) && !string.IsNullOrEmpty(CarHash))
        {
            var board = Db.TimeTrials
                .Include(t => t.User)
                .Where(t => t.StageHash == StageHash && t.CarHash == CarHash && t.IsValid)
                .OrderBy(t => t.TotalTicks);
            Entries = await board.Where(t => t.ReplayVersion == SavedTimeTrial.CURRENT_VERSION).Take(100).ToListAsync();
            OutdatedEntries = await board.Where(t => t.ReplayVersion != SavedTimeTrial.CURRENT_VERSION).Take(100).ToListAsync();
        }

        return Page();
    }
}
