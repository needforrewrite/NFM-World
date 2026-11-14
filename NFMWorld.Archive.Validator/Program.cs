using System.Text.Json;
using NFMWorldLibrary;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Content;

namespace NFMWorld.Archive.Validator;

/// <summary>
/// Validates untrusted archive input in a process the server can kill.
///
/// <see cref="TimeTrialSimulator"/> is synchronous and cannot be cancelled mid-call, so running
/// it in the web process would let one crafted replay pin a request thread indefinitely. The
/// archive instead runs this and applies a wall clock of its own.
///
/// Usage: validator tt-info &lt;replay-file&gt;
///        validator tt-sim &lt;replay-file&gt; &lt;stageName&gt; &lt;car-name&gt; &lt;stage-radpack&gt; &lt;car-radpack&gt;
///        validator radpack &lt;radpack-file&gt;
///        validator rad &lt;legacy-rad-file&gt; &lt;asset-kind&gt;
///
/// Every command prints one JSON object to stdout and exits non-zero on refusal.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
            return Fail("usage: validator <tt-info|tt-sim|radpack|rad> <file> [stage] [car|kind]");

        try
        {
            BackendGameSparker.Load();

            return args[0] switch
            {
                "tt-info" => TimeTrialInfo(args[1]),
                "tt-sim" => TimeTrialSimulation(
                    args[1],
                    args[2],
                    args[3],
                    args.Length > 4 ? args[4] : null,
                    args.Length > 5 ? args[5] : null
                ),
                "radpack" => Radpack(args[1]),
                "rad" => LegacyRad(args[1], args.Length > 2 ? args[2] : null),
                _ => Fail($"unknown command {args[0]}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static RadpackAsset ReadTrialAsset(string path, string id, AssetKind kind)
    {
        if (path.StartsWith("stock:", StringComparison.Ordinal))
        {
            var stock = StockCatalog.Find(AppContext.BaseDirectory, kind, id);
            if (stock is null || stock.Hash != path["stock:".Length..])
                throw new InvalidDataException(
                    $"The server's shipped {kind.ToString().ToLowerInvariant()} '{id}' does not match this recording."
                );
            return RadpackSerializer.Deserialize(stock.Bytes);
        }

        return RadpackSerializer.Deserialize(File.ReadAllBytes(path));
    }

    private static int TimeTrialInfo(string path)
    {
        var replay = SavedTimeTrial.Load(File.ReadAllBytes(path));
        if (replay is null)
            return Fail("replay could not be read");

        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    ok = true,
                    tickCount = replay.DemoData.Ticks.Count,
                    checkpointCount = replay.Splits.SplitTimes.Count,
                    replayVersion = replay.Version ?? 0,
                    backendVersion = SavedTimeTrial.CURRENT_VERSION,
                    stageName = replay.StageName,
                    carName = replay.CarName
                }
            )
        );

        // A replay recorded by a different backend is refused before it is simulated: its ticks
        // mean different physics, so the result would not be comparable.
        return (replay.Version ?? 0) == SavedTimeTrial.CURRENT_VERSION ? 0 : 2;
    }

    private static int TimeTrialSimulation(
        string path,
        string stageName,
        string carName,
        string? stageRadpackPath,
        string? carRadpackPath
    )
    {
        var replay = SavedTimeTrial.Load(File.ReadAllBytes(path));
        if (replay is null)
            return Fail("replay could not be read");

        // Refused before simulating, for the same reason tt-info refuses it: another backend's
        // ticks are different physics, so the result is not comparable with the leaderboard's.
        if ((replay.Version ?? 0) != SavedTimeTrial.CURRENT_VERSION)
            return Fail(
                $"The replay is version {replay.Version ?? 0}, this backend is version {SavedTimeTrial.CURRENT_VERSION}."
            );

        // The stage must come from server-owned content, never from the replay. A
        // replay carries its own StageData and the simulator prefers it, which made the tick
        // count circular: a replay of a stage whose finish is at the start reproduces itself.
        if (stageRadpackPath is null)
            return Fail("No stage asset was supplied to simulate against.");

        var stageAsset = ReadTrialAsset(stageRadpackPath, stageName, AssetKind.Stage);
        if (stageAsset is not RadpackStage track)
            return Fail("The stage asset is not a track.");

        if (carRadpackPath is null)
            return Fail("No car asset was supplied to simulate against.");

        var carAsset = ReadTrialAsset(carRadpackPath, carName, AssetKind.Car);
        if (carAsset is not RadpackRad3d car)
            return Fail("The car asset is not a model.");

        var expectedTicks = replay.DemoData.Ticks.Count;
        var elapsed = TimeTrialSimulator.Run(
            stageName,
            replay,
            carName,
            expectedTicks + 500,
            track.Stage,
            car.Rad
        );
        if (elapsed is null)
            return Fail("simulation did not finish within the tick limit");

        var ok = elapsed == expectedTicks;
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    ok,
                    elapsedTicks = elapsed.Value,
                    expectedTicks,
                    replayVersion = replay.Version ?? 0
                }
            )
        );

        return ok ? 0 : 3;
    }

    private static int Radpack(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length > RadpackLimits.MaxCompressedBytes)
            return Fail("radpack exceeds the compressed size limit");

        var asset = RadpackSerializer.Deserialize(data, RadpackLimits.MaxCompressedBytes, RadpackLimits.MaxDecompressedBytes);
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    ok = true,
                    type = asset.Metadata.Type.ToString(),
                    kind = AssetKinds.FromRadpackType(asset.Metadata.Type) is { } assetType
                        ? AssetKinds.Name(assetType)
                        : null,
                    name = asset.Metadata.Name
                }
            )
        );

        return 0;
    }

    /// <summary>
    /// Reads a legacy <c>.rad</c> text model without executing anything. A bare model file carries
    /// no kind of its own, so the uploader's claim is checked against the kinds a model can be.
    /// </summary>
    private static int LegacyRad(string path, string? kindName)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length > RadpackLimits.MaxCompressedBytes)
            return Fail("legacy rad exceeds the size limit");

        if (!AssetKinds.TryParseName(kindName, out var kind) || !AssetKinds.IsLegacyRadKind(kind))
            return Fail("legacy .rad files may only be published as a Car, StagePiece or Wheel.");

        RadParser.ParseRad(System.Text.Encoding.UTF8.GetString(data), Path.GetFileNameWithoutExtension(path));

        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, type = "Rad3d", kind = AssetKinds.Name(kind) }));
        return 0;
    }

    private static int Fail(string message)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = message }));
        return 1;
    }
}
