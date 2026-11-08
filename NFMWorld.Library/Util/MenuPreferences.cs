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

    public static string[] GetStages(VirtualFS fs) => fs.EnumerateFiles("data/stages", "*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                       || path.EndsWith(".radpack", StringComparison.OrdinalIgnoreCase)
        )
        .Select(path => path["data/stages/".Length..path.LastIndexOf('.')])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(Comparer<string>.Create(NaturalCompare.CompareNatural))
        .ToArray();

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
