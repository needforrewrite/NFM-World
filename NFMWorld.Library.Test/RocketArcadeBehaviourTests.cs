using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using FixedMathSharp;

namespace NFMWorld.Library.Test;

/// <summary>
/// The Rocket arcade layer behaves as intended: a jump enters the stock stunt state, the first
/// jump is plain (never hijacked by a held throttle/turn key) and only the aerial keys make the
/// airborne jump directional, and the boost meter's fill follows the fuel actually spent.
/// </summary>
[TestClass]
public class RocketArcadeBehaviourTests
{
    private static readonly string CarName = $"rocket-arcade/{Guid.NewGuid():N}";

    [ClassInitialize]
    public static void InitSentry(TestContext _)
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
        }
    }

    private sealed class ClientContext(string stageName = "rocket-arcade") : IGamemodeContext
    {
        private BackendStage? _stage;

        public BackendStage CurrentStage => _stage ??= new BackendStage(stageName);
        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks { get; } = new NoCallbacks();

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class NoCallbacks : IClientCallbacks
    {
        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => throw new NotSupportedException();
    }

    private static (RocketGamemodeHarness Gamemode, BackendCar Car, BackendStage Stage) CreateSandboxed()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/rocket",
            "data/gamemodes/nfmm/rocket/gamemode.dll"
        );
        Assert.IsNotNull(plugin);
        var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        BackendGameSparker.dynamic_models.Add(CarName, (0, car));
        var gamemode = plugin.CreateClient(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "Human", CarName = CarName, Color = default, IsBot = false, IsClientPlayer = true
                    }
                ]
            },
            new ClientContext()
        );
        gamemode.Begin();

        // Tick past the countdown so the arcade layer is live.
        for (var i = 0; i < NFMWorldLibrary.Gamemodes.DotNet.GamemodeUtils.CountdownInterval * 4; i++)
            gamemode.GameTick();

        var player = gamemode.ClientPlayer!.Car!;
        player.Position = new Vector3d(0, World.Ground - player.GroundAt, 0);
        for (var i = 0; i < 8; i++)
            player.Drive(gamemode.CurrentStage);
        Assert.IsTrue(player.CarPhysics.Grounded, "the fixture car must start grounded");

        return (new RocketGamemodeHarness(gamemode, plugin), player, gamemode.CurrentStage);
    }

    [TestMethod]
    public void Jump_EntersTheStuntState()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            var control = car.Control;
            control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Drive(stage);

            Assert.AreEqual(2, car.CarPhysics.StuntState, "a jump must enter the airborne stunt state");
        }
        finally
        {
            harness.Dispose();
        }
    }

    [TestMethod]
    public void GroundedJump_UnderThrottle_IsNotDirectional()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            // Throttle held (the natural state while driving) must not tilt or steer the jump.
            car.Control.Up = true;
            car.Control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Drive(stage);

            Assert.AreEqual(
                0,
                car.CarPhysics.SummedWheelVelocity.X.rawValue,
                "a plain jump must not kick sideways just because throttle is held"
            );
        }
        finally
        {
            harness.Dispose();
        }
    }

    [TestMethod]
    public void AerialJump_HoldingAnAerialKey_IsDirectional()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            // First jump is the ground jump (straight up); the second, in the air, steers.
            car.Control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Drive(stage);
            car.Control.SetGamemodeAction(0, false);
            harness.Tick();
            car.Drive(stage);
            // The follow camera sits behind the car looking +Z, so screen-right is world +X.
            car.Control.Right = true;
            car.Control.SetGamemodeAction(0, true);
            harness.Tick();
            var beforeX = car.Position.X;
            car.Drive(stage);

            Assert.IsTrue(
                car.Position.X > beforeX,
                $"right+jump must move the car right (+X at yaw 0), moved {beforeX} -> {car.Position.X}"
            );
        }
        finally
        {
            harness.Dispose();
        }
    }


    /// <summary>
    /// Airborne, Left/Right steer the heading instead of rolling the car: the plugin clears the
    /// control bit and yaws, then restores it after the step. Holding Right must swing the nose
    /// towards screen-right (+X at yaw 0); Left the other way.
    /// </summary>
    [TestMethod]
    public void Airborne_LeftAndRight_TurnTheHeading()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            // Get airborne first; the ground jump is always straight up.
            car.Control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Control.SetGamemodeAction(0, false);
            for (var i = 0; i < 4; i++) harness.Tick();
            Assert.IsFalse(car.CarPhysics.Grounded, "the fixture must be airborne");

            var startNose = car.CarPhysics.NoseDirection;

            car.Control.Right = true;
            for (var i = 0; i < 4; i++) harness.Tick();
            var rightNose = car.CarPhysics.NoseDirection;
            car.Control.Right = false;

            car.Control.Left = true;
            for (var i = 0; i < 8; i++) harness.Tick();
            var leftNose = car.CarPhysics.NoseDirection;
            car.Control.Left = false;

            Assert.IsTrue(
                rightNose.X > startNose.X,
                $"airborne Right must swing the nose towards +X: {startNose} -> {rightNose}"
            );
            Assert.IsTrue(
                leftNose.X < rightNose.X,
                $"airborne Left must swing it the other way: {rightNose} -> {leftNose}"
            );
        }
        finally
        {
            harness.Dispose();
        }
    }

    private sealed class RocketGamemodeHarness(
        BaseClientGamemode gamemode,
        NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin plugin
    ) : IDisposable
    {
        public void Tick()
        {
            for (var i = 0; i < 3; i++)
                gamemode.GameTick();
        }

        public void Dispose()
        {
            try
            {
                gamemode.End();
            }
            finally
            {
                BackendGameSparker.dynamic_models.Remove(CarName);
                plugin.Unload();
            }
        }
    }
}
