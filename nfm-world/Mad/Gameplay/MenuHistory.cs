using System.Text.Json;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public static class MenuHistory
{
    public const string DefaultStage = "nfm2/1_introductory";
    public const string DefaultCar = "nfmm/radicalone";
    private const string SavePath = "data/cfg/menu-history.json";

    public sealed record Selection(string Stage = DefaultStage, string Car = DefaultCar);
    public static Selection LastPlayed { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (TheVFS.VFS.FileExists(SavePath))
                LastPlayed = JsonSerializer.Deserialize<Selection>(TheVFS.VFS.ReadAllText(SavePath)) ?? new();
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load menu history: {ex.Message}");
            LastPlayed = new();
        }
    }

    public static string Stage => !string.IsNullOrWhiteSpace(LastPlayed.Stage)
        && (TheVFS.VFS.FileExists($"data/stages/{LastPlayed.Stage}.txt")
            || TheVFS.VFS.FileExists($"data/stages/{LastPlayed.Stage}.radpack"))
        ? LastPlayed.Stage : DefaultStage;

    public static Rad3d? Car => BackendGameSparker.GetCar(LastPlayed.Car ?? DefaultCar).Rad
        ?? BackendGameSparker.GetCar(DefaultCar).Rad;

    public static void Remember(string? stage = null, string? car = null)
    {
        var next = new Selection(stage ?? LastPlayed.Stage, car ?? LastPlayed.Car);
        if (next == LastPlayed) return;
        LastPlayed = next;
        try
        {
            using var stream = TheVFS.VFS.OpenWrite(SavePath);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonSerializer.Serialize(LastPlayed));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not save menu history: {ex.Message}");
        }
    }
}
