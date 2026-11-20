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
/// The Rocket arcade layer behaves as intended: a jump is a plain impulse that leaves the
/// vanilla stunt state to the stage, the first jump is never hijacked by a held throttle/turn
/// key and only the aerial keys make the airborne jump directional, and the boost meter's fill
/// follows the fuel actually spent.
/// </summary>
[TestClass]
public class RocketArcadeBehaviourTests
{
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
        var carName = $"rocket-arcade/{Guid.NewGuid():N}";
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var plugin = NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin.Load(
            "nfmm/rocket",
            "data/gamemodes/nfmm/rocket/gamemode.dll"
        );
        Assert.IsNotNull(plugin);
        var car = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], carName);
        BackendGameSparker.dynamic_models.Add(carName, (0, car));
        var gamemode = plugin.CreateClient(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "Human", CarName = carName, Color = default, IsBot = false, IsClientPlayer = true
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

        return (new RocketGamemodeHarness(gamemode, plugin, carName), player, gamemode.CurrentStage);
    }

    /// <summary>
    /// The arcade jump is an impulse only: it lifts the car off the ground but leaves the
    /// vanilla stunt state alone. Forcing `<see cref="NFMWorldLibrary.Mad.CarPhysics.Mtouch"/>
    /// = false; StuntState = 1` here would hand the same keys to the stock air controls as well
    /// as to this gamemode's own heading control.
    /// </summary>
    [TestMethod]
    public void Jump_LiftsTheCar_WithoutEnteringTheVanillaStuntState()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            var restingY = car.Position.Y;
            var control = car.Control;
            control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Drive(stage);

            // Y is positive downward, so a rising car's position value decreases.
            Assert.IsFalse(car.CarPhysics.Grounded, "a jump must leave the ground");
            Assert.IsLessThan(
                restingY,
                car.Position.Y,
                $"a jump must lift the car, moved {restingY} -> {car.Position.Y}"
            );
            Assert.AreEqual(0, car.CarPhysics.StuntState, "an arcade jump must not enter the vanilla stunt state");
        }
        finally
        {
            harness.Dispose();
        }
    }

    /// <summary>
    /// The tuning values must be read at use, not baked in: raising the jump impulse has to
    /// change how high the same jump goes. This is what makes the dev console's `tune` command
    /// (and any future config option) mean anything.
    /// </summary>
    [TestMethod]
    public void JumpImpulse_IsReadFromTuning()
    {
        static float JumpHeight(RocketGamemodeHarness harness, BackendCar car, BackendStage stage)
        {
            var restingY = (float)car.Position.Y;
            car.Control.SetGamemodeAction(0, true);
            harness.Tick();
            car.Drive(stage);

            var lowest = (float)car.Position.Y;
            for (var i = 0; i < 12; i++)
            {
                harness.Tick();
                car.Drive(stage);
                lowest = System.MathF.Min(lowest, (float)car.Position.Y);
            }

            // Y is positive downward, so the peak height is the smallest Y.
            return restingY - lowest;
        }

        var (weak, weakCar, weakStage) = CreateSandboxed();
        try
        {
            weak.SetTuning("JumpImpulse", 60);
            var weakRise = JumpHeight(weak, weakCar, weakStage);

            var (strong, strongCar, strongStage) = CreateSandboxed();
            try
            {
                strong.SetTuning("JumpImpulse", 400);
                var strongRise = JumpHeight(strong, strongCar, strongStage);

                Assert.IsTrue(
                    strongRise > weakRise,
                    $"a stronger jump must rise higher: {weakRise} vs {strongRise}"
                );
            }
            finally
            {
                strong.Dispose();
            }
        }
        finally
        {
            weak.Dispose();
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

    /// <summary>
    /// An empty tank with the button held must not flicker the boost on and off. It used to:
    /// one tick regen made Boost &gt; 0, the next tick spent it back to 0, so the boost loop
    /// sound restarted 63 times a second - heard as a drum roll ("trrrrr").
    /// </summary>
    [TestMethod]
    public void EmptyTank_HeldBoost_DoesNotFlickerTheBoostState()
    {
        var (harness, car, stage) = CreateSandboxed();
        try
        {
            harness.SetBoost(0);
            car.Control.SetGamemodeAction(1, true);

            var transitions = 0;
            var wasBoosting = harness.IsBoosting;
            for (var i = 0; i < 40; i++)
            {
                harness.Tick();
                if (harness.IsBoosting != wasBoosting)
                {
                    transitions++;
                    wasBoosting = harness.IsBoosting;
                }
            }

            Assert.IsFalse(harness.IsBoosting, "an empty tank must not latch boost on");
            Assert.AreEqual(
                0,
                transitions,
                $"boost must not flicker while the tank is empty (saw {transitions} transitions)"
            );
        }
        finally
        {
            harness.Dispose();
        }
    }

    private sealed class RocketGamemodeHarness(
        BaseClientGamemode gamemode,
        NFMWorldLibrary.Gamemodes.DotNet.GamemodePlugin plugin,
        string carName
    ) : IDisposable
    {
        /// <summary>The gamemode's per-car arcade state object (a private nested type).</summary>
        private object CarState
        {
            get
            {
                var cars = gamemode.GetType()
                    .GetField("_cars", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(gamemode)!;
                return cars.GetType().GetProperty("Item")!.GetValue(cars, [0])!;
            }
        }

        /// <summary>Sets one arcade tuning value the way the dev console's `tune` command does.</summary>
        public void SetTuning(string name, double value)
        {
            var tuning = gamemode.GetType().GetProperty("Tuning")!.GetValue(gamemode)!;
            tuning.GetType().GetField(name)!.SetValue(tuning, value);
        }

        public bool IsBoosting =>
            (bool)CarState.GetType().GetField("Boosting")!.GetValue(CarState)!;

        public void SetBoost(float value) =>
            CarState.GetType().GetField("Boost")!.SetValue(CarState, value);

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
                BackendGameSparker.dynamic_models.Remove(carName);
                plugin.Unload();
            }
        }
    }
}
