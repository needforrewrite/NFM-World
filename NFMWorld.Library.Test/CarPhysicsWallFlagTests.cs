using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class CarPhysicsWallFlagTests
{
    // BackendCar.Drive opens a Sentry transaction, and nothing else in this suite initializes it.
    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://public@localhost/1";
                options.TracesSampleRate = 0;
            });
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }
    }

    private static BackendCar CreateCar(int index)
    {
        var carName = $"wall-flag-test/{index}";
        var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
        return new BackendCar(car, index, 0, 0, isClientPlayer: false);
    }

    /// <summary>
    /// <see cref="Control.Wall"/> is set on contact and feeds the surfing bonus, so it has to
    /// be cleared every tick for every car. Only the car at index 0 used to clear it, which left
    /// a ghost (index 1) stuck surfing - and earning power - for the rest of the replay.
    /// </summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void Drive_ClearsTheWallFlag_ForAnyCarIndex(int carIndex)
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var stage = new BackendStage("empty");
        var car = CreateCar(carIndex);
        car.Control.Wall = 9999;

        car.Drive(stage);

        Assert.AreEqual(-1, car.Control.Wall);
    }
}
