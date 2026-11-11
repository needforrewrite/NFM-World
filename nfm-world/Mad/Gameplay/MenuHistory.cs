using System.Text.Json;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public static class MenuHistory
{
    public const string DefaultStage = "nfm2/1_introductory";
    public const string DefaultCar = "nfmm/radicalone";
    private const string SavePath = "data/cfg/menu-history.json";

    public sealed record Selection(string Stage = DefaultStage, string Car = DefaultCar);

    public static Selection LastPlayed { get; private set; } = new();

    public static void Load(VirtualFS? fileSystem = null)
    {
        var fs = fileSystem ?? TheVFS.VFS;
        LastPlayed = new();
        try
        {
            if (fs.FileExists(SavePath))
                LastPlayed = JsonSerializer.Deserialize<Selection>(fs.ReadAllText(SavePath)) ?? new();
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load menu history: {ex.Message}");
            LastPlayed = new();
        }
    }

    public static string Stage => !string.IsNullOrWhiteSpace(LastPlayed.Stage)
                                  && (RadpackReader.IsArchiveId(LastPlayed.Stage)
                                      || TheVFS.VFS.FileExists($"data/stages/{LastPlayed.Stage}.txt")
                                      || TheVFS.VFS.FileExists($"data/stages/{LastPlayed.Stage}.radpack"))
        ? LastPlayed.Stage
        : DefaultStage;

    public static Rad3d? Car => BackendGameSparker.GetCar((LastPlayed.Car ?? DefaultCar).Replace('\\', '/')).Rad
                                ?? BackendGameSparker.GetCar(DefaultCar).Rad
                                ?? BackendGameSparker.cars.Values.SelectMany(cars => cars).FirstOrDefault();

    public static void Remember(string? stage = null, string? car = null, VirtualFS? fileSystem = null)
    {
        var next = new Selection(stage ?? LastPlayed.Stage, car ?? LastPlayed.Car);
        if (next == LastPlayed) return;
        LastPlayed = next;
        try
        {
            using var stream = (fileSystem ?? TheVFS.VFS).OpenWrite(SavePath);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonSerializer.Serialize(LastPlayed));
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not save menu history: {ex.Message}");
        }
    }
}
