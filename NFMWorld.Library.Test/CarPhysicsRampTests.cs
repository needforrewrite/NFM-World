using FixedMathSharp;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// Drives cars at full speed over stock jump pieces and checks they keep their speed.
/// </summary>
[TestClass]
public class CarPhysicsRampTests
{
    [ClassInitialize]
    public static void Init(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }

        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        if (BackendGameSparker.stage_parts.Count != 0) return;
        var n = 0;
        foreach (var part in BackendGameSparker.StageRads)
            BackendGameSparker.stage_parts[n++] = RadParser.ParseRad(
                TheVFS.VFS.ReadAllText($"data/models/nfmm/stage/{part}.rad"),
                $"nfmm/{part}"
            );
    }

    private static BackendStage Stage(string piece)
    {
        var lines = new List<string>();
        for (var z = 0; z <= 28000; z += 5600) lines.Add($"set(nfmm/road,0,{z},0)");
        lines.Add($"set(nfmm/{piece},0,30000,0)");
        lines.Add("maxl(3,-20000,0)");
        lines.Add("maxr(3,20000,0)");
        lines.Add("maxb(3,-20000,0)");
        lines.Add("maxt(3,200000,0)");
        return new BackendStage("ramp-test", StageLoader.FromLines("ramp-test", lines));
    }

    /// <summary>Speed at the ramp, slowest speed on it, and speed one second after landing.</summary>
    private static (double Entry, double Min, double AfterLanding) Jump(string carName, string piece, double yaw)
    {
        var stage = Stage(piece);
        var rad = RadParser.ParseRad(TheVFS.VFS.ReadAllText($"data/models/nfmm/cars/{carName}.rad"), carName);
        var car = new BackendCar(rad, 0, (int)(22000 * Math.Tan(yaw * Math.PI / 180)), 8000, isClientPlayer: false);
        ContO conto = car;
        conto.Xz = (Fixed64)yaw;
        var physics = car.CarPhysics;
        car.Control.Up = true;
        physics.Speed = car.Stats.Swits[2];
        for (var i = 0; i < 4; i++) physics.Scz[i] = car.Stats.Swits[2];

        var tps = (int)Math.Round(1 / Physics.PHYSICS_MULTIPLIER);
        double entry = 0, min = double.MaxValue;
        var onRamp = false;
        var air = 0;
        var landedAt = -1;
        for (var tick = 0; tick < 3000; tick++)
        {
            FrameTrace.ClearMessages(); // only the game loop clears it
            car.Drive(stage);
            if (physics.Wtouch && !physics.Gtouch && !onRamp)
            {
                onRamp = true;
                entry = (double)physics.Speed;
            }

            if (!onRamp) continue;
            if (landedAt >= 0)
            {
                if (tick == landedAt + 21 * tps) return (entry, min, (double)physics.Speed);
            }
            else if (!physics.Wtouch) air++;
            else if (air > 4 * tps) landedAt = tick;
            else
            {
                air = 0;
                min = Math.Min(min, (double)physics.Speed);
            }
        }

        Assert.Fail($"{carName} never landed after {piece}");
        return default;
    }

    [TestMethod]
    [DataRow("radicalone", 1.0)]
    [DataRow("radicalone", 4.0)]
    [DataRow("2000tornados", 2.0)]
    [DataRow("formula7", 1.0)]
    public void Drive_LaunchpadOffAxis_ScrapesTheRailWithoutStalling(string carName, double yaw)
    {
        // The original game loses about a third of its speed scraping the rail.
        var (entry, min, _) = Jump(carName, "launchpad", yaw);
        Assert.IsGreaterThan(entry * 0.4, min, $"entry {entry:0} min {min:0}");
    }

    [TestMethod]
    [DataRow("radicalone")]
    [DataRow("formula7")]
    [DataRow("drmonster")]
    public void Drive_TakeoffAtTopSpeed_KeepsSpeedAcrossTheRampJoints(string carName)
    {
        var (entry, min, _) = Jump(carName, "takeoff", 0);
        Assert.IsGreaterThan(entry * 0.95, min, $"entry {entry:0} min {min:0}");
    }
}
