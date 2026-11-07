using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Pages;

public sealed class LeaderboardsModel(ArchiveDbContext db) : ArchivePageModel(db)
{
    [BindProperty(SupportsGet = true)] public string? StageHash { get; set; }
    [BindProperty(SupportsGet = true)] public string? CarHash { get; set; }

    public IReadOnlyList<TimeTrial> Entries { get; private set; } = [];
    public IReadOnlyList<(string StageHash, string StageName, string CarHash, string CarName)> Boards { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        await ResolveAccountAsync();

        // Distinct stage/car pairs that have at least one entry: the boards a visitor can pick.
        Boards = await Db.TimeTrials.Where(t => t.IsValid)
            .GroupBy(t => new { t.StageHash, t.CarHash })
            .Select(g => new { g.Key.StageHash, g.Key.CarHash })
            .Join(Db.TimeTrials, k => new { k.StageHash, k.CarHash }, t => new { t.StageHash, t.CarHash },
                (k, t) => new { k.StageHash, k.CarHash, t.StageName, t.CarName })
            .Distinct()
            .Take(200)
            .Select(x => ValueTuple.Create(x.StageHash, x.StageName, x.CarHash, x.CarName))
            .ToListAsync();

        if (string.IsNullOrEmpty(StageHash) && Boards.Count > 0)
        {
            StageHash = Boards[0].StageHash;
            CarHash = Boards[0].CarHash;
        }

        if (!string.IsNullOrEmpty(StageHash) && !string.IsNullOrEmpty(CarHash))
        {
            Entries = await Db.TimeTrials
                .Include(t => t.User)
                .Where(t => t.StageHash == StageHash && t.CarHash == CarHash && t.IsValid)
                .OrderBy(t => t.TotalTicks)
                .Take(100)
                .ToListAsync();
        }

        return Page();
    }
}
