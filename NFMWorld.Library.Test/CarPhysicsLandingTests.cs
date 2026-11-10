using FixedMathSharp;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// Drops real cars onto flat ground and checks they end up on their wheels instead of balancing
/// on the nose, the tail or a side.
/// </summary>
[TestClass]
public class CarPhysicsLandingTests
{
    private static readonly string[] Cars = ["2000tornados", "drmonster"];
    private static readonly Dictionary<string, Rad3d> Rads = [];

    // BackendCar.Drive opens a Sentry transaction, and nothing else in this suite initializes it.
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
    }

    private static BackendCar CreateCar(string name)
    {
        if (!Rads.TryGetValue(name, out var rad))
            Rads[name] = rad = RadParser.ParseRad(TheVFS.VFS.ReadAllText($"data/models/nfmm/cars/{name}.rad"), name);
        var car = new BackendCar(rad, 0, 0, 0, isClientPlayer: false);
        // The empty stage is all off-road; a road's traction is the harder case.
        car.CarPhysics.Stat = car.Stats = car.Stats with { OffRoadGrip = Fixed64.One };
        return car;
    }

    private static double FromFlat(Fixed64 angle)
    {
        var a = (double)angle % 180;
        if (a < 0) a += 180;
        return Math.Min(a, 180 - a);
    }

    /// <summary>Null when the car came to rest on its wheels, otherwise what went wrong.</summary>
    private static string? Run(BackendStage stage, BackendCar car, bool releaseOnLanding)
    {
        var physics = car.CarPhysics;
        var landed = false;
        var ticksOnEnd = 0;
        for (var tick = 0; tick < 700; tick++)
        {
            FrameTrace.ClearMessages(); // only the game loop clears it
            car.Drive(stage);
            landed |= physics.Wtouch;
            if (landed && releaseOnLanding)
                car.Control.Handb = car.Control.Up = car.Control.Down = car.Control.Left = car.Control.Right = false;
            if (physics.Wtouch && (FromFlat(physics.Pzy) > 45 || FromFlat(physics.Pxy) > 45)) ticksOnEnd++;
        }

        if (FromFlat(physics.Pxy) > 12 || FromFlat(physics.Pzy) > 12 || physics.BadLanding)
            return $"ended at Pxy={(double)physics.Pxy:0.0} Pzy={(double)physics.Pzy:0.0} BadLanding={physics.BadLanding}";
        // About two seconds of contact while on end; a car that tips over needs a fraction of that.
        return ticksOnEnd > 120 ? $"touched the ground on end for {ticksOnEnd} ticks" : null;
    }

    [TestMethod]
    public void Drive_DroppedOnEnd_SettlesOnItsWheels()
    {
        var stage = new BackendStage("empty");
        var failures = new List<string>();
        foreach (var name in Cars)
        foreach (var roll in new[] { 0, 30, 60, 75, 90, 105, 120, 150, 180, 270 })
            for (var pitch = 60; pitch < 300; pitch += pitch is >= 120 and < 240 ? 120 : 10)
            {
                var car = CreateCar(name);
                ContO conto = car;
                conto.Y -= 400;
                conto.Xy = roll;
                conto.Zy = pitch;
                car.CarPhysics.Pxy = roll;
                car.CarPhysics.Pzy = pitch;

                if (Run(stage, car, releaseOnLanding: false) is { } failure)
                    failures.Add($"{name} roll={roll} pitch={pitch}: {failure}");
            }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(10)));
    }

    [TestMethod]
    public void Drive_StuntLanding_SettlesOnItsWheels()
    {
        var stage = new BackendStage("empty");
        var failures = new List<string>();
        foreach (var name in Cars)
        foreach (var input in new[] { "down", "down+left", "up+right", "down+left+release" })
        foreach (var speed in new[] { 0, 200 })
            for (var height = 300; height <= 4000; height += 200)
            {
                var car = CreateCar(name);
                ContO conto = car;
                car.Control.Handb = true;
                car.Control.Up = input.Contains("up");
                car.Control.Down = input.Contains("down");
                car.Control.Left = input.Contains("left");
                car.Control.Right = input.Contains("right");
                conto.Y -= height;
                for (var i = 0; i < 4; i++) car.CarPhysics.Scz[i] = speed;

                if (Run(stage, car, input.Contains("release")) is { } failure)
                    failures.Add($"{name} input={input} speed={speed} height={height}: {failure}");
            }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(10)));
    }
}
