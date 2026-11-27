using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace NFMWorld.Archive.Data;

/// <summary>Hashtags start with # followed by Unicode letters, digits or underscore; subsequent
/// characters may also include combining marks. Punctuation and whitespace end a tag.</summary>
public static class AssetTags
{
    public static string Extract(string? description)
    {
        if (string.IsNullOrEmpty(description))
            return "";

        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < description.Length; i++)
        {
            if (description[i] != '#')
                continue;
            var end = i + 1;
            while (end < description.Length && Rune.TryGetRuneAt(description, end, out var rune) &&
                   IsTagCharacter(rune, end != i + 1))
                end += rune.Utf16SequenceLength;
            if (end == i + 1)
                continue;
            var tag = description[i..end];
            if (seen.Add(tag))
                tags.Add(tag);
            i = end - 1;
        }

        return string.Join(' ', tags);
    }

    public static string[] Split(string? tags) =>
        string.IsNullOrEmpty(tags) ? [] : tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsTagCharacter(Rune rune, bool hasPrevious) =>
        Rune.IsLetterOrDigit(rune) || rune.Value == '_' || (hasPrevious && Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark);

    // SQLite LIKE only folds ASCII. Store and search invariant-uppercase text instead so Unicode
    // hashtags are matched case-insensitively without changing their displayed spelling.
    public static string SearchText(string tags) => tags.ToUpperInvariant();

    // The nullable migration column marks rows created before tags existed. Batching avoids
    // loading the entire archive at startup and makes an interrupted backfill resumable.
    public static async Task BackfillAsync(ArchiveDbContext db, CancellationToken ct)
    {
        while (true)
        {
            var rows = await db.Assets.Where(a => a.Tags == null || a.TagsSearch == null)
                .OrderBy(a => a.Id).Take(500).ToListAsync(ct);
            if (rows.Count == 0)
                return;
            foreach (var row in rows)
            {
                row.Tags = Extract(row.Description);
                row.TagsSearch = SearchText(row.Tags);
            }

            await db.SaveChangesAsync(ct);
            foreach (var row in rows)
                db.Entry(row).State = EntityState.Detached;
        }
    }
}
