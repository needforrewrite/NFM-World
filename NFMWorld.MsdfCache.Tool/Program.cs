// Bakes a font's distance fields ahead of time and writes them next to it.
//
// Startup bakes the fields of every character a screen might draw, and on the browser that is most
// of the launch: ~220 glyphs a font at milliseconds each. The fields only depend on the font's
// bytes and the bake constants, so they are baked here, once per build, and loaded at run time.
//
// Usage: NFMWorld.MsdfCache.Tool <fonts directory> [--check]
// Each <name>.ttf/otf gets a <name>.msdf beside it, which ShapeFont.LoadPrewarmed applies only when
// the font's bytes still hash to the one recorded in it.
//
// --check only reports what it would write and exits non-zero if anything is stale, for a target
// that wants an up-to-date decision without baking.

using Apos.Shapes;

// Printable ASCII plus Latin-1: what the game's UI actually draws, and the range a font is most
// likely to be asked for unexpectedly. Anything outside it still bakes on first draw, exactly as
// before - this is an accelerator, not a requirement.
var ranges = new (int From, int To)[] { (0x20, 0x7E), (0xA0, 0xFF) };

if (args.Length == 0) {
    Console.Error.WriteLine("usage: NFMWorld.MsdfCache.Tool <fonts directory> [--check]");
    return 2;
}

string directory = args[0];
bool check = args.Contains("--check");
if (!Directory.Exists(directory)) {
    Console.Error.WriteLine($"no such directory: {directory}");
    return 2;
}

var codePoints = new List<int>();
foreach ((int from, int to) in ranges) {
    for (int cp = from; cp <= to; cp++) codePoints.Add(cp);
}
string text = string.Concat(codePoints.Select(cp => char.ConvertFromUtf32(cp)));

int written = 0, skipped = 0, failed = 0;
foreach (string fontPath in Directory.EnumerateFiles(directory)
                                      .Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                                               || p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                                      .OrderBy(p => p, StringComparer.Ordinal)) {
    string cachePath = Path.ChangeExtension(fontPath, ".msdf");
    byte[] fontBytes = File.ReadAllBytes(fontPath);

    if (!ShapeFont.TryLoad(fontBytes, out ShapeFont? font)) {
        // A CFF font cannot be drawn at all here, so it has no fields to bake and the game will
        // report the same thing when it loads it.
        Console.WriteLine($"skip  {Path.GetFileName(fontPath)} (not drawable by this baker)");
        skipped++;
        continue;
    }

    using (font) {
        font.Prewarm(text);
        byte[] cache = font.SavePrewarmed();

        // Rebuilds are compared by content rather than by timestamp: baking is slow, and an
        // unchanged font must not rewrite the file and wake everything that copies it.
        if (File.Exists(cachePath) && File.ReadAllBytes(cachePath).AsSpan().SequenceEqual(cache)) {
            Console.WriteLine($"same  {Path.GetFileName(cachePath)} ({cache.Length / 1024} KiB)");
            skipped++;
            continue;
        }

        if (check) {
            Console.WriteLine($"stale {Path.GetFileName(cachePath)}");
            failed++;
            continue;
        }

        File.WriteAllBytes(cachePath, cache);
        Console.WriteLine($"write {Path.GetFileName(cachePath)} ({cache.Length / 1024} KiB, " +
                          $"{font.PrewarmedFieldCount} glyphs)");
        written++;
    }
}

Console.WriteLine($"done: {written} written, {skipped} unchanged, {failed} stale");
return failed > 0 ? 1 : 0;
