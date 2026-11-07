using System.Text.Json;
using NFMWorldLibrary;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Archive.Validator;

/// <summary>
/// Validates untrusted archive input in a process the server can kill.
///
/// <see cref="TimeTrialSimulator"/> is synchronous and cannot be cancelled mid-call, so running
/// it in the web process would let one crafted replay pin a request thread indefinitely. The
/// archive instead runs this and applies a wall clock of its own.
///
/// Usage: validator tt-info &lt;replay-file&gt;
///        validator tt-sim &lt;replay-file&gt; &lt;stage-name&gt; &lt;car-name&gt;
///        validator radpack &lt;radpack-file&gt;
///
/// Every command prints one JSON object to stdout and exits non-zero on refusal.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
            return Fail("usage: validator <tt-info|tt-sim|radpack> <file> [stage] [car]");

        try
        {
            BackendGameSparker.Load();

            return args[0] switch
            {
                "tt-info" => TimeTrialInfo(args[1]),
                "tt-sim" => TimeTrialSimulation(args[1], args[2], args[3]),
                "radpack" => Radpack(args[1]),
                _ => Fail($"unknown command {args[0]}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int TimeTrialInfo(string path)
    {
        var replay = SavedTimeTrial.Load(File.ReadAllBytes(path));
        if (replay is null)
            return Fail("replay could not be read");

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            tickCount = replay.DemoData.Ticks.Count,
            checkpointCount = replay.Splits.SplitTimes.Count,
            replayVersion = replay.Version ?? 0,
            backendVersion = SavedTimeTrial.CURRENT_VERSION,
            stageName = replay.StageName,
            carName = replay.CarName
        }));

        // A replay recorded by a different backend is refused before it is simulated: its ticks
        // mean different physics, so the result would not be comparable.
        return (replay.Version ?? 0) == SavedTimeTrial.CURRENT_VERSION ? 0 : 2;
    }

    private static int TimeTrialSimulation(string path, string stageName, string carName)
    {
        var replay = SavedTimeTrial.Load(File.ReadAllBytes(path));
        if (replay is null)
            return Fail("replay could not be read");

        // Refused before simulating, for the same reason tt-info refuses it: another backend's
        // ticks are different physics, so the result is not comparable with the leaderboard's.
        if ((replay.Version ?? 0) != SavedTimeTrial.CURRENT_VERSION)
            return Fail($"The replay is version {replay.Version ?? 0}, this backend is version {SavedTimeTrial.CURRENT_VERSION}.");

        var expectedTicks = replay.DemoData.Ticks.Count;
        var elapsed = TimeTrialSimulator.Run(stageName, replay, carName, expectedTicks + 500);
        if (elapsed is null)
            return Fail("simulation did not finish within the tick limit");

        var ok = elapsed == expectedTicks;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok,
            elapsedTicks = elapsed.Value,
            expectedTicks,
            replayVersion = replay.Version ?? 0
        }));

        return ok ? 0 : 3;
    }

    private static int Radpack(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length > RadpackLimits.MaxCompressedBytes)
            return Fail("radpack exceeds the compressed size limit");

        var asset = RadpackSerializer.Deserialize(data, RadpackLimits.MaxDecompressedBytes);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = true,
            type = asset.Metadata.Type.ToString(),
            name = asset.Metadata.Name
        }));

        return 0;
    }

    private static int Fail(string message)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = message }));
        return 1;
    }
}
