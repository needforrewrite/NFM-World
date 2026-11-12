using NFMWorld.PhysicsFS;

namespace NFMWorldLibrary.Util;

public static class MenuPreferences
{
    public const string LastPlayedStage = "last";
    public const string StageSoundtrack = "stage";
    public const string MusicDirectory = "data/music/interface/";

    public static string NormalizeStage(string selection) => IsRelativeAsset(selection) ? selection : LastPlayedStage;
    public static string NormalizeMusic(string selection) => IsRelativeAsset(selection) ? selection : StageSoundtrack;

    public static string ResolveStage(VirtualFS fs, string selection, string lastPlayed)
        => selection != LastPlayedStage
           && IsRelativeAsset(selection)
           && (fs.FileExists($"data/stages/{selection}.txt") || fs.FileExists($"data/stages/{selection}.radpack"))
            ? selection
            : lastPlayed;

    public static string? ResolveMusic(VirtualFS fs, string selection)
        => selection != StageSoundtrack
           && IsRelativeAsset(selection)
           && IsMusicFile(selection)
           && (fs.FileExists(MusicDirectory + selection)
               || fs.EnumerateFiles(MusicDirectory, "*", SearchOption.AllDirectories)
               .Contains(MusicDirectory + selection, StringComparer.OrdinalIgnoreCase))
            ? MusicDirectory + selection
            : null;

    public static string[] GetStages(VirtualFS fs) => StageFiles(fs)
        .Select(StageIdOf)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(Comparer<string>.Create(NaturalCompare.CompareNatural))
        .ToArray();

    /// <summary>
    /// The display name each stage declares in its <c>name(...)</c> line, keyed by stage id.
    /// </summary>
    /// <remarks>
    /// Only text stages are read: a radpack would have to be decompressed and parsed to reach the
    /// same line, which is far too much work for a list, so those keep their file name.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> GetStageNames(VirtualFS fs)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in StageFiles(fs))
        {
            if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
            var id = StageIdOf(path);
            if (names.ContainsKey(id)) continue;

            try
            {
                using var stream = fs.OpenRead(path);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } raw)
                {
                    // Mirrors StageLoader: the directive may sit behind a "ds:"/"?" editor prefix.
                    var line = raw.Trim();
                    if (line.StartsWith("ds:")) line = line[3..];
                    if (line.StartsWith('?')) line = line[1..];
                    if (!line.StartsWith("name(")) continue;

                    if (Utility.GetString("name(", line, 0).Trim() is { Length: > 0 } name)
                    {
                        names[id] = name;
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logging.Warning($"Could not read the name of stage '{id}': {ex.Message}");
            }
        }

        return names;
    }

    private static string[] StageFiles(VirtualFS fs) => fs.EnumerateFiles("data/stages", "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                       || path.EndsWith(".radpack", StringComparison.OrdinalIgnoreCase)
        )
        .ToArray();

    private static string StageIdOf(string path) => path["data/stages/".Length..path.LastIndexOf('.')];

    public static string[] GetMusic(VirtualFS fs) => fs.EnumerateFiles(MusicDirectory, "*", SearchOption.AllDirectories)
        .Where(IsMusicFile)
        .Select(path => path[MusicDirectory.Length..])
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool IsMusicFile(string path) => Path.GetExtension(path).ToLowerInvariant()
        is ".mp3" or ".ogg" or ".wav" or ".flac" or ".mod" or ".xm" or ".s3m" or ".it";

    private static bool IsRelativeAsset(string selection) => !string.IsNullOrWhiteSpace(selection)
                                                             && !selection.Any(char.IsControl)
                                                             && !selection.Contains(':')
                                                             && !selection.Contains('\\')
                                                             && selection.Split('/').All(part
                                                                 => part.Length > 0 && part is not "." and not ".."
                                                             );
}
